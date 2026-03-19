using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Effects;
using HaloLight.Interop;
using HaloLight.Models;

using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace HaloLight.Views;

public partial class OverlayWindow : Window
{
    private static readonly Duration FadeDuration = TimeSpan.FromMilliseconds(160);
    private AppSettings _settings = new();
    private DisplayInfo? _display;
    private bool _sourceReady;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void ApplySettings(AppSettings settings, DisplayInfo display)
    {
        _settings = settings.Clone();
        _display = display;

        ApplyEdgeLayout(_settings.EdgeThickness);
        ApplyEdgeBrushes(_settings.ColorTemperature, _settings.Brightness, _settings.SecondaryColorHex);

        if (_sourceReady)
        {
            ApplyNativeWindowState();
        }

        if (IsVisible && _settings.IsEnabled)
        {
            PulseGlow();
        }
    }

    public void ShowAnimated()
    {
        BeginAnimation(OpacityProperty, null);

        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }

        BeginAnimation(OpacityProperty, CreateOpacityAnimation(1));
    }

    public void HideAnimated()
    {
        if (!IsVisible)
        {
            return;
        }

        var animation = CreateOpacityAnimation(0);
        animation.Completed += (_, _) =>
        {
            if (Opacity <= 0.01)
            {
                Hide();
            }
        };

        BeginAnimation(OpacityProperty, animation);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _sourceReady = true;
        ApplyNativeWindowState();
    }

    private void ApplyEdgeLayout(double thickness)
    {
        var outerThickness = Math.Max(24d, thickness);
        var innerThickness = Math.Max(12d, outerThickness * 0.72);
        var margin = outerThickness / 2d;
        var radius = Math.Max(36d, outerThickness * 1.6);

        ColorRing.Margin = new Thickness(margin);
        ColorRing.StrokeThickness = outerThickness;
        ColorRing.RadiusX = radius;
        ColorRing.RadiusY = radius;

        WhiteRing.Margin = new Thickness(margin);
        WhiteRing.StrokeThickness = innerThickness;
        WhiteRing.RadiusX = radius;
        WhiteRing.RadiusY = radius;
    }

    private void ApplyEdgeBrushes(double colorTemperature, double brightness, string? secondaryColorHex)
    {
        var edgeColor = CreateStrokeColor(colorTemperature, brightness);
        var coreColor = CreateCoreColor(brightness);
        var secondaryColor = CreateSecondaryColor(secondaryColorHex, brightness);

        ColorRing.Stroke = CreateColorStrokeBrush(edgeColor, secondaryColor, coreColor);
        WhiteRing.Stroke = CreateWhiteStrokeBrush(edgeColor, secondaryColor, coreColor);

        var baseThickness = ColorRing.StrokeThickness;
        ColorRing.Effect = CreateGlowEffect(BlendColor(edgeColor, secondaryColor, 0.35), Math.Max(16d, baseThickness * 0.42), 0.92);
        WhiteRing.Effect = CreateGlowEffect(BlendColor(coreColor, secondaryColor, 0.12), Math.Max(10d, baseThickness * 0.24), 0.74);
    }

    private void ApplyNativeWindowState()
    {
        if (_display is null)
        {
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        WindowStyleHelper.ApplyOverlayStyles(handle);
        WindowStyleHelper.PositionOverlay(handle, _display.Bounds);
        WindowStyleHelper.ApplyCaptureExclusion(handle, _settings.ExcludeFromCapture);
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
        return ScaleColor(ParseColor(colorHex, AppSettings.DefaultSecondaryColorHex), intensity);
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

    private static Effect CreateGlowEffect(Color color, double blurRadius, double opacity)
    {
        return new DropShadowEffect
        {
            BlurRadius = blurRadius,
            Color = color,
            Opacity = opacity,
            ShadowDepth = 0,
            RenderingBias = RenderingBias.Performance
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

    private static Color ParseColor(string? value, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorConverter.ConvertFromString(value) is Color color)
            {
                return Color.FromRgb(color.R, color.G, color.B);
            }
        }
        catch
        {
        }

        return (Color)ColorConverter.ConvertFromString(fallback)!;
    }

    private void PulseGlow()
    {
        GlowRoot.BeginAnimation(UIElement.OpacityProperty, null);
        GlowRoot.Opacity = 0.94;
        GlowRoot.BeginAnimation(UIElement.OpacityProperty, CreateOpacityAnimation(1));
    }

    private static DoubleAnimation CreateOpacityAnimation(double to)
    {
        return new DoubleAnimation
        {
            To = to,
            Duration = FadeDuration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
    }
}
