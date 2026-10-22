using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WinTaskbar.Models;
using WinTaskbar.Native;

namespace WinTaskbar.Services
{
    public class MemoryService : IMemoryService
    {
        private readonly List<double> _history = new();
        private const int MaxHistory = 60;

        public ulong TotalBytes { get; private set; }
        public ulong FreeBytes { get; private set; }
        public ulong UsedBytes { get; private set; }
        public double UsagePercentage { get; private set; }
        public ulong TotalSwapBytes { get; private set; }
        public ulong FreeSwapBytes { get; private set; }
        public ulong UsedSwapBytes { get; private set; }
        public double SwapUsagePercentage { get; private set; }

        public string FormattedTotal => FormatGb(TotalBytes);
        public string FormattedUsed => FormatGb(UsedBytes);
        public string FormattedFree => FormatGb(FreeBytes);
        public string FormattedSwapTotal => FormatGb(TotalSwapBytes);
        public string FormattedSwapUsed => FormatGb(UsedSwapBytes);

        public IReadOnlyList<double> History => _history;
        public IReadOnlyList<ProcessItem> TopProcesses { get; private set; } = new List<ProcessItem>();

        public void Update()
        {
            var mem = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(mem))
            {
                TotalBytes = mem.ullTotalPhys;
                FreeBytes = mem.ullAvailPhys;
                UsedBytes = TotalBytes > FreeBytes ? TotalBytes - FreeBytes : 0;
                UsagePercentage = TotalBytes > 0 ? ((double)UsedBytes / TotalBytes) * 100.0 : 0;

                TotalSwapBytes = mem.ullTotalPageFile;
                FreeSwapBytes = mem.ullAvailPageFile;
                UsedSwapBytes = TotalSwapBytes > FreeSwapBytes ? TotalSwapBytes - FreeSwapBytes : 0;
                SwapUsagePercentage = TotalSwapBytes > 0 ? ((double)UsedSwapBytes / TotalSwapBytes) * 100.0 : 0;
            }

            _history.Add(UsagePercentage);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }

            UpdateTopProcesses();
        }

        private void UpdateTopProcesses()
        {
            try
            {
                TopProcesses = Process.GetProcesses()
                    .Select(p =>
                    {
                        try
                        {
                            return new ProcessItem
                            {
                                Id = p.Id,
                                Name = p.ProcessName,
                                MemoryBytes = p.WorkingSet64
                            };
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(p => p != null && p.MemoryBytes > 10 * 1024 * 1024)
                    .OrderByDescending(p => p!.MemoryBytes)
                    .Take(5)
                    .Select(p => p!)
                    .ToList();
            }
            catch { }
        }

        private static string FormatGb(ulong bytes)
        {
            return $"{(double)bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}
