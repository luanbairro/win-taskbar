using System;

namespace WinTaskbar.Models
{
    public class NetworkInterfaceItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IpAddress { get; set; } = "-";
        public string InterfaceType { get; set; } = "Network";
        public bool IsWireless { get; set; }
        public bool IsUp { get; set; }

        public double UploadBytesPerSec { get; set; }
        public double DownloadBytesPerSec { get; set; }

        public string FormattedUpload => FormatSpeed(UploadBytesPerSec);
        public string FormattedDownload => FormatSpeed(DownloadBytesPerSec);

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024)
                return $"{bytesPerSec / (1024 * 1024):F1} MB/s";
            if (bytesPerSec >= 1024)
                return $"{bytesPerSec / 1024:F1} KB/s";
            return $"{bytesPerSec:F0} B/s";
        }
    }
}
