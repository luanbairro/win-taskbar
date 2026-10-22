using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using WinTaskbar.Models;
using WinTaskbar.Native;

namespace WinTaskbar.Services
{
    public class AppLauncherService : IAppLauncherService
    {
        private readonly List<StartMenuAppItem> _cachedApps = new();
        private readonly List<DockAppItem> _pinnedApps = new();
        private readonly string _configFilePath;

        public AppLauncherService()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinTaskbar");
            Directory.CreateDirectory(appData);
            _configFilePath = Path.Combine(appData, "dock_pinned.json");

            LoadPinnedApps();
            RefreshInstalledApps();
        }

        public IReadOnlyList<StartMenuAppItem> GetAllApps() => _cachedApps;

        public IReadOnlyList<StartMenuAppItem> SearchApps(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _cachedApps.Take(30).ToList();

            string clean = query.Trim();
            return _cachedApps
                .Where(a => a.Name.Contains(clean, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name.StartsWith(clean, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(a => a.Name)
                .Take(25)
                .ToList();
        }

        public IReadOnlyList<DockAppItem> GetPinnedApps() => _pinnedApps;

        public void PinApp(string name, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return;

            if (_pinnedApps.Any(p => string.Equals(p.ExecutablePath, targetPath, StringComparison.OrdinalIgnoreCase)))
                return;

            _pinnedApps.Add(new DockAppItem
            {
                Name = name,
                ExecutablePath = targetPath,
                IsPinned = true,
                Icon = IconHelper.GetIconForFile(targetPath)
            });

            SavePinnedApps();
        }

        public void UnpinApp(string targetPath)
        {
            int removed = _pinnedApps.RemoveAll(p => string.Equals(p.ExecutablePath, targetPath, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                SavePinnedApps();
            }
        }

        public void Launch(string targetPath, string? arguments = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetPath)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = targetPath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch {targetPath}: {ex.Message}");
            }
        }

        public void Shutdown()
        {
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { UseShellExecute = true, CreateNoWindow = true });
        }

        public void Restart()
        {
            Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { UseShellExecute = true, CreateNoWindow = true });
        }

        public void Lock()
        {
            NativeMethods.LockWorkStation();
        }

        public void Sleep()
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0") { UseShellExecute = true });
        }

        private void RefreshInstalledApps()
        {
            _cachedApps.Clear();
            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add standard Windows apps
            AddKnownApp("Explorador de Arquivos", "explorer.exe", addedNames);
            AddKnownApp("Terminal do Windows", "wt.exe", addedNames);
            AddKnownApp("Bloco de Notas", "notepad.exe", addedNames);
            AddKnownApp("Calculadora", "calc.exe", addedNames);
            AddKnownApp("Gerenciador de Tarefas", "taskmgr.exe", addedNames);
            AddKnownApp("Configurações", "ms-settings:", addedNames);

            // Scan Start Menu directories
            var searchPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
            };

            foreach (var root in searchPaths)
            {
                if (!Directory.Exists(root)) continue;

                try
                {
                    var lnkFiles = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories);
                    foreach (var file in lnkFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(file);

                            // Skip uninstaller, readme, help links
                            if (fileName.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                                fileName.Contains("desinstalar", StringComparison.OrdinalIgnoreCase) ||
                                fileName.Contains("readme", StringComparison.OrdinalIgnoreCase) ||
                                fileName.Contains("help", StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (addedNames.Add(fileName))
                            {
                                var icon = IconHelper.GetIconForFile(file);
                                _cachedApps.Add(new StartMenuAppItem
                                {
                                    Name = fileName,
                                    Path = file,
                                    Description = "Aplicativo",
                                    Icon = icon,
                                    IsPinned = _pinnedApps.Any(p => string.Equals(p.ExecutablePath, file, StringComparison.OrdinalIgnoreCase))
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            _cachedApps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private void AddKnownApp(string name, string path, HashSet<string> addedNames)
        {
            if (addedNames.Add(name))
            {
                _cachedApps.Add(new StartMenuAppItem
                {
                    Name = name,
                    Path = path,
                    Description = "Sistema Windows",
                    Icon = IconHelper.GetIconForFile(path)
                });
            }
        }

        private void LoadPinnedApps()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    var items = JsonSerializer.Deserialize<List<PinnedDto>>(json);
                    if (items != null && items.Count > 0)
                    {
                        foreach (var item in items)
                        {
                            _pinnedApps.Add(new DockAppItem
                            {
                                Name = item.Name,
                                ExecutablePath = item.Path,
                                IsPinned = true,
                                Icon = IconHelper.GetIconForFile(item.Path)
                            });
                        }
                        return;
                    }
                }
            }
            catch { }

            // Default fallback pinned apps
            AddDefaultPinned("Explorador", "explorer.exe");
            AddDefaultPinned("Navegador", GetDefaultBrowserPath());
            AddDefaultPinned("Terminal", "wt.exe");
            AddDefaultPinned("Bloco de Notas", "notepad.exe");
            SavePinnedApps();
        }

        private void AddDefaultPinned(string name, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _pinnedApps.Add(new DockAppItem
            {
                Name = name,
                ExecutablePath = path,
                IsPinned = true,
                Icon = IconHelper.GetIconForFile(path)
            });
        }

        private static string GetDefaultBrowserPath()
        {
            string edge = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Microsoft\Edge\Application\msedge.exe");
            if (File.Exists(edge)) return edge;

            string chrome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Google\Chrome\Application\chrome.exe");
            if (File.Exists(chrome)) return chrome;

            return "explorer.exe";
        }

        private void SavePinnedApps()
        {
            try
            {
                var dtos = _pinnedApps.Select(p => new PinnedDto { Name = p.Name, Path = p.ExecutablePath }).ToList();
                string json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch { }
        }

        private class PinnedDto
        {
            public string Name { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
        }
    }
}
