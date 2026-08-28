# 本机适配说明（荣耀 MagicBook 16 Pro 2021 / R7-5800H）

> 基于 GitHub 仓库 [4evergr8/C-sharpEC](https://github.com/4evergr8/C-sharpEC) 的本地化改造。
> **目标已达成：电池充电阈值 80%（充到 80% 停止充电），2026-08-26 实测验证 ✅**

---

## 一、最终结论（已实测验证 ✅）

| 项目 | 值 |
|---|---|
| EC 访问协议 | **Mach 协议**（来源：荣耀电脑管家 `BIOS_EC_config.xml`） |
| 命令端口 | **0x25D** |
| 数据端口 | **0x25C** |
| 读命令 | 0x80（写 0x81），**8 位地址**（2 字节地址会写错数据！） |
| EC 寄存器基址 | 0x0200 |
| **充电阈值寄存器** | **EC[0xE5]**（基址 0x0200 + 0xE5 = 地址 0x02E5） |
| 阈值编码 | **直接百分比**：0x32=50%  0x50=80%  0x64=100% |
| 验证结果 | `mach setlimit 50` → EC[0xE5] = 0x50 ✓；阈值=2% 时插电停在 98% 不再充（实锤） |

## 二、踩过的坑（重要经验）

1. **0x62/0x66 端口协议在这台机器上是死的**（标准 ACPI EC 协议无效）——EC 数据在 MMIO（0xFE80D700）和 Mach 端口空间（0x0200+）。
2. **2 字节地址写入会写错数据**：EC 只认 8 位地址，多发的字节会被当成数据写进寄存器（曾把 0xE5 写成 0x02）。
3. **Burst 模式陷阱**：向命令口发 0x82 会使 EC 进入 Burst 模式，**此模式下写入被挂起**（表现为写入后回读不变）——必须发 **0x83** 退出 Burst。
4. **接口卡死恢复**：读命令后必须把 EC 输出的数据读走（排空 OBF），否则后续操作全部超时（`mach drain` 可恢复）。
5. **WinRing0 内存 IOCTL 不可用**：本仓库附带的驱动是精简版（函数号 0x833-0x838，无内存访问）；**盲扫未知 IOCTL 曾导致系统死机 3 次——切勿再对未知 IOCTL 做探测**。
6. **电脑管家 20.x 在这台机器上无法使用**（MBAMainService 崩溃循环，0xc000000d），且其后台服务会持续把阈值改回 100%——已卸载，本工具完全替代。

## 三、使用方法

### 日常使用（托盘常驻）
```
output\HonorPCManagerisJ8.exe
```
- 自动提权（本机 UAC 为静默模式，不弹窗）；
- 写入 config.yaml 的阈值（默认 80%），每小时重写一次防失效；
- 托盘图标右键退出 = 恢复 100%；
- `startup: true` 自动写开机自启注册表。

### 改阈值
编辑 `config.yaml` 的 `limit: 80`（0-100 十进制），重启程序即可（无需重编译）。

### 命令行工具（管理员运行）
```
HonorPCManagerisJ8.exe mach read E5          # 读当前阈值
HonorPCManagerisJ8.exe mach setlimit 50      # 一键设 50%（排空+Burst退出+写+验证）
HonorPCManagerisJ8.exe mach dump             # 全空间扫描（电池信息/温度等）
HonorPCManagerisJ8.exe mach drain            # 接口卡死时排空
HonorPCManagerisJ8.exe mach burstoff         # 退出 Burst 模式
```

### 一键脚本（双击即用，输出自动存文件）
- `run_set80_v3.bat`：设 80%（已验证成功）
- `run_mach_dump1/2.bat`：EC 全空间扫描

## 四、EC 空间已知布局（Mach 空间 0x0200+）

- 0x00-0x1F：状态/标志位
- 0x20-0x29：温度传感器阵列（10 个，单位 ℃）
- 0x5A-0x5D：电池化学类型（"LION"）
- 0x60-0x6B：电池制造商（"Sunwoda" 欣旺达）
- 0xB2-0xC4：电池型号（"HB6181V1ECW-41"）
- **0xE5：充电阈值（百分比，已验证可写）**
- 0xE0-0xE1：电池容量相关（16 位）

## 五、本机环境备忘

- Windows 10 21H2（19044.1288），.NET Framework 4.8（内置）；
- UAC：ConsentPromptBehaviorAdmin=0（静默提升，无弹窗）；
- Windows Defender 已被用户关闭；
- 荣耀电脑管家已卸载（其服务会覆盖阈值设置）。
