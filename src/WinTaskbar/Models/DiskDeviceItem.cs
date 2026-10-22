using System;

namespace WinTaskbar.Models
{
    public class DiskDeviceItem
    {
        public string Name { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
        public double UsagePercentage => TotalBytes > 0 ? ((double)UsedBytes / TotalBytes) * 100.0 : 0;

        public double ReadBytesPerSec { get; set; }
        public double WriteBytesPerSec { get; set; }

        public string FormattedTotal => FormatBytes(TotalBytes);
        public string FormattedUsed => FormatBytes(UsedBytes);
        public string FormattedFree => FormatBytes(FreeBytes);

        public string FormattedReadSpeed => FormatSpeed(ReadBytesPerSec);
        public string FormattedWriteSpeed => FormatSpeed(WriteBytesPerSec);

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L * 1024L)
                return $"{(double)bytes / (1024L * 1024L * 1024L * 1024L):F1} TB";
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{(double)bytes / (1024 * 1024 * 1024):F1} GB";
            if (bytes >= 1024L * 1024L)
                return $"{(double)bytes / (1024 * 1024):F1} MB";
            return $"{(double)bytes / 1024:F0} KB";
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024)
                return $"{bytesPerSec / (1024 * 1024):F1} MB/s";
            if (bytesPerSec >= 1024)
                return $"{bytesPerSec / 1024:F0} KB/s";
            return $"{bytesPerSec:F0} B/s";
        }
    }
}
