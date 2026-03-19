using System.Runtime.InteropServices;

namespace HaloLight.Interop;

internal static class NativeMethods
{
    public const int EddGetDeviceInterfaceName = 0x00000001;
    public const int GwlExStyle = -20;
    public const int WsExTransparent = 0x00000020;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExLayered = 0x00080000;
    public const int SwpNoSize = 0x0001;
    public const int SwpNoMove = 0x0002;
    public const int SwpNoActivate = 0x0010;
    public const int SwpShowWindow = 0x0040;
    public const int SwpFrameChanged = 0x0020;
    public const int WmHotkey = 0x0312;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint VkL = 0x4C;
    public const uint WdaNone = 0x00000000;
    public const uint WdaMonitor = 0x00000001;
    public const uint WdaExcludeFromCapture = 0x00000011;
    public static readonly IntPtr HwndTopmost = new(-1);

    public static IntPtr GetWindowLongPtr(IntPtr handle, int index)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(handle, index) : GetWindowLong32(handle, index);
    }

    public static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(handle, index, value) : SetWindowLong32(handle, index, value);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLong32(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong32(IntPtr handle, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr handle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowDisplayAffinity(IntPtr handle, uint affinity);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr handle, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DisplayDevice
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public DisplayDeviceStateFlags StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags]
    public enum DisplayDeviceStateFlags : int
    {
        AttachedToDesktop = 0x00000001,
        MultiDriver = 0x00000002,
        PrimaryDevice = 0x00000004,
        MirroringDriver = 0x00000008,
        VgaCompatible = 0x00000010,
        Removable = 0x00000020,
        ModesPruned = 0x08000000,
        Remote = 0x04000000,
        Disconnect = 0x02000000
    }
}
