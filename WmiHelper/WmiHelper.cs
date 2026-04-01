using System;
using System.Management;
using System.Runtime.InteropServices;

/// <summary>
/// Standalone helper that queries Thunderobot WMI for hardware info.
/// Output: CPU_TEMP GPU_TEMP CPU_RPM GPU_RPM
/// </summary>
class WmiHelper
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SmiStruct
    {
        public ushort a0, a1;
        public uint a2, a3, a4, a5, a6, rev0, rev1;
    }

    static int Main()
    {
        try
        {
            var scope = new ManagementScope("root\\wmi");
            var query = new SelectQuery("RW_GMWMI");

            // Build command: a0=0xFA00, a1=0x0200 (GetHardwareInformation)
            var cmd = new SmiStruct { a0 = 0xFA00, a1 = 0x0200 };
            int size = Marshal.SizeOf(cmd);
            byte[] cmdBytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(cmd, ptr, false);
            Marshal.Copy(ptr, cmdBytes, 0, size);
            Marshal.FreeHGlobal(ptr);

            // Write
            using (var searcher = new ManagementObjectSearcher(scope, query))
            using (var results = searcher.Get())
                foreach (ManagementObject obj in results)
                { obj.SetPropertyValue("BufferBytes", cmdBytes); obj.Put(); break; }

            // Read
            using (var searcher = new ManagementObjectSearcher(scope, query))
            using (var results = searcher.Get())
                foreach (ManagementObject obj in results)
                {
                    byte[] resp = (byte[])obj["BufferBytes"];
                    ptr = Marshal.AllocHGlobal(resp.Length);
                    Marshal.Copy(resp, 0, ptr, resp.Length);
                    var r = (SmiStruct)Marshal.PtrToStructure(ptr, typeof(SmiStruct));
                    Marshal.FreeHGlobal(ptr);

                    Console.WriteLine($"{r.a2 & 0xFF} {r.a3 & 0xFF} {r.a4 & 0xFFFF} {r.a5 & 0xFFFF}");
                    return 0;
                }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
        return 1;
    }
}
