using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace EditorWpf.Controls;

public sealed class GameHostControl : HwndHost
{
    private IntPtr _hostHwnd = IntPtr.Zero;
    private Process? _process;
    private string? _lastArgs;
    private IntPtr _gameHwnd = IntPtr.Zero;

    public void StartGame(string exePath, string? workingDir = null, string? args = null, bool restart = false)
    {
        if (_process is not null && !_process.HasExited)
        {
            if (!restart && string.Equals(_lastArgs, args, StringComparison.OrdinalIgnoreCase))
                return;
            StopGame();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = workingDir ?? System.IO.Path.GetDirectoryName(exePath) ?? string.Empty,
            UseShellExecute = true,
            Arguments = args ?? string.Empty
        };

        _process = Process.Start(startInfo);
        if (_process is null)
            return;
        _lastArgs = args ?? string.Empty;

        _process.WaitForInputIdle(3000);
        _gameHwnd = WaitForMainWindow(_process, 5000);
        if (_gameHwnd == IntPtr.Zero || _hostHwnd == IntPtr.Zero)
            return;

        SetParent(_gameHwnd, _hostHwnd);

        var style = GetWindowLong(_gameHwnd, GWL_STYLE);
        style &= ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        style |= WS_CHILD;
        SetWindowLong(_gameHwnd, GWL_STYLE, style);

        SetWindowPos(_gameHwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);

        ShowWindow(_gameHwnd, SW_SHOW);
        ResizeHostedWindow();
    }

    public void StopGame()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _process.CloseMainWindow();
                if (!_process.WaitForExit(1500))
                    _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _gameHwnd = IntPtr.Zero;
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        if (_gameHwnd == IntPtr.Zero)
            return;

        EnableWindow(_gameHwnd, enabled);
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hostHwnd = CreateWindowEx(
            0, "static", "",
            WS_CHILD | WS_VISIBLE,
            0, 0, 0, 0,
            hwndParent.Handle,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        return new HandleRef(this, _hostHwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        StopGame();
        if (hwnd.Handle != IntPtr.Zero)
            DestroyWindow(hwnd.Handle);
        _hostHwnd = IntPtr.Zero;
    }

    protected override void OnWindowPositionChanged(System.Windows.Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        ResizeHostedWindow();
    }

    protected override void OnRenderSizeChanged(System.Windows.SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ResizeHostedWindow();
    }

    private void ResizeHostedWindow()
    {
        if (_gameHwnd == IntPtr.Zero || _hostHwnd == IntPtr.Zero)
            return;

        var (width, height) = GetHostPixelSize();
        MoveWindow(_gameHwnd, 0, 0, width, height, true);
    }

    private (int width, int height) GetHostPixelSize()
    {
        var width = ActualWidth;
        var height = ActualHeight;

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is not null)
        {
            var m = source.CompositionTarget.TransformToDevice;
            width *= m.M11;
            height *= m.M22;
        }

        return (Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height)));
    }

    private static IntPtr WaitForMainWindow(Process process, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
                return process.MainWindowHandle;
            Thread.Sleep(50);
        }
        return IntPtr.Zero;
    }

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;
    private const int SW_SHOW = 5;
    private const int SWP_NOSIZE = 0x0001;
    private const int SWP_NOMOVE = 0x0002;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        int exStyle,
        string className,
        string windowName,
        int style,
        int x, int y, int width, int height,
        IntPtr hwndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        int uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);
}
