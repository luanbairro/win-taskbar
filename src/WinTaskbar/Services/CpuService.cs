using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32;
using WinTaskbar.Models;

namespace WinTaskbar.Services
{
    public class CpuService : ICpuService
    {
        private readonly List<double> _history = new();
        private const int MaxHistory = 60;
        private PerformanceCounter? _cpuTotalCounter;
        private PerformanceCounter? _cpuUserCounter;
        private PerformanceCounter? _cpuPrivilegedCounter;
        private readonly Dictionary<int, TimeSpan> _lastProcessTimes = new();
        private DateTime _lastProcessSampleTime = DateTime.UtcNow;

        public string CpuName { get; private set; } = "CPU";
        public double TotalUsage { get; private set; }
        public double UserUsage { get; private set; }
        public double SystemUsage { get; private set; }
        public TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);
        public IReadOnlyList<double> History => _history;
        public IReadOnlyList<ProcessItem> TopProcesses { get; private set; } = new List<ProcessItem>();

        public CpuService()
        {
            InitializeCpuName();
            InitializeCounters();
        }

        private void InitializeCpuName()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string regName && !string.IsNullOrWhiteSpace(regName))
                {
                    CpuName = regName.Trim();
                    return;
                }
            }
            catch { }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    CpuName = obj["Name"]?.ToString()?.Trim() ?? "CPU";
                    break;
                }
            }
            catch
            {
                CpuName = "Intel / AMD Processor";
            }
        }

        private void InitializeCounters()
        {
            try
            {
                _cpuTotalCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuUserCounter = new PerformanceCounter("Processor", "% User Time", "_Total", true);
                _cpuPrivilegedCounter = new PerformanceCounter("Processor", "% Privileged Time", "_Total", true);
                _cpuTotalCounter.NextValue();
                _cpuUserCounter.NextValue();
                _cpuPrivilegedCounter.NextValue();
            }
            catch { }
        }

        public void Update()
        {
            try
            {
                if (_cpuTotalCounter != null)
                {
                    TotalUsage = Math.Clamp(_cpuTotalCounter.NextValue(), 0, 100);
                    UserUsage = Math.Clamp(_cpuUserCounter?.NextValue() ?? (TotalUsage * 0.7), 0, 100);
                    SystemUsage = Math.Clamp(_cpuPrivilegedCounter?.NextValue() ?? (TotalUsage * 0.3), 0, 100);
                }
                else
                {
                    TotalUsage = 0;
                }
            }
            catch
            {
                TotalUsage = 0;
            }

            _history.Add(TotalUsage);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }

            UpdateTopProcesses();
        }

        private void UpdateTopProcesses()
        {
            var now = DateTime.UtcNow;
            var elapsedSecs = (now - _lastProcessSampleTime).TotalSeconds;
            if (elapsedSecs < 0.5) return;

            var processList = new List<ProcessItem>();
            int coreCount = Math.Max(1, Environment.ProcessorCount);

            try
            {
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        var totalTime = p.TotalProcessorTime;
                        double cpuPct = 0;

                        if (_lastProcessTimes.TryGetValue(p.Id, out var prevTime))
                        {
                            var timeDelta = (totalTime - prevTime).TotalSeconds;
                            cpuPct = (timeDelta / (elapsedSecs * coreCount)) * 100.0;
                        }

                        _lastProcessTimes[p.Id] = totalTime;

                        if (cpuPct > 0.05 || processList.Count < 5)
                        {
                            processList.Add(new ProcessItem
                            {
                                Id = p.Id,
                                Name = p.ProcessName,
                                CpuPercentage = Math.Clamp(cpuPct, 0, 100),
                                MemoryBytes = p.WorkingSet64
                            });
                        }
                    }
                    catch { }
                }

                TopProcesses = processList
                    .OrderByDescending(p => p.CpuPercentage)
                    .Take(5)
                    .ToList();
            }
            catch { }

            _lastProcessSampleTime = now;
        }
    }
}
