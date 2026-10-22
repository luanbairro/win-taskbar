using System;
using System.Windows;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class CpuFlyout : Window
    {
        private readonly ICpuService _cpuService;

        public CpuFlyout(ICpuService cpuService)
        {
            InitializeComponent();
            _cpuService = cpuService;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TaskbarHelper.ApplyWindows11Styling(this);
            TaskbarHelper.SetToolWindow(this);
            UpdateView();
        }

        public void UpdateView()
        {
            TxtCpuName.Text = _cpuService.CpuName;
            TxtTotalUsage.Text = $"{_cpuService.TotalUsage:F0}%";
            TxtUserUsage.Text = $"{_cpuService.UserUsage:F0}%";
            TxtSystemUsage.Text = $"{_cpuService.SystemUsage:F0}%";

            BarCpuUsage.Value = _cpuService.TotalUsage;
            CpuChart.PrimaryValues = _cpuService.History;
            CpuChart.InvalidateVisual();

            ListProcesses.ItemsSource = _cpuService.TopProcesses;

            var up = _cpuService.Uptime;
            TxtUptime.Text = $"{up.Days}d {up.Hours:D2}h {up.Minutes:D2}m {up.Seconds:D2}s";
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            Hide();
        }
    }
}
