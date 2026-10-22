using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinTaskbar.Native
{
    public static class IconHelper
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource? GetIconForFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            if (_iconCache.TryGetValue(filePath, out var cached))
                return cached;

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    var source = CreateImageSourceFromHIcon(icon.Handle);
                    if (source != null)
                    {
                        source.Freeze();
                        _iconCache[filePath] = source;
                        return source;
                    }
                }
            }
            catch { }

            return null;
        }

        public static ImageSource? CreateImageSourceFromHIcon(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero)
                return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
        }
    }
}
