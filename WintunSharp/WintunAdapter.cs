using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WintunSharp;

/// <summary>
/// Represents a wintun virtual network adapter.
/// </summary>
public sealed class WintunAdapter : IDisposable
{
    private readonly IntPtr _handle;
    private bool _disposed;

    /// <summary>
    /// The kernel LUID that identifies the adapter.
    /// </summary>
    public long Luid { get; }

    private WintunAdapter(IntPtr handle, long luid)
    {
        _handle = handle;
        Luid = luid;
    }

    /// <summary>
    /// Create a new wintun adapter.
    /// </summary>
    /// <param name="name">Friendly name shown in Network Connections.</param>
    /// <param name="tunnelType">Identifies the tunnel type to applications (e.g. "WireGuard").</param>
    /// <param name="requestedGuid">Optional GUID for the network connection device instance.</param>
    public WintunAdapter(string name, string tunnelType, Guid? requestedGuid = null)
    {
        var handle = WintunCreateAdapterSafe(name, tunnelType, requestedGuid);
        if (handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to create adapter \"{name}\"");

        _handle = handle;
        Luid = GetLuid(handle);
    }

    /// <summary>
    /// Open an existing wintun adapter by name.
    /// </summary>
    public static WintunAdapter Open(string name)
    {
        var handle = WintunNative.WintunOpenAdapter(name);
        if (handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to open adapter \"{name}\"");

        var luid = GetLuid(handle);
        return new WintunAdapter(handle, luid);
    }

    /// <summary>
    /// Start a new packet I/O session on this adapter.
    /// </summary>
    /// <param name="ringCapacity">
    /// Ring buffer capacity in bytes. Must be between 128 KiB and 64 MiB. Default 1 MiB.
    /// </param>
    public WintunSession StartSession(int ringCapacity = 1 * 1024 * 1024)
    {
        ThrowIfDisposed();

        if (ringCapacity < WintunNative.MinRingCapacity || ringCapacity > WintunNative.MaxRingCapacity)
            throw new ArgumentOutOfRangeException(nameof(ringCapacity),
                $"Must be between {WintunNative.MinRingCapacity} and {WintunNative.MaxRingCapacity}");

        var session = WintunNative.WintunStartSession(_handle, (uint)ringCapacity);
        if (session == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start wintun session");

        return new WintunSession(session);
    }

    /// <summary>
    /// Delete the wintun driver entirely. Use with caution — removes the driver, not just this adapter.
    /// </summary>
    public static bool DeleteDriver() => WintunNative.WintunDeleteDriver();

    /// <summary>
    /// Query the currently installed wintun driver version (e.g. 0x24 = v2.4).
    /// </summary>
    public static int DriverVersion => (int)WintunNative.WintunGetRunningDriverVersion();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        WintunNative.WintunCloseAdapter(_handle);
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(this?.GetType().FullName);
    }

    private static IntPtr WintunCreateAdapterSafe(string name, string tunnelType, Guid? guid)
    {
        unsafe
        {
            if (guid.HasValue)
            {
                var g = guid.Value;
                return WintunNative.WintunCreateAdapter(name, tunnelType, &g);
            }

            return WintunNative.WintunCreateAdapter(name, tunnelType, null);
        }
    }

    private static long GetLuid(IntPtr handle)
    {
        WintunNative.WintunGetAdapterLUID(handle, out var luid);
        return luid.Value;
    }
}
