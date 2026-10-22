using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
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
        private TaskbarOverlayWindow? _overlayWindow;

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

            // Initial updates
            cpuService.Update();
            memoryService.Update();
            diskService.Update();
            networkService.Update();

            // Initialize System Tray Icon
            InitializeTrayIcon();

            // Launch Overlay Bar
            _overlayWindow = new TaskbarOverlayWindow(cpuService, memoryService, diskService, networkService);
            _overlayWindow.Show();
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = "WinTaskbar Monitor (Windows 11)",
                Icon = SystemIcons.Application,
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Repositionar na Barra", null, (s, e) => _overlayWindow?.RepositionOnTaskbar());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Sobre o WinTaskbar", null, (s, e) =>
            {
                MessageBox.Show("WinTaskbar Monitor v1.0\nMonitor de CPU, RAM, Disco e Rede para Windows 11\nDesenvolvido por Luan Bairro.",
                    "Sobre o WinTaskbar", MessageBoxButton.OK, MessageBoxImage.Information);
            });
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Sair", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _overlayWindow?.Close();
                Shutdown();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => _overlayWindow?.RepositionOnTaskbar();
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
