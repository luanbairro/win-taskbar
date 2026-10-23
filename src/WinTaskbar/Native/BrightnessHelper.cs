using System;
using System.Management;

namespace WinTaskbar.Native
{
    public static class BrightnessHelper
    {
        public static int GetBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToInt32(obj["CurrentBrightness"]);
                }
            }
            catch { }
            return 100;
        }
    }
}
