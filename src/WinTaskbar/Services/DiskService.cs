using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public class DiskService : IDiskService
    {
        private readonly List<double> _readHistory = new();
        private readonly List<double> _writeHistory = new();
        private const int MaxHistory = 60;

        private PerformanceCounter? _diskReadCounter;
        private PerformanceCounter? _diskWriteCounter;
        private List<DiskDeviceItem> _drives = new();

        public double GlobalReadBytesPerSec { get; private set; }
        public double GlobalWriteBytesPerSec { get; private set; }
        public string FormattedGlobalRead => FormatSpeed(GlobalReadBytesPerSec);
        public string FormattedGlobalWrite => FormatSpeed(GlobalWriteBytesPerSec);
        public IReadOnlyList<DiskDeviceItem> Drives => _drives;
        public IReadOnlyList<double> ReadHistory => _readHistory;
        public IReadOnlyList<double> WriteHistory => _writeHistory;

        public DiskService()
        {
            try
            {
                _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                _diskReadCounter.NextValue();
                _diskWriteCounter.NextValue();
            }
            catch { }

            RefreshDrives();
        }

        public void Update()
        {
            try
            {
                GlobalReadBytesPerSec = _diskReadCounter?.NextValue() ?? 0;
                GlobalWriteBytesPerSec = _diskWriteCounter?.NextValue() ?? 0;
            }
            catch
            {
                GlobalReadBytesPerSec = 0;
                GlobalWriteBytesPerSec = 0;
            }

            _readHistory.Add(GlobalReadBytesPerSec);
            _writeHistory.Add(GlobalWriteBytesPerSec);

            if (_readHistory.Count > MaxHistory) _readHistory.RemoveAt(0);
            if (_writeHistory.Count > MaxHistory) _writeHistory.RemoveAt(0);

            RefreshDrives();
        }

        private void RefreshDrives()
        {
            var list = new List<DiskDeviceItem>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (!d.IsReady) continue;
                    list.Add(new DiskDeviceItem
                    {
                        DriveLetter = d.Name.TrimEnd('\\'),
                        Name = string.IsNullOrWhiteSpace(d.VolumeLabel) ? $"Disco ({d.Name.TrimEnd('\\')})" : d.VolumeLabel,
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.AvailableFreeSpace,
                        ReadBytesPerSec = GlobalReadBytesPerSec,
                        WriteBytesPerSec = GlobalWriteBytesPerSec
                    });
                }
            }
            catch { }

            _drives = list;
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
