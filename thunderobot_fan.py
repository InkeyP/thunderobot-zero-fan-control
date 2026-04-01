#!/usr/bin/env python3
"""
Thunderobot ZERO Fan Control Tool
==================================
Controls fan speed via EC registers discovered through reverse engineering.

EC Register Map:
  0x2C (write) = Fan1 (CPU) duty cycle: 0-100 manual %, 0xFF = auto
  0x2D (write) = Fan2 (GPU) duty cycle: 0-100 manual %, 0xFF = auto
  0x2E (read)  = Fan1 current speed %
  0x2F (read)  = Fan2 current speed %
  0x58 (read)  = CPU temperature (C)
  0x59 (read)  = GPU temperature (C)

Requires: ec-probe.exe from NoteBook FanControl (run as Administrator)

Usage:
  python thunderobot_fan.py status          Show current fan/temp status
  python thunderobot_fan.py auto            Set both fans to auto mode
  python thunderobot_fan.py set 60          Set both fans to 60%
  python thunderobot_fan.py set 80 50       Set fan1=80%, fan2=50%
  python thunderobot_fan.py max             Set both fans to 100% (full blast)
  python thunderobot_fan.py silent          Set both fans to minimum (~20%)
  python thunderobot_fan.py curve           Run auto curve (custom temp->fan mapping)
  python thunderobot_fan.py monitor         Continuous monitoring display
"""
import subprocess
import time
import re
import sys
import signal
import os

EC_PROBE = r"C:\Program Files (x86)\NoteBook FanControl\ec-probe.exe"

# EC Register addresses
REG_FAN1_WRITE = 0x2C  # Fan1 duty write (0-100, 0xFF=auto)
REG_FAN2_WRITE = 0x2D  # Fan2 duty write (0-100, 0xFF=auto)
REG_FAN1_READ  = 0x2E  # Fan1 current speed %
REG_FAN2_READ  = 0x2F  # Fan2 current speed %
REG_CPU_TEMP   = 0x58  # CPU temperature
REG_GPU_TEMP   = 0x59  # GPU temperature
AUTO_VALUE     = 0xFF  # Auto mode magic value

# Custom fan curve: (temp_threshold, fan_duty)
# Fan ramps up as temperature increases
DEFAULT_CURVE = [
    (45, 0),     # Below 45C: fans off (risky, use 20 for safe minimum)
    (50, 25),    # 50C: 25%
    (60, 35),    # 60C: 35%
    (70, 50),    # 70C: 50%
    (80, 70),    # 80C: 70%
    (85, 85),    # 85C: 85%
    (90, 100),   # 90C+: full speed
]

SAFE_CURVE = [
    (40, 20),    # Below 40C: minimum 20%
    (50, 30),    # 50C: 30%
    (60, 40),    # 60C: 40%
    (70, 55),    # 70C: 55%
    (80, 75),    # 80C: 75%
    (85, 90),    # 85C: 90%
    (90, 100),   # 90C+: full speed
]


def read_reg(reg):
    r = subprocess.run([EC_PROBE, "read", f"0x{reg:02X}"],
                       capture_output=True, text=True)
    m = re.match(r'(\d+)', r.stdout.strip())
    return int(m.group(1)) if m else None


def write_reg(reg, val):
    subprocess.run([EC_PROBE, "write", f"0x{reg:02X}", str(val)],
                   capture_output=True, text=True)


def get_status():
    return {
        'fan1_target': read_reg(REG_FAN1_WRITE),
        'fan2_target': read_reg(REG_FAN2_WRITE),
        'fan1_speed': read_reg(REG_FAN1_READ),
        'fan2_speed': read_reg(REG_FAN2_READ),
        'cpu_temp': read_reg(REG_CPU_TEMP),
        'gpu_temp': read_reg(REG_GPU_TEMP),
    }


def set_auto():
    write_reg(REG_FAN1_WRITE, AUTO_VALUE)
    write_reg(REG_FAN2_WRITE, AUTO_VALUE)


def set_fans(fan1_duty, fan2_duty=None):
    if fan2_duty is None:
        fan2_duty = fan1_duty
    fan1_duty = max(0, min(100, fan1_duty))
    fan2_duty = max(0, min(100, fan2_duty))
    write_reg(REG_FAN1_WRITE, fan1_duty)
    write_reg(REG_FAN2_WRITE, fan2_duty)


