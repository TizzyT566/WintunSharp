using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace WintunSharp;

/// <summary>
/// Represents an active wintun session for sending and receiving packets.
/// </summary>
public sealed class WintunSession : IDisposable
{
    /// <summary>
    /// Maximum IP packet size supported by wintun (65535).
    /// </summary>
    public const int MaxPacketSize = 0xFFFF;

    private readonly IntPtr _handle;
    private bool _disposed;

    internal WintunSession(IntPtr handle)
    {
        _handle = handle;
        // The event handle is owned by wintun and lifetime-tied to the session.
        // We wrap it only for WaitHandle compatibility, not ownership.
        ReadEvent = new(WintunNative.WintunGetReadWaitEvent(handle), ownsHandle: false);
    }

    /// <summary>
    /// OS event that becomes signaled when received packets are available in the ring buffer.
    /// </summary>
    public SafeWaitHandle ReadEvent { get; }

    /// <summary>
    /// Try to receive the next packet from the ring buffer.
    /// </summary>
    /// <param name="packet">Copied packet data (safe to retain after the call returns).</param>
    /// <returns><c>true</c> if a packet was received; <c>false</c> if the ring is empty or full.</returns>
    public bool TryReceive(out byte[]? packet)
    {
        ThrowIfDisposed();

        unsafe
        {
            var ptr = WintunNative.WintunReceivePacket(_handle, out uint size);
            if (ptr == null)
            {
                packet = null;
                return false;
            }

            // Copy data out before releasing — the ring buffer may be overwritten.
            var data = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy((IntPtr)ptr, data, 0, (int)size);
            WintunNative.WintunReleaseReceivePacket(_handle, ptr);
            packet = data;
            return true;
        }
    }

    /// <summary>
    /// Send a packet through the adapter.
    /// </summary>
    /// <param name="packet">Raw IP packet bytes (max 65535).</param>
    public void Send(ReadOnlySpan<byte> packet)
    {
        ThrowIfDisposed();

        if (packet.Length > WintunNative.MaxIpPacketSize)
            throw new ArgumentOutOfRangeException(nameof(packet),
                $"Packet exceeds maximum IP packet size ({WintunNative.MaxIpPacketSize})");

        unsafe
        {
            var ptr = WintunNative.WintunAllocateSendPacket(_handle, (uint)packet.Length);
            if (ptr == null)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to allocate send packet (ring buffer full?)");

            fixed (byte* p = packet)
                Buffer.MemoryCopy(p, ptr, packet.Length, packet.Length);
            WintunNative.WintunSendPacket(_handle, ptr);
        }
    }

    /// <summary>
    /// Start an async packet reader that pushes received packets into a channel.
    /// </summary>
    /// <param name="bufferSize">Channel depth (default 16). Excess packets drop oldest.</param>
    /// <param name="linkedToken">Cancellation token to stop the reader early.</param>
    /// <returns>Tuple of cancellation source to stop reading, and channel reader for packets.</returns>
    public (CancellationTokenSource Cts, ChannelReader<byte[]> Reader) ReceiveAsync(
        int bufferSize = 16, CancellationToken linkedToken = default)
    {
        ThrowIfDisposed();

        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        var cts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    // Drain available packets from the ring buffer
                    while (TryReceive(out var packet))
                        await channel.Writer.WriteAsync(packet!, cts.Token);

                    // Ring empty — wait for more data
                    await WaitForEventAsync(ReadEvent.DangerousGetHandle(), cts.Token);
                }
            }
            catch (OperationCanceledException) { /* normal exit */ }
            finally
            {
                channel.Writer.Complete();
            }
        }); // No token here — ensures task starts and can complete the channel in finally

        return (cts, channel.Reader);
    }

    private static async Task WaitForEventAsync(IntPtr eventHandle, CancellationToken ct)
    {
        // Wait on the wintun read event without blocking a thread-pool thread.
        // Uses kernel32.WaitForSingleObject with 10ms timeout + cancellation check loop.
        while (!ct.IsCancellationRequested)
        {
            var result = WintunNative.WaitForSingleObject(eventHandle, 10);
            if (result == WintunNative.WaitObject0)
                return; // Event signaled — packets available
            ct.ThrowIfCancellationRequested();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        WintunNative.WintunEndSession(_handle);
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(this?.GetType().FullName);
    }
}
