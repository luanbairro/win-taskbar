using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public class NetworkService : INetworkService
    {
        private readonly List<double> _downloadHistory = new();
        private readonly List<double> _uploadHistory = new();
        private const int MaxHistory = 60;

        private readonly Dictionary<string, (long bytesSent, long bytesReceived, DateTime timestamp)> _previousStats = new();
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
        private List<NetworkInterfaceItem> _interfaces = new();
        private DateTime _lastPublicIpCheck = DateTime.MinValue;

        public double GlobalUploadBytesPerSec { get; private set; }
        public double GlobalDownloadBytesPerSec { get; private set; }
        public string FormattedUpload => FormatSpeed(GlobalUploadBytesPerSec);
        public string FormattedDownload => FormatSpeed(GlobalDownloadBytesPerSec);
        public string PublicIp { get; private set; } = "Obtendo...";
        public string DefaultGateway { get; private set; } = "-";
        public IReadOnlyList<NetworkInterfaceItem> Interfaces => _interfaces;
        public IReadOnlyList<double> DownloadHistory => _downloadHistory;
        public IReadOnlyList<double> UploadHistory => _uploadHistory;

        public NetworkService()
        {
            RefreshPublicIpAsync();
        }

        public void Update()
        {
            double totalSentRate = 0;
            double totalRecvRate = 0;
            var now = DateTime.UtcNow;
            var updatedInterfaces = new List<NetworkInterfaceItem>();

            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var stats = nic.GetIPStatistics();
                    long currentSent = stats.BytesSent;
                    long currentRecv = stats.BytesReceived;

                    double sentRate = 0;
                    double recvRate = 0;

                    if (_previousStats.TryGetValue(nic.Id, out var prev))
                    {
                        var elapsed = (now - prev.timestamp).TotalSeconds;
                        if (elapsed > 0.2)
                        {
                            sentRate = Math.Max(0, (currentSent - prev.bytesSent) / elapsed);
                            recvRate = Math.Max(0, (currentRecv - prev.bytesReceived) / elapsed);
                        }
                    }

                    _previousStats[nic.Id] = (currentSent, currentRecv, now);

                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        totalSentRate += sentRate;
                        totalRecvRate += recvRate;
                    }

                    var ipProps = nic.GetIPProperties();
                    var unicast = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                    var gateway = ipProps.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (gateway != null && DefaultGateway == "-")
                    {
                        DefaultGateway = gateway.Address.ToString();
                    }

                    updatedInterfaces.Add(new NetworkInterfaceItem
                    {
                        Id = nic.Id,
                        Name = nic.Name,
                        Description = nic.Description,
                        IpAddress = unicast?.Address.ToString() ?? "-",
                        InterfaceType = nic.NetworkInterfaceType.ToString(),
                        IsWireless = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                        IsUp = nic.OperationalStatus == OperationalStatus.Up,
                        UploadBytesPerSec = sentRate,
                        DownloadBytesPerSec = recvRate
                    });
                }
            }
            catch { }

            GlobalUploadBytesPerSec = totalSentRate;
            GlobalDownloadBytesPerSec = totalRecvRate;

            _downloadHistory.Add(GlobalDownloadBytesPerSec);
            _uploadHistory.Add(GlobalUploadBytesPerSec);

            if (_downloadHistory.Count > MaxHistory) _downloadHistory.RemoveAt(0);
            if (_uploadHistory.Count > MaxHistory) _uploadHistory.RemoveAt(0);

            _interfaces = updatedInterfaces;

            // Auto-refresh public IP every 15 minutes
            if ((now - _lastPublicIpCheck).TotalMinutes >= 15)
            {
                RefreshPublicIpAsync();
            }
        }

        public async void RefreshPublicIpAsync()
        {
            _lastPublicIpCheck = DateTime.UtcNow;
            try
            {
                var ip = await _httpClient.GetStringAsync("https://api.ipify.org");
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    PublicIp = ip.Trim();
                }
            }
            catch
            {
                if (PublicIp == "Obtendo...") PublicIp = "Indisponível";
            }
        }

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
