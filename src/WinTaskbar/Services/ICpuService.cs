using System;
using System.Collections.Generic;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface ICpuService
    {
        string CpuName { get; }
        double TotalUsage { get; }
        double UserUsage { get; }
        double SystemUsage { get; }
        TimeSpan Uptime { get; }
        IReadOnlyList<double> History { get; }
        IReadOnlyList<ProcessItem> TopProcesses { get; }
        void Update();
    }
}
