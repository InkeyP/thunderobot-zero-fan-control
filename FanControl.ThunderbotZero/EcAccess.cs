using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FanControl.ThunderbotZero
{
    internal static class EcAccess
    {
        private static readonly string EcProbePath =
            @"C:\Program Files (x86)\NoteBook FanControl\ec-probe.exe";

        private static string _wmiHelperPath;

        public const byte REG_FAN1_WRITE = 0x2C;
        public const byte REG_FAN2_WRITE = 0x2D;
        public const byte REG_FAN1_READ  = 0x2E;
        public const byte REG_FAN2_READ  = 0x2F;
        public const byte AUTO_VALUE     = 0xFF;

        public struct HardwareInfo
        {
            public int CpuTemp;
            public int GpuTemp;
            public int CpuFanRpm;
            public int GpuFanRpm;
            public bool Valid;
        }

        // Cached WMI result with timestamp
        private static HardwareInfo _cachedInfo;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Get hardware info with 1-second caching.
        /// Multiple sensors calling this within the same update cycle share one WmiHelper call.
        /// </summary>
        public static HardwareInfo GetHardwareInfoCached()
        {
            if ((DateTime.UtcNow - _cacheTime) < CacheDuration && _cachedInfo.Valid)
                return _cachedInfo;

            _cachedInfo = GetHardwareInfoWmi();
            _cacheTime = DateTime.UtcNow;
            return _cachedInfo;
        }

        private static string GetWmiHelperPath()
        {
            if (_wmiHelperPath != null && File.Exists(_wmiHelperPath))
                return _wmiHelperPath;

            // Look next to the plugin DLL
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var candidate = Path.Combine(dir, "WmiHelper.exe");
                if (File.Exists(candidate))
                {
                    _wmiHelperPath = candidate;
                    return _wmiHelperPath;
                }
            }
            catch { }

            // Fallback: FanControl Plugins directory
            _wmiHelperPath = @"D:\FanControl\Plugins\WmiHelper.exe";
            return _wmiHelperPath;
        }

        private static HardwareInfo GetHardwareInfoWmi()
        {
            var info = new HardwareInfo();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = GetWmiHelperPath(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    var output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(3000);
                    var parts = output.Split(' ');
                    if (parts.Length >= 4)
                    {
                        info.CpuTemp = int.Parse(parts[0]);
                        info.GpuTemp = int.Parse(parts[1]);
                        info.CpuFanRpm = int.Parse(parts[2]);
                        info.GpuFanRpm = int.Parse(parts[3]);
                        info.Valid = true;
                    }
                }
            }
            catch { }
            return info;
        }

        public static int? ReadRegister(byte register)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = EcProbePath,
                    Arguments = $"read 0x{register:X2}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(2000);
                    var match = Regex.Match(output, @"^(\d+)");
                    if (match.Success)
                        return int.Parse(match.Groups[1].Value);
                }
            }
            catch { }
            return null;
        }

        public static void WriteRegister(byte register, byte value)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = EcProbePath,
                    Arguments = $"write 0x{register:X2} {value}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(2000);
                }
            }
            catch { }
        }
    }
}
