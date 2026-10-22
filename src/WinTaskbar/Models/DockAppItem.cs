using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WinTaskbar.Models
{
    public class DockAppItem : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _isRunning;
        private bool _isActive;
        private ImageSource? _icon;
        private int _badgeCount;
        private double _scale = 1.0;
        private bool _showSeparatorBefore;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public bool IsPinned { get; set; }

        public IntPtr Hwnd { get; set; } = IntPtr.Zero;
        public int ProcessId { get; set; }
        public List<IntPtr> OpenHwnds { get; } = new();

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public bool ShowSeparatorBefore
        {
            get => _showSeparatorBefore;
            set { _showSeparatorBefore = value; OnPropertyChanged(); }
        }

        public int BadgeCount
        {
            get => _badgeCount;
            set
            {
                if (_badgeCount != value)
                {
                    _badgeCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasBadge));
                    OnPropertyChanged(nameof(BadgeText));
                }
            }
        }

        public bool HasBadge => _badgeCount > 0;
        public string BadgeText => _badgeCount > 99 ? "99+" : _badgeCount.ToString();

        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) > 0.001)
                {
                    _scale = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
