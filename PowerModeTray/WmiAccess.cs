using System;
using System.Management;
using System.Runtime.InteropServices;

namespace PowerModeTray
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SmiStruct
    {
        public ushort a0, a1;
        public uint a2, a3, a4, a5, a6, rev0, rev1;
    }

    internal static class WmiAccess
    {
        private const string NS = "root\\wmi";
        private const string CLS = "RW_GMWMI";

        public static SmiStruct? PerformSMI(ushort a0, ushort a1, uint a2 = 0, uint a3 = 0)
        {
            var cmd = new SmiStruct { a0 = a0, a1 = a1, a2 = a2, a3 = a3 };
            byte[] buf = ToBytes(cmd);

            var scope = new ManagementScope(NS);
            var query = new SelectQuery(CLS);

            using (var s = new ManagementObjectSearcher(scope, query))
            using (var r = s.Get())
                foreach (ManagementObject o in r)
                { o.SetPropertyValue("BufferBytes", buf); o.Put(); break; }

            using (var s = new ManagementObjectSearcher(scope, query))
            using (var r = s.Get())
                foreach (ManagementObject o in r)
                {
                    byte[] resp = (byte[])o["BufferBytes"];
                    if (resp?.Length >= 24) return FromBytes(resp);
                    break;
                }
            return null;
        }

        /// <summary>Set power mode: 0=High, 1=Gaming, 2=Office</summary>
        public static void SetPowerMode(uint mode)
        {
            PerformSMI(0xFB00, 0x0300, mode);
        }

        private const string REG_KEY = @"SOFTWARE\WOW6432Node\LeiShen\ControlCenter";

        /// <summary>Get current power mode from registry (same as ControlCenter)</summary>
        public static int GetPowerMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(REG_KEY))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("ModeBeforeDC");
                        if (val != null) return Convert.ToInt32(val);
                    }
                }
            }
            catch { }
            return -1;
        }

        /// <summary>Save power mode to registry for persistence</summary>
        public static void SavePowerMode(uint mode)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(REG_KEY))
                    key?.SetValue("ModeBeforeDC", mode, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { }
        }

        /// <summary>Get hardware info: temps + RPMs</summary>
        public static (int cpuT, int gpuT, int cpuRpm, int gpuRpm) GetHardwareInfo()
        {
            var r = PerformSMI(0xFA00, 0x0200);
            if (r.HasValue)
                return ((int)(r.Value.a2 & 0xFF), (int)(r.Value.a3 & 0xFF),
                        (int)(r.Value.a4 & 0xFFFF), (int)(r.Value.a5 & 0xFFFF));
            return (0, 0, 0, 0);
        }

        static byte[] ToBytes(SmiStruct s)
        {
            int sz = Marshal.SizeOf(s);
            byte[] b = new byte[sz];
            IntPtr p = Marshal.AllocHGlobal(sz);
            try { Marshal.StructureToPtr(s, p, false); Marshal.Copy(p, b, 0, sz); }
            finally { Marshal.FreeHGlobal(p); }
            return b;
        }

        static SmiStruct FromBytes(byte[] b)
        {
            IntPtr p = Marshal.AllocHGlobal(b.Length);
            try { Marshal.Copy(b, 0, p, b.Length); return (SmiStruct)Marshal.PtrToStructure(p, typeof(SmiStruct)); }
            finally { Marshal.FreeHGlobal(p); }
        }
    }
}
