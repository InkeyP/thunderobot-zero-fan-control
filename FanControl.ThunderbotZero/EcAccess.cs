using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace FanControl.ThunderbotZero
{
    /// <summary>
    /// Thunderobot ZERO hardware access — hybrid approach:
    ///   READ:  WMI (in-process, fast) for temps + RPMs
    ///   WRITE: ec-probe.exe (EC registers 0x2C/0x2D, only on user action)
    ///
    /// WMI SetFanSpeed doesn't persist (EC overrides within seconds),
    /// but direct EC register writes to 0x2C/0x2D do persist.
    /// </summary>
    internal static class EcAccess
    {
        private const string WMI_NAMESPACE = "root\\wmi";
        private const string WMI_CLASS = "RW_GMWMI";
        private static readonly string EcProbePath =
            @"C:\Program Files (x86)\NoteBook FanControl\ec-probe.exe";

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SmiStruct
        {
            public ushort a0, a1;
            public uint a2, a3, a4, a5, a6, rev0, rev1;
        }

        public struct HardwareInfo
        {
            public int CpuTemp, GpuTemp, CpuFanRpm, GpuFanRpm;
            public bool Valid;
        }

        // --- Caching ---
        private static HardwareInfo _cache;
        private static long _cacheTicksUtc;
        private static readonly long CacheTicks = TimeSpan.FromSeconds(2).Ticks;

        public static HardwareInfo GetHardwareInfoCached()
        {
            long now = DateTime.UtcNow.Ticks;
            if (_cache.Valid && (now - _cacheTicksUtc) < CacheTicks)
                return _cache;

            _cache = QueryHardwareInfo();
            _cacheTicksUtc = now;
            return _cache;
        }

        // --- WMI read: temps + RPMs ---
        private static HardwareInfo QueryHardwareInfo()
        {
            var info = new HardwareInfo();
            try
            {
                var resp = PerformSMI(0xFA00, 0x0200, 0, 0);
                if (resp.HasValue)
                {
                    var r = resp.Value;
                    info.CpuTemp = (int)(r.a2 & 0xFF);
                    info.GpuTemp = (int)(r.a3 & 0xFF);
                    info.CpuFanRpm = (int)(r.a4 & 0xFFFF);
                    info.GpuFanRpm = (int)(r.a5 & 0xFFFF);
                    info.Valid = info.CpuTemp > 0 && info.CpuTemp < 150;
                }
            }
            catch { }
            return info;
        }

        // --- EC register write via ec-probe (only called on user Set/Reset) ---
        public static void WriteEcRegister(byte register, byte value)
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
                    proc.WaitForExit(2000);
            }
            catch { }
        }

        // --- Core WMI communication ---
        private static SmiStruct? PerformSMI(ushort a0, ushort a1, uint a2, uint a3)
        {
            var cmd = new SmiStruct { a0 = a0, a1 = a1, a2 = a2, a3 = a3 };
            byte[] cmdBytes = StructToBytes(cmd);

            var scope = new ManagementScope(WMI_NAMESPACE);
            var query = new SelectQuery(WMI_CLASS);

            // Write command
            using (var searcher = new ManagementObjectSearcher(scope, query))
            using (var results = searcher.Get())
                foreach (ManagementObject obj in results)
                {
                    obj.SetPropertyValue("BufferBytes", cmdBytes);
                    obj.Put();
                    break;
                }

            // Read response
            using (var searcher = new ManagementObjectSearcher(scope, query))
            using (var results = searcher.Get())
                foreach (ManagementObject obj in results)
                {
                    byte[] resp = (byte[])obj["BufferBytes"];
                    if (resp != null && resp.Length >= 24)
                        return BytesToStruct(resp);
                    break;
                }

            return null;
        }

        private static byte[] StructToBytes(SmiStruct s)
        {
            int size = Marshal.SizeOf(s);
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(s, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally { Marshal.FreeHGlobal(ptr); }
            return bytes;
        }

        private static SmiStruct BytesToStruct(byte[] bytes)
        {
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                return (SmiStruct)Marshal.PtrToStructure(ptr, typeof(SmiStruct));
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
    }
}
