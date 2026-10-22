using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinTaskbar.Native
{
    public static class TaskbarHelper
    {
        public struct TaskbarInfo
        {
            public NativeMethods.RECT Rect;
            public TaskbarEdge Edge;
            public bool IsAutoHide;
        }

        public enum TaskbarEdge
        {
            Left = 0,
            Top = 1,
            Right = 2,
            Bottom = 3
        }

        public static TaskbarInfo GetTaskbarInfo()
        {
            var data = new NativeMethods.APPBARDATA
            {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.APPBARDATA))
            };

            IntPtr result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data);
            if (result != IntPtr.Zero)
            {
                return new TaskbarInfo
                {
                    Rect = data.rc,
                    Edge = (TaskbarEdge)data.uEdge,
                    IsAutoHide = false
                };
            }

            // Fallback: Query Shell_TrayWnd directly
            IntPtr trayHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (trayHwnd != IntPtr.Zero && NativeMethods.GetWindowRect(trayHwnd, out var rect))
            {
                return new TaskbarInfo
                {
                    Rect = rect,
                    Edge = TaskbarEdge.Bottom,
                    IsAutoHide = false
                };
            }

            return new TaskbarInfo
            {
                Rect = new NativeMethods.RECT
                {
                    Left = 0,
                    Top = (int)SystemParameters.PrimaryScreenHeight - 48,
                    Right = (int)SystemParameters.PrimaryScreenWidth,
                    Bottom = (int)SystemParameters.PrimaryScreenHeight
                },
                Edge = TaskbarEdge.Bottom,
                IsAutoHide = false
            };
        }

        public static void ApplyWindows11Styling(Window window, bool roundCorners = true)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.EnsureHandle();

            // Force Immersive Dark Mode in Win11
            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Force rounded corners
            if (roundCorners)
            {
                int cornerPreference = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
            }
        }

        public static void SetToolWindow(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.EnsureHandle();

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }
    }
}
