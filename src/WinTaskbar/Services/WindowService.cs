using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using WinTaskbar.Models;
using WinTaskbar.Native;

namespace WinTaskbar.Services
{
    public class WindowService : IWindowService
    {
        private static readonly HashSet<string> ExcludedTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Program Manager", "Settings", "Microsoft Text Input Application",
            "WinTaskbar Overlay", "WinTaskbar Dock", "WinTaskbar"
        };

        public IReadOnlyList<DockAppItem> GetOpenWindows()
        {
            var windows = new List<DockAppItem>();
            IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd))
                    return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                int exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
                bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;

                if (isToolWindow && !isAppWindow)
                    return true;

                var sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();

                if (string.IsNullOrWhiteSpace(title) || ExcludedTitles.Contains(title))
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0)
                    return true;

                string exePath = GetProcessPath((int)pid);
                string procName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName;
                }
                catch { }

                if (string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase) &&
                    (title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) ||
                     title.Equals("Windows Shell Experience Host", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                var icon = IconHelper.GetIconForFile(exePath);

                windows.Add(new DockAppItem
                {
                    Hwnd = hWnd,
                    ProcessId = (int)pid,
                    Title = title,
                    Name = !string.IsNullOrWhiteSpace(procName) ? procName : title,
                    ExecutablePath = exePath,
                    Icon = icon,
                    IsRunning = true,
                    IsActive = (hWnd == foregroundHwnd)
                });

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        private static string GetProcessPath(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                string? fileName = proc.MainModule?.FileName;
                if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                    return fileName;
            }
            catch { }

            try
            {
                IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        {
                            return sb.ToString();
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hProcess);
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        public void ActivateWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            if (NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }
            NativeMethods.SetForegroundWindow(hWnd);
        }

        public void MinimizeWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
        }

        public void ToggleWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == hWnd && !NativeMethods.IsIconic(hWnd))
            {
                MinimizeWindow(hWnd);
            }
            else
            {
                ActivateWindow(hWnd);
            }
        }

        public void CloseWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            NativeMethods.PostMessage(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
