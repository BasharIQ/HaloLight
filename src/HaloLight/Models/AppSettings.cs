namespace HaloLight.Models;

public sealed class AppSettings
{
    public const string DefaultSecondaryColorHex = "#29D7FF";

    public bool IsEnabled { get; set; } = true;
    public double Brightness { get; set; } = 45;
    public double ColorTemperature { get; set; } = 4600;
    public double EdgeThickness { get; set; } = 140;
    public string SecondaryColorHex { get; set; } = DefaultSecondaryColorHex;
    public string? MonitorDeviceName { get; set; }
    public bool LaunchAtStartup { get; set; }
    public bool ExcludeFromCapture { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            IsEnabled = IsEnabled,
            Brightness = Brightness,
            ColorTemperature = ColorTemperature,
            EdgeThickness = EdgeThickness,
            SecondaryColorHex = SecondaryColorHex,
            MonitorDeviceName = MonitorDeviceName,
            LaunchAtStartup = LaunchAtStartup,
            ExcludeFromCapture = ExcludeFromCapture
        };
    }
}
