# Thunderobot ZERO Fan Control / 雷神 ZERO 风扇控制

[English](#english) | [中文](#中文)

---

## English

Reverse-engineered EC (Embedded Controller) fan control for the **Thunderobot ZERO** gaming laptop.

Provides a Python CLI tool, a [FanControl](https://github.com/Rem0o/FanControl.Releases) plugin with real RPM readout, and an NBFC configuration — all based on reverse engineering of the EC registers and WMI interface.

### Hardware Info

| Item | Value |
|------|-------|
| Manufacturer | THUNDEROBOT (雷神) |
| Model | ZERO (911 series) |
| OEM/Barebone | Quanta (广达) |
| EC Access | Port 0x62/0x66 (standard ACPI EC) |
| WMI Class | `RW_GMWMI` in `root\wmi` namespace |

> **Note:** Other laptops sharing the same Quanta barebone — such as **Eluktronics Prometheus XVI**, **MaiBenBen x658** — may also work. Please open an issue to confirm.

### EC Register Map

| Register | Type | Function |
|----------|------|----------|
| `0x2C` | Read/Write | Fan1 (CPU) duty cycle control |
| `0x2D` | Read/Write | Fan2 (GPU) duty cycle control |
| `0x2E` | Read-only | Fan1 current speed (%) |
| `0x2F` | Read-only | Fan2 current speed (%) |
| `0x58` | Read-only | CPU temperature (°C) |
| `0x59` | Read-only | GPU temperature (°C) |

**Fan control values:** `0xFF` = auto mode, `0`–`100` = manual duty cycle %.

### WMI Interface (reverse-engineered from ControlCenter.exe)

| Command | a0 | a1 | Returns |
|---------|----|----|---------|
| GetHardwareInfo | 0xFA00 | 0x0200 | a2=CPU Temp, a3=GPU Temp, **a4=CPU Fan RPM**, **a5=GPU Fan RPM** |
| SetFanSpeed | 0xFB00 | 0x0205 | a2=CPU duty%, a3=GPU duty% |
| SetPowerMode | 0xFB00 | 0x0300 | a2=mode (0=High, 1=Gaming, 2=Office) |

### Quick Start

#### Prerequisites

- Windows 10/11
- [NoteBook FanControl](https://github.com/hirschmann/nbfc) installed (provides `ec-probe.exe`)
- **Run as Administrator** (EC access requires elevated privileges)

#### Option 1: FanControl Plugin (Recommended)

Integrates with [FanControl (Rem0o)](https://github.com/Rem0o/FanControl.Releases), providing real RPM readout via WMI and fan duty control via EC registers.

1. Build:
   ```bash
   cd FanControl.ThunderbotZero && dotnet build -c Release
   cd ../WmiHelper && dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/single
   ```
2. Copy to FanControl's Plugins directory:
   ```
   copy FanControl.ThunderbotZero\bin\Release\netstandard2.0\FanControl.ThunderbotZero.dll <FanControl>\Plugins\
   copy WmiHelper\bin\single\WmiHelper.exe <FanControl>\Plugins\
   ```
3. Launch FanControl (as Administrator). You will see:
   - **Temperature sensors:** `TR ZERO CPU Temp`, `TR ZERO GPU Temp`
   - **Fan sensors (RPM):** `TR ZERO Fan1 (CPU)`, `TR ZERO Fan2 (GPU)`
   - **Fan controls:** `TR ZERO Fan1 Control`, `TR ZERO Fan2 Control`

> FanControl automatically restores EC auto mode (0xFF) when closed.

#### Option 2: Python CLI Tool

```bash
python thunderobot_fan.py status       # Show current status
python thunderobot_fan.py set 60       # Set both fans to 60%
python thunderobot_fan.py set 80 50    # Fan1=80%, Fan2=50%
python thunderobot_fan.py max          # Full speed
python thunderobot_fan.py silent       # 20% (quiet, watch temps!)
python thunderobot_fan.py auto         # Restore auto mode
python thunderobot_fan.py monitor      # Continuous monitoring
python thunderobot_fan.py curve        # Custom temp-based fan curve
```

#### Option 3: NBFC

1. Copy `nbfc/Thunderobot ZERO.xml` to `C:\Program Files (x86)\NoteBook FanControl\Configs\`
2. Select "THUNDEROBOT ZERO" in NBFC and apply

### File Structure

```
thunderobot-zero-fan-control/
├── README.md                           # This file (EN + CN)
├── thunderobot_fan.py                  # Python CLI fan control tool
├── FanControl.ThunderbotZero/          # FanControl plugin (C#)
│   ├── ThunderbotZeroPlugin.cs         # Plugin entry point
│   ├── Sensors.cs                      # Temp/RPM/control sensors
│   ├── EcAccess.cs                     # EC register + WMI helper
│   └── FanControl.ThunderbotZero.csproj
├── WmiHelper/                          # Standalone WMI RPM query tool
│   ├── WmiHelper.cs                    # Queries RW_GMWMI for RPM
│   └── WmiHelper.csproj
├── nbfc/
│   └── Thunderobot ZERO.xml            # NBFC configuration
├── docs/
│   └── reverse-engineering.md          # Full RE process (EN + CN)
└── data/
    ├── ec_dump_idle.txt                # EC register dump at idle
    └── ec_register_map.csv             # Register classification
```

### Safety

- Always restores `0xFF` (auto mode) on exit
- Do not set fans below 20% at high temperatures
- **Never write to unknown EC registers** — this caused a forced reboot during research

### Reverse Engineering Process

See [`docs/reverse-engineering.md`](docs/reverse-engineering.md).

### License

MIT — see [LICENSE](LICENSE).

### Acknowledgments

- [FanControl (Rem0o)](https://github.com/Rem0o/FanControl.Releases)
- [NBFC](https://github.com/hirschmann/nbfc) — for `ec-probe.exe`
- [NBFC Issue #1262](https://github.com/hirschmann/nbfc/issues/1262)

---

## 中文

逆向工程 **雷神 ZERO**（Thunderobot ZERO）游戏本的 EC（嵌入式控制器）风扇控制。

提供 Python 命令行工具、[FanControl](https://github.com/Rem0o/FanControl.Releases) 插件（支持真实 RPM 读数）以及 NBFC 配置文件——全部基于对笔记本 EC 寄存器和 WMI 接口的完整逆向工程。

### 硬件信息

| 项目 | 值 |
|------|-----|
| 制造商 | 雷神 (THUNDEROBOT) |
| 型号 | ZERO (911 系列) |
| 代工/模具 | 广达 (Quanta) |
| EC 访问 | 端口 0x62/0x66（标准 ACPI EC）|
| WMI 类 | `root\wmi` 命名空间下的 `RW_GMWMI` |

> **提示：** 使用相同广达模具的笔记本（如 **Eluktronics Prometheus XVI**、**麦本本 x658**）也可能适用。欢迎开 issue 反馈。

### EC 寄存器映射

| 寄存器 | 类型 | 功能 |
|--------|------|------|
| `0x2C` | 读写 | Fan1（CPU 风扇）占空比控制 |
| `0x2D` | 读写 | Fan2（GPU 风扇）占空比控制 |
| `0x2E` | 只读 | Fan1 当前转速百分比 |
| `0x2F` | 只读 | Fan2 当前转速百分比 |
| `0x58` | 只读 | CPU 温度（°C）|
| `0x59` | 只读 | GPU 温度（°C）|

**风扇控制值：** `0xFF` = 自动模式（EC 自动控制），`0`–`100` = 手动占空比百分比。

### WMI 接口（逆向自 ControlCenter.exe）

| 命令 | a0 | a1 | 返回值 |
|------|----|----|--------|
| 获取硬件信息 | 0xFA00 | 0x0200 | a2=CPU温度, a3=GPU温度, **a4=CPU风扇RPM**, **a5=GPU风扇RPM** |
| 设置风扇转速 | 0xFB00 | 0x0205 | a2=CPU占空比%, a3=GPU占空比% |
| 设置性能模式 | 0xFB00 | 0x0300 | a2=模式（0=狂暴, 1=游戏, 2=办公）|

### 快速开始

#### 前置条件

- Windows 10/11
- 安装 [NoteBook FanControl](https://github.com/hirschmann/nbfc)（提供 `ec-probe.exe`）
- **以管理员身份运行**（EC 访问需要提权）

#### 方案一：FanControl 插件（推荐）

插件集成到 [FanControl (Rem0o)](https://github.com/Rem0o/FanControl.Releases)，通过 WMI 提供真实 RPM 读数，通过 EC 寄存器控制风扇占空比。

1. 编译：
   ```bash
   cd FanControl.ThunderbotZero && dotnet build -c Release
   cd ../WmiHelper && dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/single
   ```
2. 复制到 FanControl 的 Plugins 目录：
   ```
   copy FanControl.ThunderbotZero\bin\Release\netstandard2.0\FanControl.ThunderbotZero.dll <FanControl>\Plugins\
   copy WmiHelper\bin\single\WmiHelper.exe <FanControl>\Plugins\
   ```
3. 以管理员身份启动 FanControl，你将看到：
   - **温度传感器：** `TR ZERO CPU Temp`、`TR ZERO GPU Temp`
   - **风扇传感器（RPM）：** `TR ZERO Fan1 (CPU)`、`TR ZERO Fan2 (GPU)`
   - **风扇控制：** `TR ZERO Fan1 Control`、`TR ZERO Fan2 Control`

> 关闭 FanControl 时会自动恢复 EC 自动模式 (0xFF)。

#### 方案二：Python 命令行工具

```bash
python thunderobot_fan.py status       # 查看当前状态
python thunderobot_fan.py set 60       # 双风扇设为 60%
python thunderobot_fan.py set 80 50    # Fan1=80%, Fan2=50%
python thunderobot_fan.py max          # 全速（应急散热）
python thunderobot_fan.py silent       # 20%（静音，注意温度！）
python thunderobot_fan.py auto         # 恢复自动模式
python thunderobot_fan.py monitor      # 持续监控
python thunderobot_fan.py curve        # 自定义温度-风扇曲线
```

#### 方案三：NBFC

1. 将 `nbfc/Thunderobot ZERO.xml` 复制到 `C:\Program Files (x86)\NoteBook FanControl\Configs\`
2. 在 NBFC 中选择 "THUNDEROBOT ZERO" 并应用

### 安全须知

- 退出时始终恢复 `0xFF`（自动模式）
- 高温时勿将风扇设置低于 20%
- **切勿向未知 EC 寄存器写入数据** — 研究过程中曾导致强制重启

### 逆向工程过程

详见 [`docs/reverse-engineering.md`](docs/reverse-engineering.md)。

### 许可证

MIT — 见 [LICENSE](LICENSE)。

### 致谢

- [FanControl (Rem0o)](https://github.com/Rem0o/FanControl.Releases)
- [NBFC](https://github.com/hirschmann/nbfc) — 提供 `ec-probe.exe`
- [NBFC Issue #1262](https://github.com/hirschmann/nbfc/issues/1262)
