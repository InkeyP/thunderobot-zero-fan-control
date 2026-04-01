using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FanControl.ThunderbotZero
{
    /// <summary>
    /// EC register access via ec-probe.exe from NBFC.
    /// </summary>
    internal static class EcAccess
    {
        private static readonly string EcProbePath =
            @"C:\Program Files (x86)\NoteBook FanControl\ec-probe.exe";

        // Thunderobot ZERO EC Register Map
        public const byte REG_FAN1_WRITE = 0x2C; // Fan1 duty (0-100=manual%, 0xFF=auto)
        public const byte REG_FAN2_WRITE = 0x2D; // Fan2 duty (0-100=manual%, 0xFF=auto)
        public const byte REG_FAN1_READ  = 0x2E; // Fan1 current speed %
        public const byte REG_FAN2_READ  = 0x2F; // Fan2 current speed %
        public const byte REG_CPU_TEMP   = 0x58; // CPU temperature °C
        public const byte REG_GPU_TEMP   = 0x59; // GPU temperature °C
        public const byte AUTO_VALUE     = 0xFF; // Auto mode

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
