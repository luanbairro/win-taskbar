using System;
using System.Windows;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class MemoryFlyout : Window
    {
        private readonly IMemoryService _memoryService;

        public MemoryFlyout(IMemoryService memoryService)
        {
            InitializeComponent();
            _memoryService = memoryService;
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
            TxtTotal.Text = _memoryService.FormattedTotal;
            TxtUsed.Text = $"{_memoryService.FormattedUsed} ({_memoryService.UsagePercentage:F0}%)";
            TxtFree.Text = _memoryService.FormattedFree;

            BarMemoryUsage.Value = _memoryService.UsagePercentage;
            MemChart.PrimaryValues = _memoryService.History;
            MemChart.InvalidateVisual();

            ListProcesses.ItemsSource = _memoryService.TopProcesses;

            BarSwapUsage.Value = _memoryService.SwapUsagePercentage;
            TxtSwapUsed.Text = $"{_memoryService.FormattedSwapUsed} ({_memoryService.SwapUsagePercentage:F0}%)";
            TxtSwapTotal.Text = _memoryService.FormattedSwapTotal;
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            Hide();
        }
    }
}
