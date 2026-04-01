# Reverse Engineering the Thunderobot ZERO EC Fan Control

This document describes the complete process of reverse-engineering the Embedded Controller (EC) registers responsible for fan control on the Thunderobot ZERO gaming laptop.

## Background

The Thunderobot ZERO (雷神 ZERO) is a gaming laptop using a **Quanta (广达) barebone** design. Unlike Clevo-based laptops which have well-documented EC interfaces, the Quanta EC had no public documentation. An [NBFC issue (#1262)](https://github.com/hirschmann/nbfc/issues/1262) from 2022 attempted to find these registers but was unsuccessful.

### Tools Used

- **ec-probe.exe** from [NoteBook FanControl](https://github.com/hirschmann/nbfc) — reads/writes individual EC registers and dumps all 256
- **Python 3** — for automation scripts
- **PowerShell / multiprocessing** — for CPU stress testing

## Phase 1: Baseline EC Dump

First, we dumped all 256 EC registers (0x00–0xFF) with the system idle:

```
   | 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F
---|------------------------------------------------
00 | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
10 | 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
20 | 00 00 00 00 00 00 00 00 00 00 00 00 FF FF 21 1F
30 | 00 00 33 00 41 82 00 00 01 00 00 00 00 00 00 00
40 | 0B 00 00 14 00 00 00 00 00 2C 2E 00 00 00 00 00
50 | 90 00 04 00 02 00 00 00 3F 2F 00 00 00 00 00 00
60 | 00 00 00 00 7F 03 00 00 08 40 00 09 00 0E 0E 00
70 | 87 10 C3 10 60 3B 80 53 4D 50 5F 43 41 36 30 36
80 | 30 37 30 00 00 AD 10 4F 43 00 00 00 01 E0 00 00
90 | 00 00 C3 10 05 01 64 00 00 00 00 00 00 17 0C 00
A0 | 00 80 11 AA 01 00 00 00 00 15 00 00 00 01 00 02
B0 | 23 08 CC 08 00 01 F7 2A C8 00 00 08 00 08 00 00
C0 | 00 00 00 00 00 00 00 00 00 00 00 00 40 00 00 05
D0 | 64 00 00 00 00 00 00 01 64 00 00 11 00 00 D1 D1
E0 | 00 00 01 00 00 00 00 0A 00 00 00 01 0A 66 00 00
F0 | 29 00 20 00 00 00 00 00 00 00 50 30 30 37 44 30
```

Notable values at idle:
- `0x2C` = 0xFF, `0x2D` = 0xFF — later confirmed as auto mode flag
- `0x2E` = 0x21 (33), `0x2F` = 0x1F (31) — fan speed readouts
- `0x58` = 0x3F (63°C), `0x59` = 0x2F (47°C) — temperatures
- `0x96` = 0x64 (100), `0xD0` = 0x64 (100), `0xD8` = 0x64 (100) — suspicious constant values
- `0x77-0x82`: ASCII string "SMP_CA606070" — battery manufacturer info (SMP = Simplo)

## Phase 2: Stress Test Monitoring

We ran a 90-second monitor while stressing all CPU cores with Python `multiprocessing`:

### Results

| Register | Idle Avg | Stress Avg | Delta | Interpretation |
|----------|----------|------------|-------|----------------|
| **0x58** | 69°C | 85°C | **+16.4°C** | CPU temperature |
| **0x59** | 47°C | 50°C | +2.4°C | GPU temperature |
| **0xB7** | 42°C | 47°C | +4.0°C | VRM/board temp |
| **0x49** | 44°C | 45°C | +1.2°C | Secondary temp |
| 0x2E | 33 | 33 | **0** | Fan1 speed (unchanged!) |
| 0x2F | 31 | 31 | **0** | Fan2 speed (unchanged!) |
| 0xB0 | 55 | 59 | +3.7 | Battery current (oscillating) |
| 0xB2 | 209 | 213 | +4.0 | Battery voltage (oscillating) |

**Key observation:** CPU temperature rose 16°C but fan speed registers `0x2E`/`0x2F` didn't change. This meant either the fan wasn't ramping up (unlikely at 85°C), or the EC was in a fixed-speed mode. Static registers `0x96`, `0xD0`, `0xD8` (all = 100) were immediately suspicious as potential duty controls.

## Phase 3: Write Test

We systematically tested each candidate register for writability by:
1. Reading the original value
2. Writing a test value (XOR with 0x01)
3. Reading back immediately
4. Restoring the original value

### Results

| Register | Original | Write Accepted? | Notes |
|----------|----------|-----------------|-------|
| **0x2C** | 0xFF | **ANOMALOUS** — wrote 254, read back **100** | Mode register! |
| **0x2D** | 0xFF | **ANOMALOUS** — same behavior as 0x2C | Mode register! |
| 0x2E | 33 | **READ-ONLY** | Fan speed readout |
| 0x2F | 31 | **READ-ONLY** | Fan speed readout |
| 0x58 | 87 | **READ-ONLY** | CPU temperature |
| 0x59 | 49 | **READ-ONLY** | GPU temperature |
| 0x96 | 100 | Writable but EC **overwrites within 2s** | EC-controlled |
| 0xD0 | 100 | **READ-ONLY** | EC's computed duty |
| 0xD8 | 100 | Writable but EC **overwrites within 2s** | EC-controlled |
| 0xD7 | 1 | Writable, value **persists** | Config register |
| 0x32 | 51 | Writable | Temp threshold? |
| 0x34 | 65 | Writable | Temp threshold? |
| 0xF0 | 41 | **READ-ONLY** | Hardware readout |

The **anomalous behavior of `0x2C`/`0x2D`** was the breakthrough. Writing 254 (0xFE) to `0x2C` (which held 0xFF) resulted in a readback of 100 — not 254, not 0xFF. This suggested the EC was **interpreting writes as duty cycle commands**: the register transitioned from auto mode (0xFF) to manual mode, and the readback of 100 represented the current duty the EC was running at.

## Phase 4: Targeted Fan Test

With `0x2C`/`0x2D` identified as the primary candidates, we wrote specific duty values and monitored the fan speed response:

### Test: Writing to 0x2C (Fan1)

```
Wrote 0x2C=100 → Fan1[0x2E] ramped: 33 → 45 → 59 → 73  (approaching 100%)
Wrote 0x2C=80  → Fan1[0x2E] settled at: 79-80
Wrote 0x2C=60  → Fan1[0x2E] settled at: 59-60
Wrote 0x2C=40  → Fan1[0x2E] settled at: 40
Wrote 0x2C=20  → Fan1[0x2E] settled at: 19-20
Wrote 0x2C=0xFF → Fan1[0x2E] returned to: 33 (auto idle)
```

### Test: Writing to 0x2D (Fan2)

```
Wrote 0x2D=100 → Fan2[0x2F] ramped: 31 → 44 → 60 → 73  (approaching 100%)
Wrote 0x2D=80  → Fan2[0x2F] settled at: 79-80
Wrote 0x2D=60  → Fan2[0x2F] settled at: 59
Wrote 0x2D=40  → Fan2[0x2F] settled at: 39-40
Wrote 0x2D=20  → Fan2[0x2F] settled at: 20
Wrote 0x2D=0xFF → Fan2[0x2F] returned to: 31 (auto idle)
```

### Test: Both Fans Together

```
0x2C=100, 0x2D=100 → Fan1: 88%, Fan2: 86% (ramping up)
0x2C=60,  0x2D=60  → Fan1: 59%, Fan2: 60% (stable)
0x2C=30,  0x2D=30  → Fan1: 30%, Fan2: 30% (stable, CPU at 79°C)
```

**Fan speed changes were clearly audible during testing.** The data is unambiguous:
- `0x2E`/`0x2F` (read registers) track the target written to `0x2C`/`0x2D` precisely
- The fans ramp toward the target over 3-5 seconds
- Writing `0xFF` immediately returns to EC auto control

## Phase 5: EC Control Loop Analysis

During earlier testing, we discovered that registers `0x96` and `0xD8` (both containing 100) were also writable but the EC would **overwrite our values within 1-2 seconds**. This reveals the EC's internal architecture:

```
EC Control Loop (runs every ~1-2s):
  1. Read CPU temp from hardware → store in 0x58
  2. Read GPU temp from hardware → store in 0x59
  3. Calculate desired fan duty based on temp curve
  4. Write calculated duty to 0x96, 0xD0, 0xD8
  5. IF 0x2C == 0xFF: apply calculated duty to Fan1
     ELSE: apply 0x2C value to Fan1 (manual override)
  6. IF 0x2D == 0xFF: apply calculated duty to Fan2
     ELSE: apply 0x2D value to Fan2 (manual override)
  7. Update actual fan speed readout in 0x2E, 0x2F
```

This explains why writing to `0x96`/`0xD8` had no lasting effect (the EC loop overwrites them), while `0x2C`/`0x2D` act as a **manual override gate** that bypasses the EC's auto calculation.

## Lessons Learned & Safety

### What Went Wrong
During an early attempt to scan **all 256 registers** for writability (writing and restoring each one), the laptop **force-rebooted**. This was caused by writing to an unknown register in the power management or watchdog area. **Never blindly write to all EC registers.**

### Safe Approach
1. Start with **read-only observation** (dump + monitor under load)
2. Identify candidates by **correlation** (temp rises → which registers change?)
3. Test writability with **minimal changes** (XOR lowest bit, restore immediately)
4. Test control effect **one register at a time**, with known-safe values
5. **Always restore original values** on exit

## Final Register Map

```
0x00-0x1F: Unused (all zeros)
0x20-0x2B: Unused
0x2C:      Fan1 duty write (0-100=manual%, 0xFF=auto) ← KEY REGISTER
0x2D:      Fan2 duty write (0-100=manual%, 0xFF=auto) ← KEY REGISTER
0x2E:      Fan1 actual speed % (read-only)
0x2F:      Fan2 actual speed % (read-only)
0x30-0x3F: Configuration/thresholds (writable)
0x40-0x4A: Additional temp sensors
0x50:      System config
0x58:      CPU temperature °C (read-only)
0x59:      GPU temperature °C (read-only)
0x60-0x6F: System config
0x70-0x82: Battery info string "SMP_CA606070"
0x83-0x9F: Battery/power config
0x96:      EC-calculated fan duty (writable but EC overwrites)
0xB0-0xB3: Battery current/voltage (oscillating, real-time)
0xB7:      VRM/board temperature
0xC0-0xCF: System config
0xD0:      Current fan duty readout (read-only, always 100)
0xD7:      Fan config flag
0xD8:      EC-calculated fan duty (writable but EC overwrites)
0xDE-0xDF: System config (0xD1, 0xD1)
0xE0-0xEF: System config
0xF0-0xFF: System identifiers, "P007D0"
```
