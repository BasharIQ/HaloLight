using System.Windows;
using System.Windows.Interop;
using HaloLight.Interop;

namespace HaloLight.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;

    private readonly Action _callback;
    private HwndSource? _source;
    private IntPtr _handle;
    private bool _registered;

    public HotkeyService(Action callback)
    {
        _callback = callback;
    }

    public void Attach(Window window)
    {
        window.SourceInitialized += OnSourceInitialized;
        window.Closed += OnWindowClosed;

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Register(window);
        }
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        if (_registered && _handle != IntPtr.Zero)
        {
            _ = NativeMethods.UnregisterHotKey(_handle, HotkeyId);
        }

        _registered = false;
        _handle = IntPtr.Zero;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            Register(window);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Dispose();
    }

    private void Register(Window window)
    {
        if (_registered)
        {
            return;
        }

        _handle = new WindowInteropHelper(window).Handle;
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(WndProc);
        _registered = NativeMethods.RegisterHotKey(_handle, HotkeyId, NativeMethods.ModControl | NativeMethods.ModShift, NativeMethods.VkL);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _callback();
            handled = true;
        }

        return IntPtr.Zero;
    }
}