def print_status(s=None):
    if s is None:
        s = get_status()
    f1t = s['fan1_target']
    f2t = s['fan2_target']
    f1_mode = "AUTO" if f1t == 255 else f"{f1t}%"
    f2_mode = "AUTO" if f2t == 255 else f"{f2t}%"
    print(f"  CPU: {s['cpu_temp']}C  GPU: {s['gpu_temp']}C  |  "
          f"Fan1: {s['fan1_speed']}% (target: {f1_mode})  "
          f"Fan2: {s['fan2_speed']}% (target: {f2_mode})")


def interpolate_curve(temp, curve):
    """Get fan duty for a given temperature using linear interpolation."""
    if temp <= curve[0][0]:
        return curve[0][1]
    if temp >= curve[-1][0]:
        return curve[-1][1]
    for i in range(len(curve) - 1):
        t1, d1 = curve[i]
        t2, d2 = curve[i + 1]
        if t1 <= temp <= t2:
            ratio = (temp - t1) / (t2 - t1)
            return int(d1 + ratio * (d2 - d1))
    return curve[-1][1]


def cmd_status():
    print("Thunderobot ZERO Fan Status:")
    print_status()


def cmd_auto():
    set_auto()
    print("Fans set to AUTO mode.")
    time.sleep(1)
    print_status()


def cmd_set(args):
    if len(args) == 1:
        duty = int(args[0])
        set_fans(duty)
        print(f"Both fans set to {duty}%.")
    elif len(args) == 2:
        fan1 = int(args[0])
        fan2 = int(args[1])
        set_fans(fan1, fan2)
        print(f"Fan1 set to {fan1}%, Fan2 set to {fan2}%.")
    else:
        print("Usage: set <duty> or set <fan1_duty> <fan2_duty>")
        return
    time.sleep(1)
    print_status()


def cmd_max():
    set_fans(100, 100)
    print("Both fans set to 100% (FULL BLAST).")
    time.sleep(1)
    print_status()


def cmd_silent():
    set_fans(20, 20)
    print("Both fans set to 20% (SILENT mode). Watch temperatures!")
    time.sleep(1)
    print_status()


def cmd_monitor():
    print("Thunderobot ZERO Fan Monitor (Ctrl+C to stop, restores auto)")
    print("-" * 65)
    try:
        while True:
            s = get_status()
            ts = time.strftime("%H:%M:%S")
            f1t = s['fan1_target']
            f2t = s['fan2_target']
            f1m = "AUTO" if f1t == 255 else f"{f1t:3d}%"
            f2m = "AUTO" if f2t == 255 else f"{f2t:3d}%"
            print(f"[{ts}] CPU:{s['cpu_temp']:3d}C  GPU:{s['gpu_temp']:3d}C  |  "
                  f"Fan1:{s['fan1_speed']:3d}%({f1m})  Fan2:{s['fan2_speed']:3d}%({f2m})")
            time.sleep(2)
    except KeyboardInterrupt:
        print("\nStopped.")


def cmd_curve():
    curve = SAFE_CURVE
    print("Thunderobot ZERO Custom Fan Curve (Ctrl+C to stop, restores auto)")
    print("Curve:")
    for temp, duty in curve:
        print(f"  {temp}C -> {duty}%")
    print("-" * 65)

    try:
        while True:
            s = get_status()
            temp = max(s['cpu_temp'] or 0, s['gpu_temp'] or 0)
            duty = interpolate_curve(temp, curve)
            set_fans(duty)
            ts = time.strftime("%H:%M:%S")
            print(f"[{ts}] Max temp: {temp}C -> Duty: {duty}%  |  "
                  f"CPU:{s['cpu_temp']}C GPU:{s['gpu_temp']}C  "
                  f"Fan1:{s['fan1_speed']}% Fan2:{s['fan2_speed']}%")
            time.sleep(3)
    except KeyboardInterrupt:
        print("\nRestoring auto mode...")
        set_auto()
        print("Done.")


def main():
    # Safety: restore auto on unexpected exit
    def cleanup(sig, frame):
        set_auto()
        sys.exit(0)
    signal.signal(signal.SIGINT, cleanup)
    signal.signal(signal.SIGTERM, cleanup)

    if len(sys.argv) < 2:
        print(__doc__)
        return

    cmd = sys.argv[1].lower()

    if cmd == "status":
        cmd_status()
    elif cmd == "auto":
        cmd_auto()
    elif cmd == "set":
        cmd_set(sys.argv[2:])
    elif cmd == "max":
        cmd_max()
    elif cmd == "silent":
        cmd_silent()
    elif cmd == "monitor":
        cmd_monitor()
    elif cmd == "curve":
        cmd_curve()
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)


if __name__ == "__main__":
    main()
