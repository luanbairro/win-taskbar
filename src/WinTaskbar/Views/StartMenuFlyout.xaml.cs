using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinTaskbar.Models;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class StartMenuFlyout : Window
    {
        private readonly IAppLauncherService _launcherService;

        public StartMenuFlyout(IAppLauncherService launcherService)
        {
            InitializeComponent();
            _launcherService = launcherService;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TaskbarHelper.ApplyWindows11Styling(this, roundCorners: true);
            TaskbarHelper.SetToolWindow(this);

            TxtUserName.Text = Environment.UserName;
            RefreshContent();
        }

        public void FocusSearch()
        {
            TxtSearch.Text = string.Empty;
            TxtSearch.Focus();
        }

        public void RefreshContent()
        {
            var pinned = _launcherService.GetPinnedApps();
            ItemsPinnedApps.ItemsSource = pinned;

            if (string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                ScrollDefaultContent.Visibility = Visibility.Visible;
                ListSearchResults.Visibility = Visibility.Collapsed;
                TxtSearchPlaceholder.Visibility = Visibility.Visible;
            }
            else
            {
                PerformSearch(TxtSearch.Text);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text;
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(query))
            {
                ScrollDefaultContent.Visibility = Visibility.Visible;
                ListSearchResults.Visibility = Visibility.Collapsed;
            }
            else
            {
                PerformSearch(query);
            }
        }

        private void PerformSearch(string query)
        {
            var results = _launcherService.SearchApps(query);
            ListSearchResults.ItemsSource = results;
            ScrollDefaultContent.Visibility = Visibility.Collapsed;
            ListSearchResults.Visibility = Visibility.Visible;

            if (results.Count > 0)
            {
                ListSearchResults.SelectedIndex = 0;
            }
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ListSearchResults.SelectedItem is StartMenuAppItem item)
                {
                    LaunchAndClose(item.Path);
                }
                else if (ListSearchResults.Items.Count > 0 && ListSearchResults.Items[0] is StartMenuAppItem firstItem)
                {
                    LaunchAndClose(firstItem.Path);
                }
                else if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                {
                    LaunchAndClose(TxtSearch.Text);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down && ListSearchResults.Items.Count > 0)
            {
                ListSearchResults.Focus();
            }
        }

        private void ListSearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListSearchResults.SelectedItem is StartMenuAppItem item)
            {
                LaunchAndClose(item.Path);
            }
        }

        private void BtnAppItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DockAppItem item)
            {
                LaunchAndClose(item.ExecutablePath, item.Arguments);
            }
        }

        private void BtnAllApps_Click(object sender, MouseButtonEventArgs e)
        {
            PerformSearch(" ");
            TxtSearch.Focus();
        }

        private void BtnExplorer_Click(object sender, RoutedEventArgs e) => LaunchAndClose("explorer.exe");
        private void BtnTerminal_Click(object sender, RoutedEventArgs e) => LaunchAndClose("wt.exe");
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => LaunchAndClose("ms-settings:");
        private void BtnTaskMgr_Click(object sender, RoutedEventArgs e) => LaunchAndClose("taskmgr.exe");

        private void BtnLock_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _launcherService.Lock();
        }

        private void BtnSleep_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _launcherService.Sleep();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Deseja realmente reiniciar o computador?", "Reiniciar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Hide();
                _launcherService.Restart();
            }
        }

        private void BtnShutdown_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Deseja realmente desligar o computador?", "Desligar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Hide();
                _launcherService.Shutdown();
            }
        }

        private void LaunchAndClose(string path, string? args = null)
        {
            Hide();
            _launcherService.Launch(path, args);
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            Hide();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }
    }
}
