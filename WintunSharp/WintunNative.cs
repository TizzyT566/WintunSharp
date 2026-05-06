using System;
using System.Runtime.InteropServices;

namespace WintunSharp;

internal static class WintunNative
{
    public const int MinRingCapacity = 0x20000;
    public const int MaxRingCapacity = 0x4000000;
    public const int MaxIpPacketSize = 0xFFFF;

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static unsafe extern IntPtr WintunCreateAdapter(
        string Name,
        string TunnelType,
        Guid* RequestedGUID);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static unsafe extern IntPtr WintunOpenAdapter(string Name);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static extern void WintunCloseAdapter(IntPtr Adapter);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static extern void WintunGetAdapterLUID(IntPtr Adapter, out NET_LUID Luid);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    internal static unsafe extern IntPtr WintunStartSession(IntPtr Adapter, uint Capacity);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static extern void WintunEndSession(IntPtr Session);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static extern IntPtr WintunGetReadWaitEvent(IntPtr Session);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    internal static unsafe extern byte* WintunReceivePacket(IntPtr Session, out uint PacketSize);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static unsafe extern void WintunReleaseReceivePacket(IntPtr Session, byte* Packet);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    internal static unsafe extern byte* WintunAllocateSendPacket(IntPtr Session, uint PacketSize);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static unsafe extern void WintunSendPacket(IntPtr Session, byte* Packet);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void LoggerCallback(WinTunLoggerLevel Level, ulong Timestamp, string Message);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi)]
    internal static extern void WintunSetLogger(LoggerCallback? NewLogger);

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    internal static extern uint WintunGetRunningDriverVersion();

    [DllImport("wintun", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WintunDeleteDriver();

    // Win32 kernel32 for event waiting (used by async reader)
    internal const uint WaitObject0 = 0;

    [DllImport("kernel32", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    internal static extern uint WaitForSingleObject(IntPtr Handle, uint Milliseconds);

    internal enum WinTunLoggerLevel : int
    {
        Info = 0,
        Warn = 1,
        Err = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NET_LUID
    {
        public readonly long Value;

        public override readonly string ToString() => $"{{ {Value:X16} }}";
    }
}