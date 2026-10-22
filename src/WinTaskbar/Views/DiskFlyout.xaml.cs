using System;
using System.Windows;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class DiskFlyout : Window
    {
        private readonly IDiskService _diskService;

        public DiskFlyout(IDiskService diskService)
        {
            InitializeComponent();
            _diskService = diskService;
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
            TxtGlobalRead.Text = _diskService.FormattedGlobalRead;
            TxtGlobalWrite.Text = _diskService.FormattedGlobalWrite;

            DiskChart.PrimaryValues = _diskService.ReadHistory;
            DiskChart.SecondaryValues = _diskService.WriteHistory;
            DiskChart.InvalidateVisual();

            ListDrives.ItemsSource = null;
            ListDrives.ItemsSource = _diskService.Drives;
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            Hide();
        }
    }
}
