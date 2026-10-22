using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WinTaskbar.Models;
using WinTaskbar.Native;

namespace WinTaskbar.Services
{
    public class WindowService : IWindowService
    {
        private static readonly HashSet<string> ExcludedTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Program Manager", "Settings", "Microsoft Text Input Application",
            "WinTaskbar Overlay", "WinTaskbar Dock", "WinTaskbar",
            "Windows Shell Experience Host", "Controles de Mídia", "Menu Iniciar"
        };

        private static readonly HashSet<string> ExcludedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
            "TextInputHost", "SystemSettingsBroker", "dwm", "conhost", "svchost",
            "WidgetService", "WinTaskbar", "WinTaskbar_LumaBar", "WinTaskbar_AntiOverlap"
        };

        public IReadOnlyList<DockAppItem> GetOpenWindows()
        {
            var appMap = new Dictionary<string, DockAppItem>(StringComparer.OrdinalIgnoreCase);
            IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd))
                    return true;

                // Check dimensions: skip invisible/zero-size helper windows
                if (!NativeMethods.GetWindowRect(hWnd, out var rect) || rect.Width < 50 || rect.Height < 50)
                    return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                int exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
                bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;

                if (isToolWindow && !isAppWindow)
                    return true;

                // Only top-level windows without owner, unless marked as WS_EX_APPWINDOW
                IntPtr owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
                if (owner != IntPtr.Zero && !isAppWindow)
                    return true;

                var sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();

                if (string.IsNullOrWhiteSpace(title) || ExcludedTitles.Contains(title))
                    return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0)
                    return true;

                string procName = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName;
                }
                catch { }

                if (string.IsNullOrEmpty(procName) || ExcludedProcesses.Contains(procName))
                    return true;

                if (string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase) &&
                    (title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase) ||
                     title.Equals("Windows Shell Experience Host", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                string exePath = GetProcessPath((int)pid);

                // Extract Icon: check Window class/hIcon first, then executable file
                var icon = IconHelper.GetIconForWindow(hWnd) ??
                           (!string.IsNullOrEmpty(exePath) ? IconHelper.GetIconForFile(exePath) : null);

                // If no icon could be resolved, do NOT show as generic mystery box
                if (icon == null)
                    return true;

                // Detect notification count from window title e.g. "(3) WhatsApp", "[1] Teams"
                int badge = 0;
                var badgeMatch = Regex.Match(title, @"^[\(\[](\d+)[\)\]]");
                if (badgeMatch.Success && int.TryParse(badgeMatch.Groups[1].Value, out int count))
                {
                    badge = count;
                }

                // Group by application key (either exePath or procName)
                string groupKey = !string.IsNullOrEmpty(exePath) ? exePath.ToLowerInvariant() : procName.ToLowerInvariant();

                if (!appMap.TryGetValue(groupKey, out var appItem))
                {
                    string displayName = FriendlyName(procName, title);

                    appItem = new DockAppItem
                    {
                        Hwnd = hWnd,
                        ProcessId = (int)pid,
                        Title = title,
                        Name = displayName,
                        ExecutablePath = exePath,
                        Icon = icon,
                        IsRunning = true,
                        IsActive = (hWnd == foregroundHwnd),
                        BadgeCount = badge
                    };
                    appItem.OpenHwnds.Add(hWnd);
                    appMap[groupKey] = appItem;
                }
                else
                {
                    // Existing app instance: add window handle
                    if (!appItem.OpenHwnds.Contains(hWnd))
                    {
                        appItem.OpenHwnds.Add(hWnd);
                    }
                    if (hWnd == foregroundHwnd)
                    {
                        appItem.IsActive = true;
                        appItem.Hwnd = hWnd;
                        appItem.Title = title;
                    }
                    appItem.BadgeCount = Math.Max(appItem.BadgeCount, badge);
                }

                return true;
            }, IntPtr.Zero);

            return appMap.Values.ToList();
        }

        private static string FriendlyName(string procName, string title)
        {
            if (string.Equals(procName, "msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
            if (string.Equals(procName, "chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
            if (string.Equals(procName, "whatsapp", StringComparison.OrdinalIgnoreCase)) return "WhatsApp";
            if (string.Equals(procName, "teams", StringComparison.OrdinalIgnoreCase) || string.Equals(procName, "ms-teams", StringComparison.OrdinalIgnoreCase)) return "Microsoft Teams";
            if (string.Equals(procName, "code", StringComparison.OrdinalIgnoreCase)) return "Visual Studio Code";
            if (string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase)) return "Explorador de Arquivos";
            if (string.Equals(procName, "spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
            if (string.Equals(procName, "windowsterminal", StringComparison.OrdinalIgnoreCase)) return "Terminal";

            return !string.IsNullOrWhiteSpace(procName) ? procName : title;
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

        public void ToggleApp(DockAppItem app)
        {
            if (app.OpenHwnds.Count == 0)
            {
                if (app.Hwnd != IntPtr.Zero)
                {
                    ToggleWindow(app.Hwnd);
                }
                return;
            }

            IntPtr fg = NativeMethods.GetForegroundWindow();
            int focusedIdx = app.OpenHwnds.IndexOf(fg);

            if (focusedIdx >= 0)
            {
                // App is currently focused
                if (app.OpenHwnds.Count > 1)
                {
                    // Cycle to the next window of this app (macOS style)
                    int nextIdx = (focusedIdx + 1) % app.OpenHwnds.Count;
                    ActivateWindow(app.OpenHwnds[nextIdx]);
                }
                else
                {
                    // Single window: minimize
                    MinimizeWindow(app.OpenHwnds[0]);
                }
            }
            else
            {
                // App is not focused: bring primary/first window forward
                ActivateWindow(app.OpenHwnds[0]);
            }
        }

        public void CloseWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            NativeMethods.PostMessage(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
