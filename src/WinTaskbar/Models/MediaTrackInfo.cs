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
