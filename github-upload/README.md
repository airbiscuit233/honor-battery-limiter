# Honor Battery Limiter（荣耀 MagicBook 充电阈值工具）

基于 [4evergr8/ECsharp](https://github.com/4evergr8/ECsharp) 本地化改造的轻量工具，
通过 WinRing0 内核驱动直写笔记本 EC 的 **Mach 协议**寄存器，实现**充电阈值控制**。

- 专机适配：**HONOR MagicBook 16 Pro 2021 (R7-5800H)**（EC Mach 协议,命令口 `0x25D` / 数据口 `0x25C`,基址 `0x0200`）
- 托盘常驻：开机自动设置充电阈值（默认 80%），每小时重写一次防被覆盖，退出时恢复
- 配置路径：程序目录 `config.yaml`（不支持写入时自动改用 `%LOCALAPPDATA%\BatteryLimiter\config.yaml`）

## 编译

```
dotnet build -c Release
```

> 需要 .NET SDK；工程目标 `.NET Framework 4.0`，编译时通过
> `Microsoft.NETFramework.ReferenceAssemblies` 包获取引用程序集。

## 使用

### 托盘模式（日常）
- 双击 `HonorPCManagerisJ8.exe`（自动提权，需管理员）
- 托盘图标右键：
  - **设置**：修改充电阈值 (limit) / 恢复阈值 (resume)
  - **恢复 100%**：临时取消限制
  - **退出**：恢复默认值并退出
- 配置项：`startup`(开机自启) / `debug`(显示控制台) / `timeout`(重写间隔 ms) / `wait` / `limit` / `resume`

### CLI 模式（调试）
```
HonorPCManagerisJ8.exe mach read E5        # 读停止充电阈值(EC[0xE5])
HonorPCManagerisJ8.exe mach read E4        # 读恢复充电阈值(EC[0xE4])
HonorPCManagerisJ8.exe mach setlimit 80    # 设置充电 80% (十进制!)
HonorPCManagerisJ8.exe mach setlimit 0x50  # 同上(十六进制前缀)
HonorPCManagerisJ8.exe mach dump           # EC 全空间扫描(0x00-0xFF)
HonorPCManagerisJ8.exe mach drain          # 接口卡死时排空 OBF
HonorPCManagerisJ8.exe mach burstoff       # 退出 Burst 模式
```

> ⚠️ `setlimit`/`setburst` 参数为**十进制百分比**(0-100,自动钳制)：
> `setlimit 80` = 80%（EC 写入 0x50）。旧版本曾按十六进制解析(`setlimit 80` 会写成 0x80=128%),
> 本版本已修复。
> 其余命令(`read`/`write`/`dump` 等)保留十六进制参数习惯。

## 修复记录

- **2026-08-28**：修复 `mach setlimit`/`mach setburst` 参数十六进制解析 bug
  （`ParseHexByte` 优先按十六进制 → `setlimit 80` 写成 `0x80`=128%，现改为十进制 `ParsePercentByte`）

## 重要说明(风险自负)

- 直写 EC 寄存器属于**裸硬件操作**，不保证在其它机型/固件上可用；
  Mach 协议细节见 `EC技术说明.md`（含踩坑记录：2 字节地址、Burst 模式、排空等）
- WinRing0 驱动未签名，Windows Defender / 内存完整性(HVCI) 会拦截加载，需自行授权
- 仅供个人研究学习，不对任何硬件损坏负责

## 致谢

- 上游项目：[4evergr8/ECsharp](https://github.com/4evergr8/ECsharp)（C# + WinRing0 方案）
- Mach 协议来源：荣耀电脑管家 `BIOS_EC_config.xml` 逆向
