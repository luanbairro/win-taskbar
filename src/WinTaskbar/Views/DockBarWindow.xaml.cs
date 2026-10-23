using System;
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
    public partial class DockBarWindow : Window
    {
        private readonly ICpuService _cpuService;
        private readonly IMemoryService _memoryService;
        private readonly IDiskService _diskService;
        private readonly INetworkService _networkService;
        private readonly IWindowService _windowService;
        private readonly IAppLauncherService _launcherService;
        private readonly IMediaService _mediaService;

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
            UpdateQuickStats();
            UpdateNotchGeometry(NotchRoot.ActualWidth > 0 ? NotchRoot.ActualWidth : 320, NotchRoot.ActualHeight > 0 ? NotchRoot.ActualHeight : 32);
        }

        public void RepositionOnScreen()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double windowWidth = Width > 0 ? Width : 560;

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

        private void UpdateQuickStats()
        {
            double battery = GetBatteryPercent();
            double cpu = _cpuService.TotalUsage;
            double ram = _memoryService.UsagePercentage;
            var track = _mediaService.CurrentTrack;

            // 1. Collapsed Mode Updates (Image 1)
            GaugeBatteryCollapsed.Value = battery;
            TxtClockCollapsed.Text = DateTime.Now.ToString("HH:mm");

            GaugeCpuCollapsed.Value = cpu;
            TxtCpuCollapsed.Text = $"{cpu:F0}%";

            GaugeRamCollapsed.Value = ram;
            TxtRamCollapsed.Text = $"{ram:F0}%";

            BorderSpotifyMini.Visibility = track.HasMedia && track.IsPlaying ? Visibility.Visible : Visibility.Collapsed;

            // 2. Expanded Mode Updates (Image 2)
            GaugeBatteryExpanded.Value = battery;
            TxtDateTimeExpanded.Text = DateTime.Now.ToString("ddd d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

            GaugeCpuExpanded.Value = cpu;
            TxtCpuExpanded.Text = $"{cpu:F0}%";

            GaugeRamExpanded.Value = ram;
            TxtRamExpanded.Text = $"{ram:F0}%";

            if (track.HasMedia)
            {
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

                // Update progress bar width
                double totalBarWidth = 280.0;
                BarMediaProgress.Width = Math.Max(2.0, (totalBarWidth * (track.ProgressPercentage / 100.0)));
            }
            else
            {
                TxtSongTitle.Text = "Nenhuma música tocando";
                TxtArtistName.Text = "Spotify";
                TxtPlayPauseIcon.Text = "▶";
                ImgAlbumArt.Source = null;
                TxtAlbumPlaceholder.Visibility = Visibility.Visible;
                TxtTimeElapsed.Text = "00:00";
                TxtTimeRemaining.Text = "-00:00";
                BarMediaProgress.Width = 0;
            }
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

            var widthAnim = new DoubleAnimation(500, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnim = new DoubleAnimation(144, TimeSpan.FromMilliseconds(220))
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
            base.OnClosed(e);
        }
    }
}
