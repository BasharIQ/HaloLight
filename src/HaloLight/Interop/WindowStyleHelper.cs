using System.Drawing;

namespace HaloLight.Interop;

internal static class WindowStyleHelper
{
    public static void ApplyOverlayStyles(IntPtr handle)
    {
        var currentStyles = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        var newStyles = currentStyles |
                        NativeMethods.WsExLayered |
                        NativeMethods.WsExTransparent |
                        NativeMethods.WsExToolWindow;

        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(newStyles));

        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove |
            NativeMethods.SwpNoSize |
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpFrameChanged);
    }

    public static void PositionOverlay(IntPtr handle, Rectangle bounds)
    {
        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTopmost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoActivate |
            NativeMethods.SwpShowWindow);
    }

    public static void ApplyCaptureExclusion(IntPtr handle, bool excludeFromCapture)
    {
        var requestedMode = excludeFromCapture ? NativeMethods.WdaExcludeFromCapture : NativeMethods.WdaNone;
        if (NativeMethods.SetWindowDisplayAffinity(handle, requestedMode))
        {
            return;
        }

        if (excludeFromCapture)
        {
            _ = NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WdaMonitor);
        }
    }
}
