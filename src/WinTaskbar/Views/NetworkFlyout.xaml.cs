using System;
using System.Windows;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class NetworkFlyout : Window
    {
        private readonly INetworkService _networkService;

        public NetworkFlyout(INetworkService networkService)
        {
            InitializeComponent();
            _networkService = networkService;
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
            TxtGlobalUpload.Text = _networkService.FormattedUpload;
            TxtGlobalDownload.Text = _networkService.FormattedDownload;
            TxtPublicIp.Text = _networkService.PublicIp;
            TxtGateway.Text = _networkService.DefaultGateway;

            NetChart.PrimaryValues = _networkService.DownloadHistory;
            NetChart.SecondaryValues = _networkService.UploadHistory;
            NetChart.InvalidateVisual();

            ListInterfaces.ItemsSource = null;
            ListInterfaces.ItemsSource = _networkService.Interfaces;
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            Hide();
        }
    }
}
