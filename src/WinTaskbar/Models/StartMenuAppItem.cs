using System.Windows.Media;

namespace WinTaskbar.Models
{
    public class StartMenuAppItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
        public bool IsPinned { get; set; }
    }
}
