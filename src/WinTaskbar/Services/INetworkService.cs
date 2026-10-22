using System.Collections.Generic;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public interface INetworkService
    {
        double GlobalUploadBytesPerSec { get; }
        double GlobalDownloadBytesPerSec { get; }
        string FormattedUpload { get; }
        string FormattedDownload { get; }
        string PublicIp { get; }
        string DefaultGateway { get; }
        IReadOnlyList<NetworkInterfaceItem> Interfaces { get; }
        IReadOnlyList<double> DownloadHistory { get; }
        IReadOnlyList<double> UploadHistory { get; }
        void Update();
        void RefreshPublicIpAsync();
    }
}
