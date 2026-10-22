using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class TaskbarOverlayWindow : Window
    {
        private readonly ICpuService _cpuService;
        private readonly IMemoryService _memoryService;
        private readonly IDiskService _diskService;
        private readonly INetworkService _networkService;

        private readonly CpuFlyout _cpuFlyout;
        private readonly MemoryFlyout _memoryFlyout;
        private readonly DiskFlyout _diskFlyout;
        private readonly NetworkFlyout _networkFlyout;

        private readonly DispatcherTimer _timer;

        public TaskbarOverlayWindow(
            ICpuService cpuService,
            IMemoryService memoryService,
            IDiskService diskService,
            INetworkService networkService)
        {
            InitializeComponent();

            _cpuService = cpuService;
            _memoryService = memoryService;
            _diskService = diskService;
            _networkService = networkService;

            _cpuFlyout = new CpuFlyout(_cpuService);
            _memoryFlyout = new MemoryFlyout(_memoryService);
            _diskFlyout = new DiskFlyout(_diskService);
            _networkFlyout = new NetworkFlyout(_networkService);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TaskbarHelper.ApplyWindows11Styling(this, roundCorners: true);
            TaskbarHelper.SetToolWindow(this);
            RepositionOnTaskbar();

            _timer.Start();
            UpdateQuickStats();
        }

        public void RepositionOnTaskbar()
        {
            var taskbar = TaskbarHelper.GetTaskbarInfo();
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            // Default placement: Bottom taskbar, positioned right before system tray area
            if (taskbar.Edge == TaskbarHelper.TaskbarEdge.Bottom)
            {
                Left = Math.Max(20, taskbar.Rect.Right - Width - 240);
                Top = taskbar.Rect.Top + Math.Max(0, (taskbar.Rect.Height - Height) / 2);
            }
            else if (taskbar.Edge == TaskbarHelper.TaskbarEdge.Top)
            {
                Left = Math.Max(20, taskbar.Rect.Right - Width - 240);
                Top = taskbar.Rect.Top + Math.Max(0, (taskbar.Rect.Height - Height) / 2);
            }
            else
            {
                Left = screenW - Width - 200;
                Top = screenH - Height - 50;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _cpuService.Update();
            _memoryService.Update();
            _diskService.Update();
            _networkService.Update();

            UpdateQuickStats();

            // Refresh open flyout
            if (_cpuFlyout.IsVisible) _cpuFlyout.UpdateView();
            if (_memoryFlyout.IsVisible) _memoryFlyout.UpdateView();
            if (_diskFlyout.IsVisible) _diskFlyout.UpdateView();
            if (_networkFlyout.IsVisible) _networkFlyout.UpdateView();
        }

        private void UpdateQuickStats()
        {
            TxtCpuQuick.Text = $"{_cpuService.TotalUsage:F0}%";
            BarRamQuick.Value = _memoryService.UsagePercentage;

            double maxDiskSpeed = Math.Max(_diskService.GlobalReadBytesPerSec, _diskService.GlobalWriteBytesPerSec);
            TxtDiskQuick.Text = FormatCompactSpeed(maxDiskSpeed);

            double maxNetSpeed = Math.Max(_networkService.GlobalDownloadBytesPerSec, _networkService.GlobalUploadBytesPerSec);
            TxtNetQuick.Text = FormatCompactSpeed(maxNetSpeed);
        }

        private static string FormatCompactSpeed(double bytes)
        {
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):F1}M/s";
            if (bytes >= 1024)
                return $"{bytes / 1024:F0}K/s";
            return $"{bytes:F0}B/s";
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void PositionAndShowFlyout(Window flyout, FrameworkElement sourceButton)
        {
            if (flyout.IsVisible)
            {
                flyout.Hide();
                return;
            }

            // Close any other open flyouts
            _cpuFlyout.Hide();
            _memoryFlyout.Hide();
            _diskFlyout.Hide();
            _networkFlyout.Hide();

            Point btnScreenPos = sourceButton.PointToScreen(new Point(0, 0));
            double flyoutX = btnScreenPos.X + (sourceButton.ActualWidth / 2) - (flyout.Width / 2);
            double flyoutY = Top - flyout.Height - 8;

            // Ensure flyout stays within screen bounds
            double screenW = SystemParameters.PrimaryScreenWidth;
            if (flyoutX + flyout.Width > screenW)
            {
                flyoutX = screenW - flyout.Width - 10;
            }
            if (flyoutX < 10)
            {
                flyoutX = 10;
            }
            if (flyoutY < 10)
            {
                flyoutY = Top + Height + 8;
            }

            flyout.Left = flyoutX;
            flyout.Top = flyoutY;
            flyout.Show();
            flyout.Activate();
        }

        private void BtnCpu_Click(object sender, RoutedEventArgs e)
        {
            _cpuFlyout.UpdateView();
            PositionAndShowFlyout(_cpuFlyout, BtnCpu);
        }

        private void BtnRam_Click(object sender, RoutedEventArgs e)
        {
            _memoryFlyout.UpdateView();
            PositionAndShowFlyout(_memoryFlyout, BtnRam);
        }

        private void BtnDisk_Click(object sender, RoutedEventArgs e)
        {
            _diskFlyout.UpdateView();
            PositionAndShowFlyout(_diskFlyout, BtnDisk);
        }

        private void BtnNet_Click(object sender, RoutedEventArgs e)
        {
            _networkFlyout.UpdateView();
            PositionAndShowFlyout(_networkFlyout, BtnNet);
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _cpuFlyout.Close();
            _memoryFlyout.Close();
            _diskFlyout.Close();
            _networkFlyout.Close();
            base.OnClosed(e);
        }
    }
}
