using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using HaloLight.Models;
using HaloLight.Services;

using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Colors = System.Windows.Media.Colors;
using GradientStopCollection = System.Windows.Media.GradientStopCollection;
using LinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using Point = System.Windows.Point;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace HaloLight.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Action<AppSettings> _applySettings;
    private readonly Action<AppSettings> _persistSettings;
    private readonly DispatcherTimer _saveTimer;
    private AppSettings _pendingSettings;
    private bool _isEnabled;
    private double _brightness;
    private double _colorTemperature;
    private double _edgeThickness;
    private string _secondaryColorHex;
    private string? _selectedMonitorDeviceName;
    private bool _launchAtStartup;
    private bool _excludeFromCapture;

    public SettingsViewModel(
        AppSettings settings,
        DisplayService displayService,
        Action<AppSettings> applySettings,
        Action<AppSettings> persistSettings)
    {
        _applySettings = applySettings;
        _persistSettings = persistSettings;
        Displays = new ObservableCollection<DisplayInfo>(displayService.GetDisplays());

        _isEnabled = settings.IsEnabled;
        _brightness = settings.Brightness;
        _colorTemperature = settings.ColorTemperature;
        _edgeThickness = settings.EdgeThickness;
        _secondaryColorHex = NormalizeSecondaryColorHex(settings.SecondaryColorHex);
        _selectedMonitorDeviceName = displayService.GetSelectedDisplay(settings.MonitorDeviceName).DeviceName;
        _launchAtStartup = settings.LaunchAtStartup;
        _excludeFromCapture = settings.ExcludeFromCapture;
        _pendingSettings = BuildSettings();

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _saveTimer.Tick += OnSaveTimerTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DisplayInfo> Displays { get; }

    public string HotkeyLabel => "Ctrl+Shift+L toggles the light instantly.";

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetField(ref _isEnabled, value))
            {
                NotifyLivePreviewChanged();
            }
        }
    }

    public double Brightness
    {
        get => _brightness;
        set
        {
            if (SetField(ref _brightness, value))
            {
                NotifyLivePreviewChanged();
            }
        }
    }

    public double ColorTemperature
    {
        get => _colorTemperature;
        set
        {
            if (SetField(ref _colorTemperature, value))
            {
                NotifyLivePreviewChanged();
            }
        }
    }

    public double EdgeThickness
    {
        get => _edgeThickness;
        set
        {
            if (SetField(ref _edgeThickness, value))
            {
                NotifyLivePreviewChanged();
            }
        }
    }

    public string SecondaryColorHex
    {
        get => _secondaryColorHex;
        set
        {
            var normalized = NormalizeSecondaryColorHex(value);
            if (SetField(ref _secondaryColorHex, normalized))
            {
                OnPropertyChanged(nameof(SecondaryColorPreview));
                NotifyLivePreviewChanged();
            }
        }
    }

    public Brush SecondaryColorPreview => new SolidColorBrush(ParseColor(SecondaryColorHex));

    public Brush LivePreviewOuterBrush
    {
        get
        {
            var edgeColor = CreateStrokeColor(ColorTemperature, Brightness);
            var coreColor = CreateCoreColor(Brightness);
            var secondaryColor = CreateSecondaryColor(SecondaryColorHex, Brightness);
            return CreateColorStrokeBrush(edgeColor, secondaryColor, coreColor);
        }
    }

    public Brush LivePreviewInnerBrush
    {
        get
        {
            var edgeColor = CreateStrokeColor(ColorTemperature, Brightness);
            var coreColor = CreateCoreColor(Brightness);
            var secondaryColor = CreateSecondaryColor(SecondaryColorHex, Brightness);
            return CreateWhiteStrokeBrush(edgeColor, secondaryColor, coreColor);
        }
    }

    public Color LivePreviewOuterGlowColor
    {
        get
        {
            var edgeColor = CreateStrokeColor(ColorTemperature, Brightness);
            var secondaryColor = CreateSecondaryColor(SecondaryColorHex, Brightness);
            return BlendColor(edgeColor, secondaryColor, 0.35);
        }
    }

    public Color LivePreviewInnerGlowColor
    {
        get
        {
            var coreColor = CreateCoreColor(Brightness);
            var secondaryColor = CreateSecondaryColor(SecondaryColorHex, Brightness);
            return BlendColor(coreColor, secondaryColor, 0.12);
        }
    }

    public Thickness LivePreviewOuterBorderThickness => new(GetLivePreviewOuterThickness());

    public Thickness LivePreviewInnerBorderThickness => new(Math.Max(2.5d, GetLivePreviewOuterThickness() * 0.58d));

    public double LivePreviewFrameOpacity => IsEnabled ? 1d : 0.35d;

    public string LivePreviewStatusLabel => IsEnabled ? "Overlay reacting live" : "Overlay paused";

    public string? SelectedMonitorDeviceName
    {
        get => _selectedMonitorDeviceName;
        set => SetField(ref _selectedMonitorDeviceName, value);
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set => SetField(ref _launchAtStartup, value);
    }

    public bool ExcludeFromCapture
    {
        get => _excludeFromCapture;
        set => SetField(ref _excludeFromCapture, value);
    }

    public void SetEnabledFromExternal(bool value)
    {
        IsEnabled = value;
    }

    public void SetSecondaryColor(string colorHex)
    {
        SecondaryColorHex = colorHex;
    }

    private void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        _persistSettings(_pendingSettings.Clone());
    }

    private AppSettings BuildSettings()
    {
        return new AppSettings
        {
            IsEnabled = IsEnabled,
            Brightness = Brightness,
            ColorTemperature = ColorTemperature,
            EdgeThickness = EdgeThickness,
            SecondaryColorHex = SecondaryColorHex,
            MonitorDeviceName = SelectedMonitorDeviceName,
            LaunchAtStartup = LaunchAtStartup,
            ExcludeFromCapture = ExcludeFromCapture
        };
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnSettingsChanged();
        return true;
    }

    private void OnSettingsChanged()
    {
        _pendingSettings = BuildSettings();
        _applySettings(_pendingSettings.Clone());
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void NotifyLivePreviewChanged()
    {
        OnPropertyChanged(nameof(LivePreviewOuterBrush));
        OnPropertyChanged(nameof(LivePreviewInnerBrush));
        OnPropertyChanged(nameof(LivePreviewOuterGlowColor));
        OnPropertyChanged(nameof(LivePreviewInnerGlowColor));
        OnPropertyChanged(nameof(LivePreviewOuterBorderThickness));
        OnPropertyChanged(nameof(LivePreviewInnerBorderThickness));
        OnPropertyChanged(nameof(LivePreviewFrameOpacity));
        OnPropertyChanged(nameof(LivePreviewStatusLabel));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string NormalizeSecondaryColorHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AppSettings.DefaultSecondaryColorHex;
        }

        return $"#{ParseColor(value).R:X2}{ParseColor(value).G:X2}{ParseColor(value).B:X2}";
    }

    private static Color ParseColor(string value)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color color)
            {
                return Color.FromRgb(color.R, color.G, color.B);
            }
        }
        catch
        {
        }

        return (Color)ColorConverter.ConvertFromString(AppSettings.DefaultSecondaryColorHex)!;
    }

    private double GetLivePreviewOuterThickness()
    {
        var normalized = (Math.Clamp(EdgeThickness, 24d, 260d) - 24d) / (260d - 24d);
        return 4d + normalized * 5d;
    }

    private static Color CreateStrokeColor(double colorTemperature, double brightness)
    {
        var intensity = 0.55 + (Math.Clamp(brightness, 0d, 100d) / 100d * 0.45);
        return ScaleColor(TemperatureToColor(colorTemperature), intensity);
    }

    private static Color CreateCoreColor(double brightness)
    {
        var intensity = 0.9 + (Math.Clamp(brightness, 0d, 100d) / 100d * 0.1);
        var channel = (byte)Math.Clamp(Math.Round(255d * intensity), 0d, 255d);
        return Color.FromArgb(255, channel, channel, channel);
    }

    private static Color CreateSecondaryColor(string? colorHex, double brightness)
    {
        var intensity = 0.55 + (Math.Clamp(brightness, 0d, 100d) / 100d * 0.45);
        return ScaleColor(ParseColor(colorHex ?? AppSettings.DefaultSecondaryColorHex), intensity);
    }

    private static Color TemperatureToColor(double kelvin)
    {
        var temperature = kelvin / 100d;

        double red;
        double green;
        double blue;

        if (temperature <= 66)
        {
            red = 255;
            green = 99.4708025861 * Math.Log(temperature) - 161.1195681661;
            blue = temperature <= 19 ? 0 : 138.5177312231 * Math.Log(temperature - 10) - 305.0447927307;
        }
        else
        {
            red = 329.698727446 * Math.Pow(temperature - 60, -0.1332047592);
            green = 288.1221695283 * Math.Pow(temperature - 60, -0.0755148492);
            blue = 255;
        }

        return Color.FromArgb(
            255,
            (byte)Math.Clamp(red, 0, 255),
            (byte)Math.Clamp(green, 0, 255),
            (byte)Math.Clamp(blue, 0, 255));
    }

    private static Color ScaleColor(Color color, double intensity)
    {
        intensity = Math.Clamp(intensity, 0d, 1d);

        return Color.FromArgb(
            255,
            (byte)Math.Clamp(Math.Round(color.R * intensity), 0d, 255d),
            (byte)Math.Clamp(Math.Round(color.G * intensity), 0d, 255d),
            (byte)Math.Clamp(Math.Round(color.B * intensity), 0d, 255d));
    }

    private static Brush CreateColorStrokeBrush(Color edgeColor, Color secondaryColor, Color coreColor)
    {
        var hotHighlight = LightenColor(edgeColor, 0.35);
        var saturatedMid = BlendColor(edgeColor, secondaryColor, 0.5);
        var deepEdge = DarkenColor(edgeColor, 0.18);
        var electricAccent = LightenColor(secondaryColor, 0.18);

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new(hotHighlight, 0),
                new(saturatedMid, 0.18),
                new(electricAccent, 0.4),
                new(edgeColor, 0.58),
                new(deepEdge, 0.8),
                new(hotHighlight, 1)
            }
        };
    }

    private static Brush CreateWhiteStrokeBrush(Color edgeColor, Color secondaryColor, Color coreColor)
    {
        var tintedWhite = BlendColor(coreColor, secondaryColor, 0.08);
        var brightWhite = LightenColor(coreColor, 0.08);
        var warmWhite = BlendColor(brightWhite, edgeColor, 0.06);

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new(tintedWhite, 0),
                new(brightWhite, 0.24),
                new(coreColor, 0.52),
                new(warmWhite, 0.78),
                new(tintedWhite, 1)
            }
        };
    }

    private static Color LightenColor(Color color, double amount)
    {
        return BlendColor(color, Colors.White, amount);
    }

    private static Color DarkenColor(Color color, double amount)
    {
        return BlendColor(color, Colors.Black, amount);
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        var inverse = 1d - amount;

        return Color.FromArgb(
            255,
            (byte)Math.Clamp(Math.Round(from.R * inverse + to.R * amount), 0d, 255d),
            (byte)Math.Clamp(Math.Round(from.G * inverse + to.G * amount), 0d, 255d),
            (byte)Math.Clamp(Math.Round(from.B * inverse + to.B * amount), 0d, 255d));
    }
}
