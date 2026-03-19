using HaloLight.Models;
using HaloLight.Views;

namespace HaloLight.Services;

public sealed class OverlayService : IDisposable
{
    private readonly DisplayService _displayService;
    private readonly OverlayWindow _overlayWindow;

    public OverlayService(DisplayService displayService)
    {
        _displayService = displayService;
        _overlayWindow = new OverlayWindow();
    }

    public void Apply(AppSettings settings)
    {
        var display = _displayService.GetSelectedDisplay(settings.MonitorDeviceName);
        _overlayWindow.ApplySettings(settings, display);

        if (settings.IsEnabled)
        {
            _overlayWindow.ShowAnimated();
            return;
        }

        _overlayWindow.HideAnimated();
    }

    public void Dispose()
    {
        _overlayWindow.Close();
    }
}
