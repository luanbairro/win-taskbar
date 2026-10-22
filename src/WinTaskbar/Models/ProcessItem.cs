using System;

namespace WinTaskbar.Models
{
    public class ProcessItem
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public double CpuPercentage { get; set; }
        public long MemoryBytes { get; set; }

        public string FormattedCpu => $"{CpuPercentage:F1}%";

        public string FormattedMemory
        {
            get
            {
                if (MemoryBytes >= 1024L * 1024L * 1024L)
                    return $"{(double)MemoryBytes / (1024 * 1024 * 1024):F2} GB";
                if (MemoryBytes >= 1024L * 1024L)
                    return $"{(double)MemoryBytes / (1024 * 1024):F1} MB";
                return $"{(double)MemoryBytes / 1024:F0} KB";
            }
        }
    }
}
