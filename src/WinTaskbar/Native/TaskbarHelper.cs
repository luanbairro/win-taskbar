using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinTaskbar.Native
{
    public static class TaskbarHelper
    {
        private static NativeMethods.RECT? _originalWorkArea;
        private static bool _isWorkAreaReserved;

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

        public static void EnableLiquidGlassBlur(Window window)
        {
            try
            {
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.EnsureHandle();

                var accent = new NativeMethods.AccentPolicy
                {
                    AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    GradientColor = unchecked((int)0x99101622) // Translucent liquid glass tint
                };

                int accentStructSize = Marshal.SizeOf(accent);
                IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new NativeMethods.WindowCompositionAttributeData
                {
                    Attribute = NativeMethods.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = accentStructSize
                };

                NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch { }
        }

        public static void SetToolWindow(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.EnsureHandle();

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }

        public static void EnsureTopMost(Window window)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.EnsureHandle();

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOPMOST) == 0)
            {
                exStyle |= NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TOOLWINDOW;
                NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
            }

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        public static void ReserveWorkArea(int bottomMarginPixels)
        {
            try
            {
                if (!_originalWorkArea.HasValue)
                {
                    var current = new NativeMethods.RECT();
                    if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETWORKAREA, 0, ref current, 0))
                    {
                        _originalWorkArea = current;
                    }
                }

                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

                var newWorkArea = new NativeMethods.RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = screenWidth,
                    Bottom = Math.Max(100, screenHeight - bottomMarginPixels)
                };

                NativeMethods.SystemParametersInfo(
                    NativeMethods.SPI_SETWORKAREA,
                    0,
                    ref newWorkArea,
                    NativeMethods.SPIF_SENDCHANGE | NativeMethods.SPIF_UPDATEINIFILE);

                _isWorkAreaReserved = true;
            }
            catch { }
        }

        public static void RestoreWorkArea()
        {
            try
            {
                if (_isWorkAreaReserved && _originalWorkArea.HasValue)
                {
                    var orig = _originalWorkArea.Value;
                    NativeMethods.SystemParametersInfo(
                        NativeMethods.SPI_SETWORKAREA,
                        0,
                        ref orig,
                        NativeMethods.SPIF_SENDCHANGE | NativeMethods.SPIF_UPDATEINIFILE);

                    _isWorkAreaReserved = false;
                }
            }
            catch { }
        }
    }
}
