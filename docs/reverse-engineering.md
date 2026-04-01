# Reverse Engineering the Thunderobot ZERO EC Fan Control

# 逆向工程雷神 ZERO EC 风扇控制

[English](#english) | [中文](#中文)

---

## English

This document describes the complete process of reverse-engineering the Embedded Controller (EC) registers and WMI interface responsible for fan control on the Thunderobot ZERO gaming laptop.

### Background

The Thunderobot ZERO is a gaming laptop using a **Quanta (广达) barebone** design. Unlike Clevo-based laptops which have well-documented EC interfaces, the Quanta EC had no public documentation. An [NBFC issue (#1262)](https://github.com/hirschmann/nbfc/issues/1262) from 2022 attempted to find these registers but was unsuccessful.

#### Tools Used

- **ec-probe.exe** from [NoteBook FanControl](https://github.com/hirschmann/nbfc) — reads/writes EC registers
- **Python 3** — automation scripts
- **ILSpy** — .NET decompilation of ControlCenter.exe
- **PowerShell / multiprocessing** — CPU stress testing

### Phase 1: Baseline EC Dump

Dumped all 256 EC registers (0x00–0xFF) at idle:

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

### Phase 2: Stress Test Monitoring

Ran 90-second monitor while stressing all CPU cores:

| Register | Idle Avg | Stress Avg | Delta | Interpretation |
|----------|----------|------------|-------|----------------|
| **0x58** | 69°C | 85°C | **+16.4°C** | CPU temperature |
| **0x59** | 47°C | 50°C | +2.4°C | GPU temperature |
| 0x2E | 33 | 33 | **0** | Fan1 (unchanged!) |
| 0x2F | 31 | 31 | **0** | Fan2 (unchanged!) |

**Key observation:** CPU temp rose 16°C but fan speed registers didn't change — the EC was in a fixed-speed mode.

### Phase 3: Write Test

Systematically tested register writability (write, read-back, restore):

| Register | Original | Result | Notes |
|----------|----------|--------|-------|
| **0x2C** | 0xFF | **Wrote 254, read back 100** | Mode register! |
| **0x2D** | 0xFF | Same anomalous behavior | Mode register! |
| 0x2E, 0x2F | 33, 31 | **READ-ONLY** | Fan speed readout |
| 0x58, 0x59 | 87, 49 | **READ-ONLY** | Temperature |
| 0x96 | 100 | EC **overwrites within 2s** | EC-controlled |
| 0xD0 | 100 | **READ-ONLY** | EC's computed duty |

The **anomalous behavior of `0x2C`/`0x2D`** was the breakthrough — the EC interpreted writes as duty cycle commands.

### Phase 4: Targeted Fan Test

Wrote specific duty values to `0x2C`/`0x2D`:

```
0x2C=100 → Fan1[0x2E]: 33 → 45 → 59 → 73  (ramping to 100%)
0x2C=80  → Fan1[0x2E]: settled at 79-80
0x2C=60  → Fan1[0x2E]: settled at 59-60
0x2C=40  → Fan1[0x2E]: settled at 40
0x2C=20  → Fan1[0x2E]: settled at 19-20
0x2C=0xFF → Fan1[0x2E]: returned to 33 (auto idle)
```

Fan speed changes were **clearly audible**. The data is unambiguous.

### Phase 5: EC Control Loop Analysis

```
EC Control Loop (every ~1-2s):
  1. Read temps → store in 0x58, 0x59
  2. Calculate duty → write to 0x96, 0xD0, 0xD8
  3. IF 0x2C == 0xFF: apply calculated duty to Fan1
     ELSE: apply 0x2C value (manual override)
  4. Same for 0x2D / Fan2
  5. Update readout in 0x2E, 0x2F
```

### Phase 6: WMI Interface (ControlCenter.exe Decompilation)

Decompiled the Thunderobot Control Center (`ControlCenter.exe`, .NET assembly) using ILSpy. Found it uses WMI class `RW_GMWMI` in `root\wmi` namespace with a 32-byte `BufferBytes` SMI struct:

```csharp
struct SMI_STRUCT_S {
    ushort a0;  // 0xFA00=get, 0xFB00=set
    ushort a1;  // command code
    uint a2, a3, a4, a5, a6, rev0, rev1;
}
```

Key discovery in `MainWindow.cs`:
```csharp
// GetHardwareInformation: a0=64000(0xFA00), a1=512(0x0200)
CPUTemperature = (byte)_WMI.data.a2;
GPUTemperature = (byte)_WMI.data.a3;
CPUFanSpeed = (ushort)_WMI.data.a4;  // ← actual RPM!
GPUFanSpeed = (ushort)_WMI.data.a5;  // ← actual RPM!
```

Verified with a test script — WMI returns real RPM values (e.g. 2796, 2585).

### Lessons Learned

- **Never blindly write to all EC registers** — caused a forced reboot
- Start with read-only observation, then targeted writes
- Always restore original values on exit

### Final Register Map

```
0x2C: Fan1 duty write (0-100=manual%, 0xFF=auto) ← KEY
0x2D: Fan2 duty write (0-100=manual%, 0xFF=auto) ← KEY
0x2E: Fan1 actual speed % (read-only)
0x2F: Fan2 actual speed % (read-only)
0x58: CPU temperature °C (read-only)
0x59: GPU temperature °C (read-only)
0x96: EC-calculated fan duty (EC overwrites)
0xD0: Current fan duty readout (read-only)
0xB0-B3: Battery current/voltage
0xB7: VRM/board temperature
```

### WMI Command Map

```
a0=0xFA00, a1=0x0200: Get temps + fan RPMs
a0=0xFB00, a1=0x0205: Set fan duty (a2=CPU%, a3=GPU%)
a0=0xFB00, a1=0x0300: Set power mode (a2: 0=High, 1=Gaming, 2=Office)
```

---

## 中文

本文档完整记录了对雷神 ZERO 游戏本 EC（嵌入式控制器）寄存器和 WMI 接口进行逆向工程的全过程。

### 背景

雷神 ZERO 采用**广达（Quanta）模具**。与有完善 EC 文档的蓝天（Clevo）模具不同，广达 EC 没有公开文档。2022 年的 [NBFC issue #1262](https://github.com/hirschmann/nbfc/issues/1262) 曾尝试寻找这些寄存器但未成功。

#### 使用的工具

- **ec-probe.exe**（来自 [NBFC](https://github.com/hirschmann/nbfc)）— 读写 EC 寄存器
- **Python 3** — 自动化脚本
- **ILSpy** — 反编译 ControlCenter.exe（.NET 程序集）
- **PowerShell / multiprocessing** — CPU 压力测试

### 阶段一：基线 EC 寄存器转储

在空闲状态下转储所有 256 个 EC 寄存器（0x00–0xFF），获得基线数据。

关键值：
- `0x2C` = 0xFF, `0x2D` = 0xFF — 后被确认为自动模式标志
- `0x2E` = 33, `0x2F` = 31 — 风扇转速读数（百分比）
- `0x58` = 63°C, `0x59` = 47°C — CPU/GPU 温度
- `0x96` = 100, `0xD0` = 100, `0xD8` = 100 — 可疑的固定值

### 阶段二：压力测试监控

启动全部 CPU 核心压力测试，持续 90 秒监控 EC 寄存器变化：

| 寄存器 | 空闲均值 | 压力均值 | 变化量 | 解释 |
|--------|----------|----------|--------|------|
| **0x58** | 69°C | 85°C | **+16.4°C** | CPU 温度 |
| **0x59** | 47°C | 50°C | +2.4°C | GPU 温度 |
| 0x2E | 33 | 33 | **0** | Fan1（无变化！）|
| 0x2F | 31 | 31 | **0** | Fan2（无变化！）|

**关键发现：** CPU 温度上升了 16°C，但风扇转速寄存器没有变化——EC 处于固定转速模式。

### 阶段三：写入测试

系统性测试每个候选寄存器的可写性（写入→立即读回→恢复原值）：

| 寄存器 | 原始值 | 结果 | 说明 |
|--------|--------|------|------|
| **0x2C** | 0xFF | **写入 254，读回 100** | 模式寄存器！|
| **0x2D** | 0xFF | 相同异常行为 | 模式寄存器！|
| 0x2E, 0x2F | 33, 31 | **只读** | 风扇转速读数 |
| 0x58, 0x59 | 87, 49 | **只读** | 温度传感器 |
| 0x96 | 100 | EC **在 2 秒内覆盖** | EC 内部控制 |

**`0x2C`/`0x2D` 的异常行为是突破口** — EC 将写入解释为占空比命令。

### 阶段四：定向风扇测试

向 `0x2C`/`0x2D` 写入特定占空比值并监控风扇响应：

```
0x2C=100 → Fan1[0x2E]: 33 → 45 → 59 → 73  (向 100% 爬升)
0x2C=80  → Fan1[0x2E]: 稳定在 79-80
0x2C=60  → Fan1[0x2E]: 稳定在 59-60
0x2C=40  → Fan1[0x2E]: 稳定在 40
0x2C=20  → Fan1[0x2E]: 稳定在 19-20
0x2C=0xFF → Fan1[0x2E]: 回到 33（自动空闲）
```

测试过程中**风扇转速变化清晰可闻**。数据明确无疑。

### 阶段五：EC 控制循环分析

```
EC 控制循环（每 ~1-2 秒执行）：
  1. 读取温度 → 存入 0x58, 0x59
  2. 计算占空比 → 写入 0x96, 0xD0, 0xD8
  3. 若 0x2C == 0xFF：使用计算值控制 Fan1
     否则：使用 0x2C 的值（手动覆盖）
  4. 0x2D / Fan2 同理
  5. 更新实际转速到 0x2E, 0x2F
```

### 阶段六：WMI 接口逆向（反编译 ControlCenter.exe）

使用 ILSpy 反编译雷神 Control Center（`ControlCenter.exe`，.NET 程序集）。发现其使用 `root\wmi` 命名空间中的 WMI 类 `RW_GMWMI`，通过 32 字节 `BufferBytes` SMI 结构体通信：

```csharp
struct SMI_STRUCT_S {
    ushort a0;  // 0xFA00=读取, 0xFB00=设置
    ushort a1;  // 命令码
    uint a2, a3, a4, a5, a6, rev0, rev1;
}
```

在 `MainWindow.cs` 中找到关键代码：
```csharp
// GetHardwareInformation: a0=64000(0xFA00), a1=512(0x0200)
CPUTemperature = (byte)_WMI.data.a2;
GPUTemperature = (byte)_WMI.data.a3;
CPUFanSpeed = (ushort)_WMI.data.a4;  // ← 真实 RPM！
GPUFanSpeed = (ushort)_WMI.data.a5;  // ← 真实 RPM！
```

通过测试脚本验证——WMI 返回真实 RPM 值（例如 2796、2585）。

### 经验教训

- **切勿盲目写入所有 EC 寄存器** — 导致了一次强制重启
- 先只读观察，再定向写入
- 退出时务必恢复原始值

### 最终寄存器映射

```
0x2C: Fan1 占空比写入 (0-100=手动%, 0xFF=自动) ← 关键寄存器
0x2D: Fan2 占空比写入 (0-100=手动%, 0xFF=自动) ← 关键寄存器
0x2E: Fan1 实际转速 %（只读）
0x2F: Fan2 实际转速 %（只读）
0x58: CPU 温度 °C（只读）
0x59: GPU 温度 °C（只读）
0x96: EC 计算的风扇占空比（EC 会覆盖）
0xD0: 当前风扇占空比读数（只读）
0xB0-B3: 电池电流/电压
0xB7: VRM/主板温度
```

### WMI 命令映射

```
a0=0xFA00, a1=0x0200: 获取温度 + 风扇 RPM
a0=0xFB00, a1=0x0205: 设置风扇占空比 (a2=CPU%, a3=GPU%)
a0=0xFB00, a1=0x0300: 设置性能模式 (a2: 0=狂暴, 1=游戏, 2=办公)
```
