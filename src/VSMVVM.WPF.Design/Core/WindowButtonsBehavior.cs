using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace VSMVVM.WPF.Design.Core
{
    /// <summary>
    /// Window 커스텀 크롬의 최소화/최대화/닫기 버튼 동작을 CommandBinding으로 자동 연결하고,
    /// WindowStyle="None" + AllowsTransparency="True" 사용 시 최대화 영역을
    /// Work Area(작업 표시줄 제외)로 제한하는 Attached Behavior.
    /// </summary>
    public static class WindowButtonsBehavior
    {
        #region Attached Property

        public static readonly DependencyProperty EnableProperty =
            DependencyProperty.RegisterAttached(
                "Enable",
                typeof(bool),
                typeof(WindowButtonsBehavior),
                new PropertyMetadata(false, OnEnableChanged));

        public static bool GetEnable(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableProperty);
        }

        public static void SetEnable(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableProperty, value);
        }

        #endregion

        #region Private Methods

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                window.CommandBindings.Add(new CommandBinding(
                    SystemCommands.MinimizeWindowCommand,
                    (s, args) => { SystemCommands.MinimizeWindow(window); }));

                window.CommandBindings.Add(new CommandBinding(
                    SystemCommands.MaximizeWindowCommand,
                    (s, args) =>
                    {
                        if (window.WindowState == WindowState.Maximized)
                        {
                            SystemCommands.RestoreWindow(window);
                        }
                        else
                        {
                            SystemCommands.MaximizeWindow(window);
                        }
                    }));

                window.CommandBindings.Add(new CommandBinding(
                    SystemCommands.RestoreWindowCommand,
                    (s, args) => { SystemCommands.RestoreWindow(window); }));

                window.CommandBindings.Add(new CommandBinding(
                    SystemCommands.CloseWindowCommand,
                    (s, args) => { SystemCommands.CloseWindow(window); }));

                // WM_GETMINMAXINFO 처리: 최대화 시 Work Area 크기로 제한
                window.SourceInitialized += (s, args) =>
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
                };
            }
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;

            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        var workArea = monitorInfo.rcWork;
                        var monitorArea = monitorInfo.rcMonitor;

                        mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
                        mmi.ptMaxPosition.Y = workArea.Top - monitorArea.Top;
                        mmi.ptMaxSize.X = workArea.Right - workArea.Left;
                        mmi.ptMaxSize.Y = workArea.Bottom - workArea.Top;
                    }
                }

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }

            return IntPtr.Zero;
        }

        #endregion

        #region Win32 Interop

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        #endregion
    }
}
