using System.Collections.Generic;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface IDiskService
    {
        double GlobalReadBytesPerSec { get; }
        double GlobalWriteBytesPerSec { get; }
        string FormattedGlobalRead { get; }
        string FormattedGlobalWrite { get; }
        IReadOnlyList<DiskDeviceItem> Drives { get; }
        IReadOnlyList<double> ReadHistory { get; }
        IReadOnlyList<double> WriteHistory { get; }
        void Update();
    }
}
