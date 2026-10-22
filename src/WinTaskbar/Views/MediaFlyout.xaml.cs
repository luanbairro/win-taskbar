using System;
using System.Windows;
using System.Windows.Input;
using WinTaskbar.Native;
using WinTaskbar.Services;

namespace WinTaskbar.Views
{
    public partial class MediaFlyout : Window
    {
        private readonly IMediaService _mediaService;

        public MediaFlyout(IMediaService mediaService)
        {
            InitializeComponent();
            _mediaService = mediaService;
            _mediaService.TrackChanged += (s, e) => Dispatcher.Invoke(UpdateView);

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TaskbarHelper.ApplyWindows11Styling(this, roundCorners: true);
            TaskbarHelper.SetToolWindow(this);
            UpdateView();
        }

        public void UpdateView()
        {
            var track = _mediaService.CurrentTrack;

            if (track.HasMedia)
            {
                TxtTitle.Text = !string.IsNullOrWhiteSpace(track.Title) ? track.Title : "Mídia em reprodução";
                TxtArtist.Text = !string.IsNullOrWhiteSpace(track.Artist) ? track.Artist : "Artista desconhecido";
                TxtAlbum.Text = track.Album;
                TxtAppHeader.Text = track.SourceApp.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ? "Spotify" : track.SourceApp;
                ImgThumbnail.Source = track.Thumbnail;
            }
            else
            {
                TxtTitle.Text = "Nenhuma música tocando";
                TxtArtist.Text = "Abra o Spotify para reproduzir";
                TxtAlbum.Text = string.Empty;
                TxtAppHeader.Text = "Spotify";
                ImgThumbnail.Source = null;
            }

            TxtPlayPauseIcon.Text = track.IsPlaying ? "⏸" : "▶";
            TxtPlayPauseIcon.Margin = track.IsPlaying ? new Thickness(0) : new Thickness(2, 0, 0, 0);
        }

        private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.PreviousAsync();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.NextAsync();
        }

        private void BtnOpenApp_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _mediaService.OpenSpotify();
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
