using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using HaloLight.Models;
using HaloLight.ViewModels;
using WinForms = System.Windows.Forms;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace HaloLight.Views;

public partial class SettingsWindow : Window
{
    private bool _isSyncingColorEditor;
    private SettingsViewModel? _trackedViewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyVerticalScreenFit();

        // Update slider track fills when values change
        UpdateSliderStyles();
        AttachToViewModel(DataContext as SettingsViewModel);
        SyncSecondaryColorEditor();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachToViewModel(e.NewValue as SettingsViewModel);
        SyncSecondaryColorEditor();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        AttachToViewModel(null);
    }

    private void ApplyVerticalScreenFit()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var workingArea = WinForms.Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);

        var workingLeft = workingArea.Left / dpi.DpiScaleX;
        var workingTop = workingArea.Top / dpi.DpiScaleY;
        var workingWidth = workingArea.Width / dpi.DpiScaleX;
        var workingHeight = workingArea.Height / dpi.DpiScaleY;

        MaxHeight = workingHeight;
        MinHeight = Math.Min(MinHeight, workingHeight);
        Height = workingHeight;
        Top = workingTop;
        Left = workingLeft + Math.Max(0, (workingWidth - Width) / 2);
    }

    private void UpdateSliderStyles()
    {
        // Find all sliders and update their track fill
        var sliders = FindVisualChildren<Slider>(this);
        foreach (var slider in sliders)
        {
            slider.ValueChanged += (s, ev) => UpdateSliderTrackFill(slider);
            UpdateSliderTrackFill(slider);
        }
    }

    private void UpdateSliderTrackFill(Slider slider)
    {
        if (slider.Template.FindName("TrackFill", slider) is Border trackFill)
        {
            var percent = (slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum);
            trackFill.Width = slider.ActualWidth * percent;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T typedChild) yield return typedChild;
            
            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }

    public event EventHandler? HideRequested;

    private void HideButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    private void GitHubLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }

        e.Handled = true;
    }

    private void OpenSecondaryColorModal_OnClick(object sender, RoutedEventArgs e)
    {
        SecondaryColorModal.Visibility = Visibility.Visible;
        SyncSecondaryColorEditor();

        Dispatcher.BeginInvoke(() =>
        {
            UpdateSecondaryColorSliderTracks();
            SecondaryColorHexInput.Focus();
            SecondaryColorHexInput.SelectAll();
        });
    }

    private void CloseSecondaryColorModal_OnClick(object sender, RoutedEventArgs e)
    {
        SecondaryColorModal.Visibility = Visibility.Collapsed;
    }

    private void SecondaryColorModalBackdrop_OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, SecondaryColorModal))
        {
            SecondaryColorModal.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SecondaryColorModal.Visibility == Visibility.Visible)
        {
            SecondaryColorModal.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void AttachToViewModel(SettingsViewModel? viewModel)
    {
        if (ReferenceEquals(_trackedViewModel, viewModel))
        {
            return;
        }

        if (_trackedViewModel is not null)
        {
            _trackedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _trackedViewModel = viewModel;

        if (_trackedViewModel is not null)
        {
            _trackedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SecondaryColorHex))
        {
            Dispatcher.Invoke(SyncSecondaryColorEditor);
        }
    }

    private void SecondaryColorPreset_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string hexColor })
        {
            ApplySecondaryColor(hexColor);
        }
    }

    private void SecondaryColorReset_OnClick(object sender, RoutedEventArgs e)
    {
        ApplySecondaryColor(AppSettings.DefaultSecondaryColorHex);
    }

    private void SecondaryColorChannel_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSyncingColorEditor || DataContext is not SettingsViewModel)
        {
            return;
        }

        var color = Color.FromRgb(
            (byte)Math.Round(SecondaryColorRedSlider.Value),
            (byte)Math.Round(SecondaryColorGreenSlider.Value),
            (byte)Math.Round(SecondaryColorBlueSlider.Value));

        ApplySecondaryColor(color);
    }

    private void SecondaryColorHexApply_OnClick(object sender, RoutedEventArgs e)
    {
        ApplySecondaryColorFromInput();
    }

    private void SecondaryColorHexInput_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplySecondaryColorFromInput();
            e.Handled = true;
        }
    }

    private void ApplySecondaryColorFromInput()
    {
        if (TryParseColor(SecondaryColorHexInput.Text, out var color))
        {
            ApplySecondaryColor(color);
            return;
        }

        SyncSecondaryColorEditor();
    }

    private void ApplySecondaryColor(Color color)
    {
        ApplySecondaryColor($"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private void ApplySecondaryColor(string hexColor)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        viewModel.SetSecondaryColor(hexColor);
        SyncSecondaryColorEditor();
    }

    private void SyncSecondaryColorEditor()
    {
        if (!IsLoaded || DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var color = ParseColor(viewModel.SecondaryColorHex);

        _isSyncingColorEditor = true;
        SecondaryColorRedSlider.Value = color.R;
        SecondaryColorGreenSlider.Value = color.G;
        SecondaryColorBlueSlider.Value = color.B;
        SecondaryColorHexInput.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        _isSyncingColorEditor = false;
        UpdateSecondaryColorSliderTracks();
    }

    private void UpdateSecondaryColorSliderTracks()
    {
        UpdateSliderTrackFill(SecondaryColorRedSlider);
        UpdateSliderTrackFill(SecondaryColorGreenSlider);
        UpdateSliderTrackFill(SecondaryColorBlueSlider);
    }

    private static Color ParseColor(string? value)
    {
        return TryParseColor(value, out var color)
            ? color
            : (Color)ColorConverter.ConvertFromString(AppSettings.DefaultSecondaryColorHex)!;
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var normalizedValue = value.Trim();

                if (normalizedValue.Length == 6 && normalizedValue.All(Uri.IsHexDigit))
                {
                    normalizedValue = $"#{normalizedValue}";
                }

                if (ColorConverter.ConvertFromString(normalizedValue) is Color parsedColor)
                {
                    color = Color.FromRgb(parsedColor.R, parsedColor.G, parsedColor.B);
                    return true;
                }
            }
        }
        catch
        {
        }

        color = default;
        return false;
    }
}
