using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private bool _isExpanded;
        private bool _isPinned;

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
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            IntPtr hwnd = helper.EnsureHandle();

            // Disable DWM window frame rounding on this transparent window
            int doNotRound = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref doNotRound, sizeof(int));

            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            TaskbarHelper.EnsureTopMost(this);

            RepositionOnScreen();

            // Reserve top work area for Notch (42px)
            TaskbarHelper.ReserveWorkArea(topMarginPixels: 42, bottomMarginPixels: 0);

            _timer.Start();
            UpdateQuickStats();
        }

        public void RepositionOnScreen()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double windowWidth = Width > 0 ? Width : 580;

            Left = Math.Max(0, Math.Round((screenW - windowWidth) / 2.0));
            Top = 0;
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

            // Refresh open flyout
            if (_cpuFlyout.IsVisible) _cpuFlyout.UpdateView();
            if (_memoryFlyout.IsVisible) _memoryFlyout.UpdateView();
            if (_diskFlyout.IsVisible) _diskFlyout.UpdateView();
            if (_networkFlyout.IsVisible) _networkFlyout.UpdateView();
            if (_mediaFlyout.IsVisible) _mediaFlyout.UpdateView();
        }

        private void UpdateQuickStats()
        {
            var track = _mediaService.CurrentTrack;

            // 1. Collapsed Pill Updates
            if (track.HasMedia)
            {
                TxtCollapsedTitle.Text = track.DisplayTitle;
                TxtMiniWaveform.Visibility = track.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
                TxtMiniPlayPauseIcon.Text = track.IsPlaying ? "⏸" : "▶";
            }
            else
            {
                TxtCollapsedTitle.Text = $"Spotify • {DateTime.Now:HH:mm}";
                TxtMiniWaveform.Visibility = Visibility.Collapsed;
                TxtMiniPlayPauseIcon.Text = "▶";
            }

            TxtCollapsedStats.Text = $"{_memoryService.UsagePercentage:F0}% RAM";

            // 2. Expanded Card Updates
            if (track.HasMedia)
            {
                TxtExpandedTrackTitle.Text = !string.IsNullOrWhiteSpace(track.Title) ? track.Title : "Tocando agora";
                TxtExpandedArtist.Text = !string.IsNullOrWhiteSpace(track.Artist) ? track.Artist : "Spotify";
                TxtExpandedPlayPauseIcon.Text = track.IsPlaying ? "⏸" : "▶";

                if (track.Thumbnail != null)
                {
                    ImgExpandedAlbum.Source = track.Thumbnail;
                    TxtAlbumPlaceholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ImgExpandedAlbum.Source = null;
                    TxtAlbumPlaceholder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                TxtExpandedTrackTitle.Text = "Nenhuma música tocando";
                TxtExpandedArtist.Text = "Spotify pausado ou fechado";
                TxtExpandedPlayPauseIcon.Text = "▶";
                ImgExpandedAlbum.Source = null;
                TxtAlbumPlaceholder.Visibility = Visibility.Visible;
            }

            // Expanded Hardware Stats
            TxtCpuQuick.Text = $"{_cpuService.TotalUsage:F0}%";
            TxtRamQuick.Text = $"{_memoryService.UsagePercentage:F0}%";

            double maxDiskSpeed = Math.Max(_diskService.GlobalReadBytesPerSec, _diskService.GlobalWriteBytesPerSec);
            TxtDiskQuick.Text = FormatCompactSpeed(maxDiskSpeed);

            double maxNetSpeed = Math.Max(_networkService.GlobalDownloadBytesPerSec, _networkService.GlobalUploadBytesPerSec);
            TxtNetQuick.Text = FormatCompactSpeed(maxNetSpeed);
        }

        // ================= DYNAMIC ISLAND EXPAND / COLLAPSE ANIMATIONS =================

        private void PillBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandNotch();
        }

        private void PillBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isPinned)
            {
                CollapseNotch();
            }
        }

        private void ExpandNotch()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            var widthAnim = new DoubleAnimation(540, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnim = new DoubleAnimation(148, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            PillBorder.BeginAnimation(WidthProperty, widthAnim);
            PillBorder.BeginAnimation(HeightProperty, heightAnim);

            GridCollapsed.Visibility = Visibility.Collapsed;
            GridExpanded.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
            GridExpanded.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void CollapseNotch()
        {
            if (!_isExpanded || _isPinned) return;
            _isExpanded = false;

            var widthAnim = new DoubleAnimation(280, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnim = new DoubleAnimation(38, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            PillBorder.BeginAnimation(WidthProperty, widthAnim);
            PillBorder.BeginAnimation(HeightProperty, heightAnim);

            GridExpanded.Visibility = Visibility.Collapsed;
            GridCollapsed.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160));
            GridCollapsed.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void BtnPinNotch_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            TxtPinIcon.Foreground = _isPinned ? (Brush)FindResource("AccentCyanBrush") : new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));
            if (_isPinned)
            {
                ExpandNotch();
            }
        }

        private async void BtnQuickPlayPause_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
            e.Handled = true;
        }

        private async void BtnPrevTrack_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.PreviousAsync();
            e.Handled = true;
        }

        private async void BtnNextTrack_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.NextAsync();
            e.Handled = true;
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
            if (e.ButtonState == MouseButtonState.Pressed && !_isExpanded)
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
            _startMenuFlyout.Top = 154; // Directly below expanded notch

            _startMenuFlyout.Show();
            _startMenuFlyout.Activate();

            if (focusSearch)
            {
                _startMenuFlyout.FocusSearch();
            }
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
            double flyoutY = 154; // Directly below expanded notch

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
                TaskbarHelper.ReserveWorkArea(topMarginPixels: 42, bottomMarginPixels: 0);
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
