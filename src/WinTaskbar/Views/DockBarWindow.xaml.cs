using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public class CalendarDayItem
    {
        public string DayName { get; set; } = string.Empty;
        public int DayNumber { get; set; }
        public bool IsToday { get; set; }
    }

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

        private readonly DispatcherTimer _timer;
        private bool _isExpanded;

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

            int doNotRound = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref doNotRound, sizeof(int));

            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            TaskbarHelper.EnsureTopMost(this);

            RepositionOnScreen();

            // Reserve top margin for Notch (34px)
            TaskbarHelper.ReserveWorkArea(topMarginPixels: 34, bottomMarginPixels: 0);

            _timer.Start();
            PopulateCalendar();
            UpdateQuickStats();
            UpdateNotchGeometry(NotchRoot.ActualWidth > 0 ? NotchRoot.ActualWidth : 320, NotchRoot.ActualHeight > 0 ? NotchRoot.ActualHeight : 32);
        }

        public void RepositionOnScreen()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double windowWidth = Width > 0 ? Width : 600;

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
        }

        private double GetBatteryPercent()
        {
            try
            {
                var power = System.Windows.Forms.SystemInformation.PowerStatus;
                float life = power.BatteryLifePercent;
                if (life >= 0f && life <= 1.0f)
                {
                    return Math.Round(life * 100.0);
                }
            }
            catch { }
            return 100.0;
        }

        private void PopulateCalendar()
        {
            try
            {
                DateTime today = DateTime.Today;
                int dayOfWeek = (int)today.DayOfWeek;
                int deltaToMonday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
                DateTime monday = today.AddDays(deltaToMonday);

                string[] shortDays = { "SEG", "TER", "QUA", "QUI", "SEX", "SÁB", "DOM" };
                var list = new List<CalendarDayItem>();

                for (int i = 0; i < 7; i++)
                {
                    DateTime d = monday.AddDays(i);
                    list.Add(new CalendarDayItem
                    {
                        DayName = shortDays[i],
                        DayNumber = d.Day,
                        IsToday = (d.Date == today.Date)
                    });
                }

                ItemsCalendar.ItemsSource = list;

                var ptCulture = new CultureInfo("pt-BR");
                string dayOfWeekName = ptCulture.DateTimeFormat.GetDayName(today.DayOfWeek);
                string monthName = ptCulture.DateTimeFormat.GetMonthName(today.Month);
                TxtAgendaHeader.Text = $"📅 Hoje, {today.Day} de {char.ToUpper(monthName[0]) + monthName.Substring(1)}";
                TxtAgendaSub.Text = $"{char.ToUpper(dayOfWeekName[0]) + dayOfWeekName.Substring(1)} • {DateTime.Now:HH:mm}";
            }
            catch { }
        }

        private void UpdateQuickStats()
        {
            double battery = GetBatteryPercent();
            double cpu = _cpuService.TotalUsage;
            double ram = _memoryService.UsagePercentage;
            double maxDiskSpeed = Math.Max(_diskService.GlobalReadBytesPerSec, _diskService.GlobalWriteBytesPerSec);
            double maxNetSpeed = Math.Max(_networkService.GlobalDownloadBytesPerSec, _networkService.GlobalUploadBytesPerSec);
            var track = _mediaService.CurrentTrack;

            // 1. Collapsed Mode Updates (Image 1)
            GaugeBatteryCollapsed.Value = battery;
            TxtClockCollapsed.Text = DateTime.Now.ToString("HH:mm");

            GaugeCpuCollapsed.Value = cpu;
            TxtCpuCollapsed.Text = $"{cpu:F0}%";

            GaugeRamCollapsed.Value = ram;
            TxtRamCollapsed.Text = $"{ram:F0}%";

            BorderSpotifyMini.Visibility = track.HasMedia && track.IsPlaying ? Visibility.Visible : Visibility.Collapsed;

            // 2. Expanded Mode Switching
            if (track.HasMedia)
            {
                // Show Media Card (Image 2 style)
                GridMediaView.Visibility = Visibility.Visible;
                GridAgendaTelemetryView.Visibility = Visibility.Collapsed;

                GaugeBatteryExpanded.Value = battery;
                TxtDateTimeExpanded.Text = DateTime.Now.ToString("ddd d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

                // Telemetry Pills in Media Header (Clickable)
                GaugeCpuMedia.Value = cpu;
                TxtCpuMedia.Text = $"{cpu:F0}%";

                GaugeRamMedia.Value = ram;
                TxtRamMedia.Text = $"{ram:F0}%";

                TxtDiskMedia.Text = FormatCompactSpeed(maxDiskSpeed);
                TxtNetMedia.Text = FormatCompactSpeed(maxNetSpeed);

                TxtSongTitle.Text = !string.IsNullOrWhiteSpace(track.Title) ? track.Title : "Histórias dos Hinos, Live 1";
                TxtArtistName.Text = !string.IsNullOrWhiteSpace(track.Artist) ? track.Artist : "Ricardo Martins";
                TxtPlayPauseIcon.Text = track.IsPlaying ? "⏸" : "▶";

                if (track.Thumbnail != null)
                {
                    ImgAlbumArt.Source = track.Thumbnail;
                    TxtAlbumPlaceholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ImgAlbumArt.Source = null;
                    TxtAlbumPlaceholder.Visibility = Visibility.Visible;
                }

                TxtTimeElapsed.Text = track.PositionFormatted;
                TxtTimeRemaining.Text = track.RemainingFormatted;

                double totalBarWidth = 280.0;
                BarMediaProgress.Width = Math.Max(2.0, (totalBarWidth * (track.ProgressPercentage / 100.0)));
            }
            else
            {
                // Show Agenda + 4 Large Telemetry Cards
                GridMediaView.Visibility = Visibility.Collapsed;
                GridAgendaTelemetryView.Visibility = Visibility.Visible;

                // Telemetry Dashboard
                GaugeCpuDash.Value = cpu;
                TxtCpuDash.Text = $"{cpu:F0}%";

                GaugeRamDash.Value = ram;
                TxtRamDash.Text = $"{ram:F0}%";

                TxtDiskDash.Text = FormatCompactSpeed(maxDiskSpeed);
                GaugeDiskDash.Value = Math.Clamp((maxDiskSpeed / (1024.0 * 1024.0 * 20.0)) * 100.0, 5, 100);

                TxtNetDash.Text = FormatCompactSpeed(maxNetSpeed);
                GaugeNetDash.Value = Math.Clamp((maxNetSpeed / (1024.0 * 1024.0 * 10.0)) * 100.0, 5, 100);

                var ptCulture = new CultureInfo("pt-BR");
                DateTime today = DateTime.Today;
                string dayOfWeekName = ptCulture.DateTimeFormat.GetDayName(today.DayOfWeek);
                string monthName = ptCulture.DateTimeFormat.GetMonthName(today.Month);
                TxtAgendaHeader.Text = $"📅 Hoje, {today.Day} de {char.ToUpper(monthName[0]) + monthName.Substring(1)}";
                TxtAgendaSub.Text = $"{char.ToUpper(dayOfWeekName[0]) + dayOfWeekName.Substring(1)} • {DateTime.Now:HH:mm}";
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

        // ================= NOTCH GEOMETRY (Concave Ears + Rounded Bottom) =================

        private void NotchRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateNotchGeometry(e.NewSize.Width, e.NewSize.Height);
        }

        private void UpdateNotchGeometry(double width, double height)
        {
            const double E = 14.0; // Concave ear curve radius
            const double R = 18.0; // Bottom corners convex radius

            if (width <= (E + R) * 2 || height <= E + R) return;

            var figure = new PathFigure
            {
                StartPoint = new Point(0, 0),
                IsClosed = true,
                IsFilled = true
            };

            // 1. Top-left ear curve down into notch
            figure.Segments.Add(new BezierSegment(
                new Point(E * 0.55, 0),
                new Point(E, E * 0.45),
                new Point(E, E),
                true));

            // 2. Left straight edge down
            figure.Segments.Add(new LineSegment(new Point(E, height - R), true));

            // 3. Bottom-left rounded corner
            figure.Segments.Add(new ArcSegment(
                new Point(E + R, height),
                new Size(R, R),
                0,
                false,
                SweepDirection.Counterclockwise,
                true));

            // 4. Bottom straight edge
            figure.Segments.Add(new LineSegment(new Point(width - E - R, height), true));

            // 5. Bottom-right rounded corner
            figure.Segments.Add(new ArcSegment(
                new Point(width - E, height - R),
                new Size(R, R),
                0,
                false,
                SweepDirection.Counterclockwise,
                true));

            // 6. Right straight edge up
            figure.Segments.Add(new LineSegment(new Point(width - E, E), true));

            // 7. Top-right ear curve back out to screen top
            figure.Segments.Add(new BezierSegment(
                new Point(width - E, E * 0.45),
                new Point(width - E * 0.55, 0),
                new Point(width, 0),
                true));

            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            NotchBackgroundPath.Data = geo;
        }

        // ================= DYNAMIC ISLAND HOVER EXPANSION =================

        private void NotchRoot_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandNotch();
        }

        private void NotchRoot_MouseLeave(object sender, MouseEventArgs e)
        {
            CollapseNotch();
        }

        private void ExpandNotch()
        {
            if (_isExpanded) return;
            _isExpanded = true;

            bool hasMedia = _mediaService.CurrentTrack.HasMedia;
            double targetWidth = hasMedia ? 520 : 550;
            double targetHeight = hasMedia ? 146 : 148;

            var widthAnim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnim = new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            NotchRoot.BeginAnimation(WidthProperty, widthAnim);
            NotchRoot.BeginAnimation(HeightProperty, heightAnim);

            GridCollapsed.Visibility = Visibility.Collapsed;
            GridExpanded.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
            GridExpanded.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void CollapseNotch()
        {
            if (!_isExpanded) return;
            _isExpanded = false;

            CloseOtherFlyouts();

            var widthAnim = new DoubleAnimation(320, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnim = new DoubleAnimation(32, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            NotchRoot.BeginAnimation(WidthProperty, widthAnim);
            NotchRoot.BeginAnimation(HeightProperty, heightAnim);

            GridExpanded.Visibility = Visibility.Collapsed;
            GridCollapsed.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160));
            GridCollapsed.BeginAnimation(OpacityProperty, fadeIn);
        }

        // ================= DETAILED TELEMETRY FLYOUT DISPATCHERS =================

        private void BtnCpu_Click(object sender, RoutedEventArgs e)
        {
            _cpuFlyout.UpdateView();
            PositionAndShowFlyout(_cpuFlyout, (FrameworkElement)sender);
        }

        private void BtnRam_Click(object sender, RoutedEventArgs e)
        {
            _memoryFlyout.UpdateView();
            PositionAndShowFlyout(_memoryFlyout, (FrameworkElement)sender);
        }

        private void BtnDisk_Click(object sender, RoutedEventArgs e)
        {
            _diskFlyout.UpdateView();
            PositionAndShowFlyout(_diskFlyout, (FrameworkElement)sender);
        }

        private void BtnNet_Click(object sender, RoutedEventArgs e)
        {
            _networkFlyout.UpdateView();
            PositionAndShowFlyout(_networkFlyout, (FrameworkElement)sender);
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
            double flyoutY = NotchRoot.ActualHeight + 6;

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
        }

        // ================= MEDIA PLAYBACK HANDLERS =================

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

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !_isExpanded)
            {
                DragMove();
            }
        }

        public void SetReserveWorkArea(bool enable)
        {
            if (enable)
                TaskbarHelper.ReserveWorkArea(topMarginPixels: 34, bottomMarginPixels: 0);
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
            base.OnClosed(e);
        }
    }
}
