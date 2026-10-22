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

        public static ImageSource? GetIconForWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            try
            {
                // 1. WM_GETICON
                IntPtr hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_BIG, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeMethods.SendMessage(hWnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_SMALL, IntPtr.Zero);
                }

                // 2. Class Long
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_HICON);
                }
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCL_HICONSM);
                }

                if (hIcon != IntPtr.Zero)
                {
                    return CreateImageSourceFromHIcon(hIcon);
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
