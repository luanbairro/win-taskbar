using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WinTaskbar.Models;
using WinTaskbar.Native;

namespace WinTaskbar.Services
{
    public class MediaService : IMediaService
    {
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly MediaTrackInfo _trackInfo = new();

        public MediaTrackInfo CurrentTrack => _trackInfo;
        public event EventHandler? TrackChanged;

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public MediaService()
        {
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_manager != null)
                {
                    _manager.CurrentSessionChanged += (s, e) => _ = UpdateAsync();
                }
            }
            catch { }

            await UpdateAsync();
        }

        public async Task UpdateAsync()
        {
            try
            {
                if (_manager != null)
                {
                    var sessions = _manager.GetSessions();
                    // Prefer Spotify session if present
                    _currentSession = sessions.FirstOrDefault(s =>
                        s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                        ?? _manager.GetCurrentSession();

                    if (_currentSession != null)
                    {
                        var playbackInfo = _currentSession.GetPlaybackInfo();
                        bool isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                        var props = await _currentSession.TryGetMediaPropertiesAsync();
                        if (props != null && !string.IsNullOrWhiteSpace(props.Title))
                        {
                            _trackInfo.Title = props.Title;
                            _trackInfo.Artist = props.Artist ?? string.Empty;
                            _trackInfo.Album = props.AlbumTitle ?? string.Empty;
                            _trackInfo.IsPlaying = isPlaying;
                            _trackInfo.HasMedia = true;
                            _trackInfo.SourceApp = _currentSession.SourceAppUserModelId ?? "Mídia";

                            // Load Album Art Thumbnail
                            if (props.Thumbnail != null)
                            {
                                try
                                {
                                    using var stream = await props.Thumbnail.OpenReadAsync();
                                    using var netStream = stream.AsStreamForRead();
                                    var memStream = new MemoryStream();
                                    await netStream.CopyToAsync(memStream);
                                    memStream.Position = 0;

                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = memStream;
                                    bitmap.EndInit();
                                    bitmap.Freeze();

                                    _trackInfo.Thumbnail = bitmap;
                                }
                                catch { }
                            }

                            TrackChanged?.Invoke(this, EventArgs.Empty);
                            return;
                        }
                    }
                }
            }
            catch { }

            // Fallback: Check Spotify process directly
            CheckSpotifyProcessFallback();
        }

        private void CheckSpotifyProcessFallback()
        {
            try
            {
                var spotifyProcs = Process.GetProcessesByName("Spotify");
                foreach (var p in spotifyProcs)
                {
                    string title = p.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title) &&
                        !title.Equals("Spotify", StringComparison.OrdinalIgnoreCase) &&
                        !title.Equals("Spotify Free", StringComparison.OrdinalIgnoreCase) &&
                        !title.Equals("Spotify Premium", StringComparison.OrdinalIgnoreCase))
                    {
                        int dashIdx = title.IndexOf(" - ");
                        if (dashIdx > 0)
                        {
                            _trackInfo.Artist = title.Substring(0, dashIdx).Trim();
                            _trackInfo.Title = title.Substring(dashIdx + 3).Trim();
                        }
                        else
                        {
                            _trackInfo.Artist = "Spotify";
                            _trackInfo.Title = title;
                        }

                        _trackInfo.IsPlaying = true;
                        _trackInfo.HasMedia = true;
                        _trackInfo.SourceApp = "Spotify";
                        TrackChanged?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                }

                if (spotifyProcs.Length > 0)
                {
                    _trackInfo.Title = "Spotify";
                    _trackInfo.Artist = "Pausado";
                    _trackInfo.IsPlaying = false;
                    _trackInfo.HasMedia = true;
                    _trackInfo.SourceApp = "Spotify";
                    TrackChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
            catch { }

            // No active media
            if (_trackInfo.HasMedia)
            {
                _trackInfo.HasMedia = false;
                _trackInfo.IsPlaying = false;
                TrackChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task TogglePlayPauseAsync()
        {
            try
            {
                if (_currentSession != null)
                {
                    await _currentSession.TryTogglePlayPauseAsync();
                    await Task.Delay(200);
                    await UpdateAsync();
                    return;
                }
            }
            catch { }

            // Fallback: Send media key
            SendMediaKey(VK_MEDIA_PLAY_PAUSE);
            await Task.Delay(200);
            await UpdateAsync();
        }

        public async Task NextAsync()
        {
            try
            {
                if (_currentSession != null)
                {
                    await _currentSession.TrySkipNextAsync();
                    await Task.Delay(200);
                    await UpdateAsync();
                    return;
                }
            }
            catch { }

            SendMediaKey(VK_MEDIA_NEXT_TRACK);
            await Task.Delay(200);
            await UpdateAsync();
        }

        public async Task PreviousAsync()
        {
            try
            {
                if (_currentSession != null)
                {
                    await _currentSession.TrySkipPreviousAsync();
                    await Task.Delay(200);
                    await UpdateAsync();
                    return;
                }
            }
            catch { }

            SendMediaKey(VK_MEDIA_PREV_TRACK);
            await Task.Delay(200);
            await UpdateAsync();
        }

        public void OpenSpotify()
        {
            try
            {
                var procs = Process.GetProcessesByName("Spotify");
                if (procs.Length > 0 && procs[0].MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(procs[0].MainWindowHandle, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(procs[0].MainWindowHandle);
                    return;
                }

                string spotifyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Spotify\Spotify.exe");

                if (File.Exists(spotifyPath))
                {
                    Process.Start(new ProcessStartInfo(spotifyPath) { UseShellExecute = true });
                    return;
                }

                // If not found in roaming, launch URI protocol
                Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open Spotify: {ex.Message}");
            }
        }

        private static void SendMediaKey(byte key)
        {
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 2, UIntPtr.Zero);
        }
    }
}
