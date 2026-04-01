# Thunderobot ZERO Fan Control

Reverse-engineered EC (Embedded Controller) fan control for the **Thunderobot ZERO** (雷神 ZERO) gaming laptop.

This project provides a command-line fan control tool and an NBFC configuration, based on a complete reverse engineering of the laptop's EC registers.

## Hardware Info

| Item | Value |
|------|-------|
| Manufacturer | THUNDEROBOT (雷神) |
| Model | ZERO (911 series) |
| OEM/Barebone | Quanta (广达) |
| EC Access | Port 0x62/0x66 (standard ACPI EC) |

> **Note:** Other laptops sharing the same Quanta barebone — such as **Eluktronics Prometheus XVI**, **MaiBenBen x658** — may also work with this configuration. If you have one of these models and can confirm, please open an issue.

## EC Register Map

| Register | Decimal | Type | Function |
|----------|---------|------|----------|
| `0x2C` | 44 | **Read/Write** | Fan1 (CPU) duty cycle control |
| `0x2D` | 45 | **Read/Write** | Fan2 (GPU) duty cycle control |
| `0x2E` | 46 | Read-only | Fan1 current speed (%) |
| `0x2F` | 47 | Read-only | Fan2 current speed (%) |
| `0x58` | 88 | Read-only | CPU temperature (°C) |
| `0x59` | 89 | Read-only | GPU temperature (°C) |
| `0x49` | 73 | Read-only | Secondary temperature sensor |
| `0xB7` | 183 | Read-only | VRM/board temperature |

### Fan Control Values

| Value Written to 0x2C / 0x2D | Behavior |
|-------------------------------|----------|
| `0xFF` (255) | **Auto mode** — EC controls fan speed automatically |
| `0` - `100` | **Manual mode** — Sets fan duty cycle as a percentage |

The actual fan speed (read from `0x2E` / `0x2F`) will ramp toward the target value over a few seconds.

## Quick Start

### Prerequisites

- Windows 10/11
- [NoteBook FanControl](https://github.com/hirschmann/nbfc) installed (provides `ec-probe.exe`)
- **Run as Administrator** (EC access requires elevated privileges)

### Using the Python Tool

```bash
# Check current status
python thunderobot_fan.py status

# Set both fans to 60%
python thunderobot_fan.py set 60

# Set fan1=80%, fan2=50%
python thunderobot_fan.py set 80 50

# Full speed (emergency cooling)
python thunderobot_fan.py max

# Quiet mode (20%, watch your temps!)
python thunderobot_fan.py silent

# Return to auto mode
python thunderobot_fan.py auto

# Continuous monitoring
python thunderobot_fan.py monitor

# Run custom temperature-based fan curve
python thunderobot_fan.py curve
```

### Using NBFC

1. Copy `nbfc/Thunderobot ZERO.xml` to the NBFC Configs directory:
   ```
   C:\Program Files (x86)\NoteBook FanControl\Configs\
   ```
2. Open NoteBook FanControl
3. Select "THUNDEROBOT ZERO" from the model list
4. Apply and enjoy automatic fan curve control

## Reverse Engineering Process

A detailed write-up of the full reverse engineering process is available in [`docs/reverse-engineering.md`](docs/reverse-engineering.md).

The short version:

1. **Dump EC registers** at idle — got baseline values for all 256 registers
2. **Stress test + monitor** — ran CPU load while monitoring registers every 1.5s to find temperature/fan-correlated registers
3. **Write test** — systematically tested each register for writability (write, read-back, restore)
4. **Identify control registers** — Found `0x2C`/`0x2D` with anomalous write behavior (wrote 254, read back 100)
5. **Targeted fan test** — Wrote duty values 100→80→60→40→20 to `0x2C`/`0x2D` and confirmed fan speed tracked the target exactly

## Safety

- The tool always restores `0xFF` (auto mode) on Ctrl+C or unexpected exit
- Setting fans below 20% at high temperatures can cause thermal throttling or shutdown
- The built-in fan curve keeps a minimum of 20% duty
- **Never write arbitrary values to unknown EC registers** — this caused a forced reboot during our research

## File Structure

```
thunderobot-zero-fan-control/
├── README.md                       # This file
├── thunderobot_fan.py              # Fan control CLI tool
├── nbfc/
│   └── Thunderobot ZERO.xml        # NBFC configuration file
├── docs/
│   └── reverse-engineering.md      # Detailed RE process write-up
└── data/
    ├── ec_dump_idle.txt            # EC register dump at idle
    └── ec_register_map.csv         # Full register classification
```

## Contributing

If you have a Thunderobot ZERO variant (different year/GPU model) and can test:

1. Run `ec-probe dump` and compare with the idle dump in `data/`
2. Test if `0x2C`/`0x2D` control your fans the same way
3. Open an issue or PR with your findings

## License

MIT License — see [LICENSE](LICENSE).

## Acknowledgments

- [NBFC (NoteBook FanControl)](https://github.com/hirschmann/nbfc) — for `ec-probe.exe` and the configuration framework
- [NBFC Issue #1262](https://github.com/hirschmann/nbfc/issues/1262) — the original request that identified the Quanta OEM connection
