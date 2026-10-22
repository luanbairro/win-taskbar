using System.Collections.Generic;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface IMemoryService
    {
        ulong TotalBytes { get; }
        ulong FreeBytes { get; }
        ulong UsedBytes { get; }
        double UsagePercentage { get; }
        ulong TotalSwapBytes { get; }
        ulong FreeSwapBytes { get; }
        ulong UsedSwapBytes { get; }
        double SwapUsagePercentage { get; }

        string FormattedTotal { get; }
        string FormattedUsed { get; }
        string FormattedFree { get; }
        string FormattedSwapTotal { get; }
        string FormattedSwapUsed { get; }

        IReadOnlyList<double> History { get; }
        IReadOnlyList<ProcessItem> TopProcesses { get; }
        void Update();
    }
}
