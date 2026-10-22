using System;
using System.Collections.Generic;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface IWindowService
    {
        IReadOnlyList<DockAppItem> GetOpenWindows();
        void ActivateWindow(IntPtr hWnd);
        void MinimizeWindow(IntPtr hWnd);
        void ToggleWindow(IntPtr hWnd);
        void ToggleApp(DockAppItem app);
        void CloseWindow(IntPtr hWnd);
    }
}
