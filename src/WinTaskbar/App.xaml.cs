using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using WinTaskbar.Native;
using WinTaskbar.Services;
using WinTaskbar.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace WinTaskbar
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private NotifyIcon? _notifyIcon;
        private DockBarWindow? _dockWindow;
        private bool _isWindowsTaskbarHidden;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            const string appMutexName = "WinTaskbar_SingleInstance_Mutex_LuanBairro";
            _singleInstanceMutex = new Mutex(true, appMutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                MessageBox.Show("O WinTaskbar já está em execução na bandeja do sistema / barra de tarefas.",
                    "WinTaskbar", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Initialize Services
            var cpuService = new CpuService();
            var memoryService = new MemoryService();
            var diskService = new DiskService();
            var networkService = new NetworkService();
            var windowService = new WindowService();
            var launcherService = new AppLauncherService();

            // Initial updates
            cpuService.Update();
            memoryService.Update();
            diskService.Update();
            networkService.Update();

            // Initialize System Tray Icon
            InitializeTrayIcon();

            // Launch Floating Liquid Glass Dock
            _dockWindow = new DockBarWindow(
                cpuService,
                memoryService,
                diskService,
                networkService,
                windowService,
                launcherService);

            _dockWindow.Show();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "WinTaskbar Dock & Menu Iniciar (Windows 11)",
                Icon = SystemIcons.Application,
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Centralizar Dock", null, (s, e) => _dockWindow?.RepositionOnScreen());

            var taskbarToggleItem = new ToolStripMenuItem("Ocultar Barra do Windows (Modo Imersivo)");
            taskbarToggleItem.Click += (s, e) =>
            {
                ToggleWindowsTaskbar();
                taskbarToggleItem.Text = _isWindowsTaskbarHidden ? "Exibir Barra Padrão do Windows" : "Ocultar Barra do Windows (Modo Imersivo)";
            };
            contextMenu.Items.Add(taskbarToggleItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Sobre o WinTaskbar Dock", null, (s, e) =>
            {
                MessageBox.Show("WinTaskbar Dock v2.0 (LumaBar Edition)\nDock Flutuante Moderna com Menu Iniciar, Gerenciador de Janelas e Monitor de Sistema.\nDesenvolvido por Luan Bairro.",
                    "Sobre o WinTaskbar Dock", MessageBoxButton.OK, MessageBoxImage.Information);
            });
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Sair", null, (s, e) =>
            {
                RestoreWindowsTaskbar();
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _dockWindow?.Close();
                Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => _dockWindow?.RepositionOnScreen();
        }

        private void ToggleWindowsTaskbar()
        {
            IntPtr trayHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (trayHwnd != IntPtr.Zero)
            {
                _isWindowsTaskbarHidden = !_isWindowsTaskbarHidden;
                NativeMethods.ShowWindow(trayHwnd, _isWindowsTaskbarHidden ? NativeMethods.SW_HIDE : NativeMethods.SW_SHOW);
            }
        }

        private void RestoreWindowsTaskbar()
        {
            if (_isWindowsTaskbarHidden)
            {
                IntPtr trayHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (trayHwnd != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(trayHwnd, NativeMethods.SW_SHOW);
                }
                _isWindowsTaskbarHidden = false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            RestoreWindowsTaskbar();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _singleInstanceMutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
