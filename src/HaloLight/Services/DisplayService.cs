using System.Windows.Forms;
using System.Runtime.InteropServices;
using HaloLight.Interop;
using HaloLight.Models;

namespace HaloLight.Services;

public sealed class DisplayService
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        return Screen.AllScreens
            .Select(screen => new DisplayInfo
            {
                DeviceName = screen.DeviceName,
                DisplayName = BuildDisplayName(screen),
                Bounds = screen.Bounds,
                IsPrimary = screen.Primary
            })
            .ToList();
    }

    public DisplayInfo GetSelectedDisplay(string? deviceName)
    {
        var displays = GetDisplays();
        return displays.FirstOrDefault(display => string.Equals(display.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
               ?? displays.FirstOrDefault(display => display.IsPrimary)
               ?? displays[0];
    }

    private static string BuildDisplayName(Screen screen)
    {
        var friendlyName = GetFriendlyMonitorName(screen) ?? screen.DeviceName;
        var label = $"{friendlyName} ({screen.Bounds.Width}x{screen.Bounds.Height})";
        return screen.Primary ? $"{label} - Primary" : label;
    }

    private static string? GetFriendlyMonitorName(Screen screen)
    {
        var adapter = CreateDisplayDevice();
        if (!NativeMethods.EnumDisplayDevices(screen.DeviceName, 0, ref adapter, NativeMethods.EddGetDeviceInterfaceName))
        {
            return null;
        }

        for (uint monitorIndex = 0; ; monitorIndex++)
        {
            var monitor = CreateDisplayDevice();
            if (!NativeMethods.EnumDisplayDevices(screen.DeviceName, monitorIndex, ref monitor, NativeMethods.EddGetDeviceInterfaceName))
            {
                break;
            }

            if (IsUsableMonitor(monitor) && !string.IsNullOrWhiteSpace(monitor.DeviceString))
            {
                return monitor.DeviceString.Trim();
            }
        }

        return !string.IsNullOrWhiteSpace(adapter.DeviceString)
            ? adapter.DeviceString.Trim()
            : null;
    }

    private static bool IsUsableMonitor(NativeMethods.DisplayDevice monitor)
    {
        return (monitor.StateFlags & NativeMethods.DisplayDeviceStateFlags.AttachedToDesktop) != 0
               && (monitor.StateFlags & NativeMethods.DisplayDeviceStateFlags.MirroringDriver) == 0;
    }

    private static NativeMethods.DisplayDevice CreateDisplayDevice()
    {
        return new NativeMethods.DisplayDevice
        {
            cb = Marshal.SizeOf<NativeMethods.DisplayDevice>()
        };
    }
}
