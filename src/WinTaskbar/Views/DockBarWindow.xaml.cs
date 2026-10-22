using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinTaskbar.Models;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class DockBarWindow : Window
    {
        private readonly ICpuService _cpuService;
        private readonly IMemoryService _memoryService;
        private readonly IDiskService _diskService;
        private readonly INetworkService _networkService;
        private readonly IWindowService _windowService;
        private readonly IAppLauncherService _launcherService;
        private readonly IMediaService _mediaService;

        private readonly CpuFlyout _cpuFlyout;
        private readonly MemoryFlyout _memoryFlyout;
        private readonly DiskFlyout _diskFlyout;
        private readonly NetworkFlyout _networkFlyout;
        private readonly StartMenuFlyout _startMenuFlyout;
        private readonly MediaFlyout _mediaFlyout;

        private readonly DispatcherTimer _timer;

        public DockBarWindow(
            ICpuService cpuService,
            IMemoryService memoryService,
            IDiskService diskService,
            INetworkService networkService,
            IWindowService windowService,
            IAppLauncherService launcherService,
            IMediaService mediaService)
        {
            InitializeComponent();

            _cpuService = cpuService;
            _memoryService = memoryService;
            _diskService = diskService;
            _networkService = networkService;
            _windowService = windowService;
            _launcherService = launcherService;
            _mediaService = mediaService;

            _cpuFlyout = new CpuFlyout(_cpuService);
            _memoryFlyout = new MemoryFlyout(_memoryService);
            _diskFlyout = new DiskFlyout(_diskService);
            _networkFlyout = new NetworkFlyout(_networkService);
            _startMenuFlyout = new StartMenuFlyout(_launcherService);
            _mediaFlyout = new MediaFlyout(_mediaService);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            Loaded += OnLoaded;
            SizeChanged += (s, e) => RepositionOnScreen();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TaskbarHelper.ApplyWindows11Styling(this, roundCorners: true);
            TaskbarHelper.EnsureTopMost(this);

            RepositionOnScreen();

            // Reserve bottom work area so maximized windows (WhatsApp, Chrome, etc.) stop above dock
            TaskbarHelper.ReserveWorkArea(64);

            _timer.Start();
            UpdateQuickStats();
            RefreshDockApps();
        }

        public void RepositionOnScreen()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            double dockWidth = ActualWidth > 0 ? ActualWidth : 760;
            double dockHeight = ActualHeight > 0 ? ActualHeight : 76;

            Left = Math.Max(10, (screenW - dockWidth) / 2);
            Top = screenH - dockHeight - 2;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            TaskbarHelper.EnsureTopMost(this);

            _cpuService.Update();
            _memoryService.Update();
            _diskService.Update();
            _networkService.Update();
            _ = _mediaService.UpdateAsync();

            UpdateQuickStats();
            RefreshDockApps();

            // Refresh open flyout
            if (_cpuFlyout.IsVisible) _cpuFlyout.UpdateView();
            if (_memoryFlyout.IsVisible) _memoryFlyout.UpdateView();
            if (_diskFlyout.IsVisible) _diskFlyout.UpdateView();
            if (_networkFlyout.IsVisible) _networkFlyout.UpdateView();
            if (_mediaFlyout.IsVisible) _mediaFlyout.UpdateView();
        }

        private void UpdateQuickStats()
        {
            TxtCpuQuick.Text = $"{_cpuService.TotalUsage:F0}%";
            BarRamQuick.Value = _memoryService.UsagePercentage;
            TxtRamQuick.Text = $"{_memoryService.UsagePercentage:F0}%";

            double maxDiskSpeed = Math.Max(_diskService.GlobalReadBytesPerSec, _diskService.GlobalWriteBytesPerSec);
            TxtDiskQuick.Text = FormatCompactSpeed(maxDiskSpeed);

            double maxNetSpeed = Math.Max(_networkService.GlobalDownloadBytesPerSec, _networkService.GlobalUploadBytesPerSec);
            TxtNetQuick.Text = FormatCompactSpeed(maxNetSpeed);

            TxtClock.Text = DateTime.Now.ToString("HH:mm");

            // Update Media Pill
            var track = _mediaService.CurrentTrack;
            if (track.HasMedia)
            {
                TxtMediaStatus.Text = track.DisplayTitle;
                TxtQuickPlayPauseIcon.Text = track.IsPlaying ? "⏸" : "▶";
            }
            else
            {
                TxtMediaStatus.Text = "Spotify";
                TxtQuickPlayPauseIcon.Text = "▶";
            }
        }

        private void RefreshDockApps()
        {
            try
            {
                var pinned = _launcherService.GetPinnedApps();
                var openApps = _windowService.GetOpenWindows();

                var displayList = new List<DockAppItem>();
                var matchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Add all pinned apps with their current running state
                foreach (var p in pinned)
                {
                    string pExeName = Path.GetFileNameWithoutExtension(p.ExecutablePath);

                    var matchingApp = openApps.FirstOrDefault(w =>
                        (!string.IsNullOrEmpty(p.ExecutablePath) && string.Equals(w.ExecutablePath, p.ExecutablePath, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(pExeName) && string.Equals(w.Name, pExeName, StringComparison.OrdinalIgnoreCase)));

                    if (matchingApp != null)
                    {
                        p.IsRunning = true;
                        p.IsActive = matchingApp.IsActive;
                        p.Hwnd = matchingApp.Hwnd;
                        p.OpenHwnds.Clear();
                        p.OpenHwnds.AddRange(matchingApp.OpenHwnds);
                        p.BadgeCount = matchingApp.BadgeCount;
                        p.Title = matchingApp.Title;
                        p.ShowSeparatorBefore = false;

                        string key = !string.IsNullOrEmpty(matchingApp.ExecutablePath) ? matchingApp.ExecutablePath : matchingApp.Name;
                        matchedKeys.Add(key);
                    }
                    else
                    {
                        p.IsRunning = false;
                        p.IsActive = false;
                        p.Hwnd = IntPtr.Zero;
                        p.OpenHwnds.Clear();
                        p.BadgeCount = 0;
                        p.ShowSeparatorBefore = false;
                    }

                    displayList.Add(p);
                }

                // Add other open apps that aren't pinned
                bool isFirstUnpinned = true;
                foreach (var w in openApps)
                {
                    string key = !string.IsNullOrEmpty(w.ExecutablePath) ? w.ExecutablePath : w.Name;
                    if (!matchedKeys.Contains(key))
                    {
                        w.IsPinned = false;
                        w.ShowSeparatorBefore = isFirstUnpinned && pinned.Count > 0;
                        isFirstUnpinned = false;
                        displayList.Add(w);
                    }
                }

                // Preserve existing scale if mouse is hovering
                if (ItemsDockApps.ItemsSource is IList<DockAppItem> existingList && existingList.Count == displayList.Count)
                {
                    for (int i = 0; i < displayList.Count; i++)
                    {
                        displayList[i].Scale = existingList[i].Scale;
                    }
                }

                ItemsDockApps.ItemsSource = displayList;
            }
            catch { }
        }

        private void ItemsDockApps_MouseMove(object sender, MouseEventArgs e)
        {
            if (ItemsDockApps.ItemsSource is not IList<DockAppItem> items || items.Count == 0)
                return;

            Point mousePos = e.GetPosition(ItemsDockApps);
            double mouseX = mousePos.X;
            const double waveRadius = 90.0;
            const double maxScale = 1.38;

            for (int i = 0; i < items.Count; i++)
            {
                var container = ItemsDockApps.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;

                Point itemCenter = container.TransformToAncestor(ItemsDockApps).Transform(new Point(container.ActualWidth / 2.0, 0));
                double distance = Math.Abs(mouseX - itemCenter.X);

                if (distance < waveRadius)
                {
                    double factor = Math.Cos((distance / waveRadius) * (Math.PI / 2.0));
                    items[i].Scale = 1.0 + (maxScale - 1.0) * factor;
                }
                else
                {
                    items[i].Scale = 1.0;
                }
            }
        }

        private void ItemsDockApps_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ItemsDockApps.ItemsSource is not IList<DockAppItem> items)
                return;

            foreach (var item in items)
            {
                item.Scale = 1.0;
            }
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

        private void BtnStartMenu_Click(object sender, RoutedEventArgs e)
        {
            ToggleStartMenu(focusSearch: false);
        }

        private void BtnSearchLauncher_Click(object sender, RoutedEventArgs e)
        {
            ToggleStartMenu(focusSearch: true);
        }

        private void ToggleStartMenu(bool focusSearch)
        {
            if (_startMenuFlyout.IsVisible)
            {
                _startMenuFlyout.Hide();
                return;
            }

            CloseOtherFlyouts();

            _startMenuFlyout.RefreshContent();

            Point btnScreenPos = BtnStartMenu.PointToScreen(new Point(0, 0));
            _startMenuFlyout.Left = Math.Max(12, btnScreenPos.X - 10);
            _startMenuFlyout.Top = Math.Max(10, Top - _startMenuFlyout.Height - 12);

            _startMenuFlyout.Show();
            _startMenuFlyout.Activate();

            if (focusSearch)
            {
                _startMenuFlyout.FocusSearch();
            }
        }

        private void BtnDockApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DockAppItem item)
            {
                if (item.IsRunning)
                {
                    _windowService.ToggleApp(item);
                }
                else if (!string.IsNullOrEmpty(item.ExecutablePath))
                {
                    _launcherService.Launch(item.ExecutablePath, item.Arguments);
                }
            }
        }

        private void BtnDockApp_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DockAppItem item)
            {
                var menu = new ContextMenu();

                if (item.IsPinned)
                {
                    var itemUnpin = new MenuItem { Header = "📌 Desafixar da Dock" };
                    itemUnpin.Click += (s, ev) =>
                    {
                        _launcherService.UnpinApp(item.ExecutablePath);
                        RefreshDockApps();
                    };
                    menu.Items.Add(itemUnpin);
                }
                else
                {
                    var itemPin = new MenuItem { Header = "📌 Fixar na Dock" };
                    itemPin.Click += (s, ev) =>
                    {
                        _launcherService.PinApp(item.Name, item.ExecutablePath);
                        RefreshDockApps();
                    };
                    menu.Items.Add(itemPin);
                }

                if (item.IsRunning && item.Hwnd != IntPtr.Zero)
                {
                    menu.Items.Add(new Separator());
                    var itemClose = new MenuItem { Header = "❌ Fechar Janela" };
                    itemClose.Click += (s, ev) =>
                    {
                        _windowService.CloseWindow(item.Hwnd);
                    };
                    menu.Items.Add(itemClose);
                }

                menu.PlacementTarget = btn;
                menu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void BtnMedia_Click(object sender, RoutedEventArgs e)
        {
            _mediaFlyout.UpdateView();
            PositionAndShowFlyout(_mediaFlyout, BtnMedia);
        }

        private async void BtnQuickPlayPause_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
            e.Handled = true;
        }

        private void PositionAndShowFlyout(Window flyout, FrameworkElement sourceButton)
        {
            if (flyout.IsVisible)
            {
                flyout.Hide();
                return;
            }

            CloseOtherFlyouts();

            Point btnScreenPos = sourceButton.PointToScreen(new Point(0, 0));
            double flyoutX = btnScreenPos.X + (sourceButton.ActualWidth / 2) - (flyout.Width / 2);
            double flyoutY = Top - flyout.Height - 10;

            double screenW = SystemParameters.PrimaryScreenWidth;
            if (flyoutX + flyout.Width > screenW)
            {
                flyoutX = screenW - flyout.Width - 10;
            }
            if (flyoutX < 10)
            {
                flyoutX = 10;
            }

            flyout.Left = flyoutX;
            flyout.Top = flyoutY;
            flyout.Show();
            flyout.Activate();
        }

        private void CloseOtherFlyouts()
        {
            _cpuFlyout.Hide();
            _memoryFlyout.Hide();
            _diskFlyout.Hide();
            _networkFlyout.Hide();
            _startMenuFlyout.Hide();
            _mediaFlyout.Hide();
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

        public void SetReserveWorkArea(bool enable)
        {
            if (enable)
                TaskbarHelper.ReserveWorkArea(64);
            else
                TaskbarHelper.RestoreWorkArea();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            TaskbarHelper.RestoreWorkArea();
            _cpuFlyout.Close();
            _memoryFlyout.Close();
            _diskFlyout.Close();
            _networkFlyout.Close();
            _startMenuFlyout.Close();
            _mediaFlyout.Close();
            base.OnClosed(e);
        }
    }
}
