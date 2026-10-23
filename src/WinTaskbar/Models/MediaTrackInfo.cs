using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WinTaskbar.Models
{
    public class MediaTrackInfo : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _artist = string.Empty;
        private string _album = string.Empty;
        private bool _isPlaying;
        private bool _hasMedia;
        private ImageSource? _thumbnail;
        private string _sourceApp = string.Empty;
        private TimeSpan _position = TimeSpan.Zero;
        private TimeSpan _duration = TimeSpan.Zero;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); }
        }

        public string Artist
        {
            get => _artist;
            set { _artist = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); }
        }

        public string Album
        {
            get => _album;
            set { _album = value; OnPropertyChanged(); }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        public bool HasMedia
        {
            get => _hasMedia;
            set { _hasMedia = value; OnPropertyChanged(); }
        }

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        public string SourceApp
        {
            get => _sourceApp;
            set { _sourceApp = value; OnPropertyChanged(); }
        }

        public TimeSpan Position
        {
            get => _position;
            set
            {
                _position = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionFormatted));
                OnPropertyChanged(nameof(RemainingFormatted));
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationFormatted));
                OnPropertyChanged(nameof(RemainingFormatted));
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        public string PositionFormatted => Position.ToString(@"mm\:ss");

        public string DurationFormatted => Duration.ToString(@"mm\:ss");

        public string RemainingFormatted
        {
            get
            {
                if (Duration <= TimeSpan.Zero) return "-00:00";
                var rem = Duration - Position;
                if (rem < TimeSpan.Zero) rem = TimeSpan.Zero;
                return $"-{rem:mm\\:ss}";
            }
        }

        public double ProgressPercentage
        {
            get
            {
                if (Duration.TotalSeconds <= 0) return 0.0;
                return Math.Clamp((Position.TotalSeconds / Duration.TotalSeconds) * 100.0, 0.0, 100.0);
            }
        }

        public string DisplayTitle
        {
            get
            {
                if (!HasMedia) return "Nenhuma música";
                if (!string.IsNullOrWhiteSpace(Artist)) return $"{Artist} - {Title}";
                return Title;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
