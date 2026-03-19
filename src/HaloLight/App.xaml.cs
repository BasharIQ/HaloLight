using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using HaloLight.Models;
using HaloLight.Services;
using HaloLight.ViewModels;
using HaloLight.Views;

namespace HaloLight;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private SettingsStore? _settingsStore;
    private StartupService? _startupService;
    private OverlayService? _overlayService;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private SettingsWindow? _settingsWindow;
    private AppSettings _currentSettings = new();
    private bool _allowExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "HaloLight.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _settingsStore = new SettingsStore();
        _startupService = new StartupService();

        var displayService = new DisplayService();
        _overlayService = new OverlayService(displayService);
        _currentSettings = _settingsStore.Load();

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closing += OnSettingsWindowClosing;
        _settingsWindow.HideRequested += OnHideRequested;

        var viewModel = new SettingsViewModel(_currentSettings, displayService, ApplySettings, PersistSettings);
        _settingsWindow.DataContext = viewModel;
        MainWindow = _settingsWindow;

        _hotkeyService = new HotkeyService(ToggleOverlay);
        _hotkeyService.Attach(_settingsWindow);

        _overlayService.Apply(_currentSettings);
        _startupService.Apply(_currentSettings.LaunchAtStartup);

        _trayService = new TrayService(ToggleOverlay, ShowSettingsWindow, ExitApplication);
        _trayService.UpdateEnabledState(_currentSettings.IsEnabled);

        if (e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
        {
            _ = new WindowInteropHelper(_settingsWindow).EnsureHandle();
        }
        else
        {
            ShowSettingsWindow();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settingsStore?.Save(_currentSettings);
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        _overlayService?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void OnHideRequested(object? sender, EventArgs e)
    {
        _settingsWindow?.Hide();
    }

    private void OnSettingsWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        _settingsWindow?.Hide();
    }

    private void ApplySettings(AppSettings settings)
    {
        _currentSettings = settings.Clone();
        _overlayService?.Apply(_currentSettings);
        _startupService?.Apply(_currentSettings.LaunchAtStartup);
        _trayService?.UpdateEnabledState(_currentSettings.IsEnabled);
    }

    private void PersistSettings(AppSettings settings)
    {
        _settingsStore?.Save(settings);
    }

    private void ToggleOverlay()
    {
        if (_settingsWindow?.DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        viewModel.SetEnabledFromExternal(!viewModel.IsEnabled);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Shutdown();
    }
}

