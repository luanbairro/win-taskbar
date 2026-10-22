using System.Collections.Generic;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface IAppLauncherService
    {
        IReadOnlyList<StartMenuAppItem> GetAllApps();
        IReadOnlyList<StartMenuAppItem> SearchApps(string query);
        IReadOnlyList<DockAppItem> GetPinnedApps();
        void PinApp(string name, string targetPath);
        void UnpinApp(string targetPath);
        void Launch(string targetPath, string? arguments = null);
        void Shutdown();
        void Restart();
        void Lock();
        void Sleep();
    }
}
