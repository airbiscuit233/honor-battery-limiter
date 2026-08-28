using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

public static class EcAccess
{
	private const uint IOCTL_READ_PORT = 2621464780u;

	private const uint IOCTL_WRITE_PORT = 2621481176u;

	public const uint EC_MMIO_BASE = 4269856512u;

	public const ushort MACH_EC_BASE = 512;

	private static SafeFileHandle handle;

	private static uint memReadIoctl;

	private static uint memWriteIoctl;

	private static ushort MACH_CMD_PORT = 605;

	private static ushort MACH_DATA_PORT = 604;

	private static byte ecCmdPort = 102;

	private static byte ecDataPort = 98;

	public static void Init()
	{
		if (handle == null || handle.IsInvalid)
		{
			handle = new SafeFileHandle(CreateFileW("\\\\.\\WinRing0_1_2_0", 3221225472u, 0u, IntPtr.Zero, 3u, 0u, IntPtr.Zero), ownsHandle: true);
			if (handle.IsInvalid)
			{
				throw new Exception("WinRing0 驱动设备打开失败（驱动未加载？）");
			}
			Console.WriteLine("驱动设备打开成功");
		}
	}

	public static void SetPorts(ushort dataPort, ushort cmdPort)
	{
		MACH_DATA_PORT = dataPort;
		MACH_CMD_PORT = cmdPort;
	}

	private static void WaitIbfClearWord(ushort port)
	{
		for (int i = 0; i < 100; i++)
		{
			byte b = ReadPortWord(port);
			if ((b & 2) == 0)
			{
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("等待 IBF 清零超时");
	}

	private static void WaitObfSetWord(ushort port)
	{
		for (int i = 0; i < 100; i++)
		{
			byte b = ReadPortWord(port);
			if (((uint)b & (true ? 1u : 0u)) != 0)
			{
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("等待 OBF 置位超时");
	}

	private static void WaitObfClearWord(ushort port)
	{
		for (int i = 0; i < 100; i++)
		{
			byte b = ReadPortWord(port);
			if ((b & 1) == 0)
			{
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("等待 OBF 清零超时");
	}

	public static byte ReadEC_Mach(byte offset, int wait)
	{
		return ReadEC_MachImpl(offset, wait, verbose: true);
	}

	public static byte ReadEC_MachQuiet(byte offset, int wait)
	{
		return ReadEC_MachImpl(offset, wait, verbose: false);
	}

	private static byte ReadEC_MachImpl(byte offset, int wait, bool verbose)
	{
		ushort num = (ushort)(512 + offset);
		for (int i = 0; i < 3; i++)
		{
			try
			{
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				WaitObfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				WritePortWord(MACH_CMD_PORT, 128);
				Thread.Sleep(wait);
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				WritePortWord(MACH_DATA_PORT, (byte)(num & 0xFFu));
				Thread.Sleep(wait);
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				WaitObfSetWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				byte result = ReadPortWord(MACH_DATA_PORT);
				if (verbose)
				{
					Console.WriteLine("[Mach/1B] EC[0x" + offset.ToString("X2") + "] (addr=0x" + num.ToString("X4") + ") = 0x" + result.ToString("X2"));
				}
				return result;
			}
			catch (TimeoutException)
			{
				if (verbose)
				{
					Console.WriteLine("[Mach/1B] 第 " + (i + 1) + " 次超时，重试...");
				}
				Thread.Sleep(wait * 2);
			}
		}
		throw new TimeoutException("Mach 协议读取失败（1 字节地址 3 次尝试均超时）");
	}

	public static void WriteEC_Mach(byte offset, byte data, int wait)
	{
		WaitIbfClearWord(MACH_CMD_PORT);
		Thread.Sleep(wait);
		WaitObfClearWord(MACH_CMD_PORT);
		Thread.Sleep(wait);
		WritePortWord(MACH_CMD_PORT, 129);
		Thread.Sleep(wait);
		WaitIbfClearWord(MACH_CMD_PORT);
		Thread.Sleep(wait);
		WritePortWord(MACH_DATA_PORT, (byte)(offset & 0xFFu));
		Thread.Sleep(wait);
		WaitIbfClearWord(MACH_CMD_PORT);
		Thread.Sleep(wait);
		WritePortWord(MACH_DATA_PORT, data);
		Thread.Sleep(wait);
		WaitIbfClearWord(MACH_CMD_PORT);
		Thread.Sleep(wait);
		Console.WriteLine("[Mach] 写 EC[0x" + offset.ToString("X2") + "] = 0x" + data.ToString("X2"));
		Thread.Sleep(1000);
		byte b = ReadEC_Mach(offset, wait);
		Console.WriteLine("[Mach] 回读 0x" + b.ToString("X2") + ((b == data) ? " ✓" : " ✗ 不一致"));
	}

	public static void BurstOffMach()
	{
		Console.WriteLine("[Mach] 发送 0x83 (Burst 禁用) 到命令口 " + MACH_CMD_PORT.ToString("X4") + "...");
		try
		{
			WaitIbfClearWord(MACH_CMD_PORT);
			WritePortWord(MACH_CMD_PORT, 131);
			Thread.Sleep(200);
			Console.WriteLine("[Mach] 状态=0x" + ReadPortWord(MACH_CMD_PORT).ToString("X2") + " (bit7=0 表示 Burst 已退出)");
		}
		catch (TimeoutException ex)
		{
			Console.WriteLine("[Mach] Burst 禁用超时: " + ex.Message + "（直接发送）");
			WritePortWord(MACH_CMD_PORT, 131);
			Thread.Sleep(200);
		}
	}

	public static void DrainMach()
	{
		Console.WriteLine("=== Mach 接口排空 (读取挂起数据) ===");
		for (int i = 0; i < 12; i++)
		{
			byte b = ReadPortWord(MACH_CMD_PORT);
			byte b2 = ReadPortWord(MACH_DATA_PORT);
			Console.WriteLine("第" + (i + 1) + "次: 状态=0x" + b.ToString("X2") + " 数据=0x" + b2.ToString("X2"));
			if (i >= 3 && (b & 1) == 0 && (b & 2) == 0)
			{
				break;
			}
			Thread.Sleep(50);
		}
		Console.WriteLine("[排空] 完成");
	}

	public static void SetLimitBurst(byte percent, int wait)
	{
		Console.WriteLine("=== Burst 锁序列测试: 0x82 → 写 0xE5=" + percent.ToString("X2") + " → 0x83 ===");
		try
		{
			WaitIbfClearWord(MACH_CMD_PORT);
			WritePortWord(MACH_CMD_PORT, 130);
			Thread.Sleep(200);
			Console.WriteLine("[1] 已发 0x82");
			WaitIbfClearWord(MACH_CMD_PORT);
			WritePortWord(MACH_CMD_PORT, 129);
			Thread.Sleep(wait);
			WaitIbfClearWord(MACH_CMD_PORT);
			WritePortWord(MACH_DATA_PORT, 229);
			Thread.Sleep(wait);
			WaitIbfClearWord(MACH_CMD_PORT);
			WritePortWord(MACH_DATA_PORT, percent);
			Thread.Sleep(wait);
			WaitIbfClearWord(MACH_CMD_PORT);
			Console.WriteLine("[2] 已写 EC[0xE5] = 0x" + percent.ToString("X2"));
			WritePortWord(MACH_CMD_PORT, 131);
			Thread.Sleep(200);
			Console.WriteLine("[3] 已发 0x83");
			Thread.Sleep(1000);
			byte b = ReadEC_Mach(229, wait);
			Console.WriteLine("[结果] EC[0xE5] = 0x" + b.ToString("X2") + ((b == percent) ? " ✓" : " ✗"));
		}
		catch (TimeoutException ex)
		{
			Console.WriteLine("超时: " + ex.Message);
		}
	}

	public static void SetLimitMach(byte percent, int wait)
	{
		Console.WriteLine("=== 一键设置充电阈值 " + percent + "% (0x" + percent.ToString("X2") + ") ===");
		DrainMach();
		BurstOffMach();
		WriteEC_Mach(229, percent, wait);
		byte b = ReadEC_Mach(229, wait);
		Console.WriteLine("[最终] EC[0xE5] = 0x" + b.ToString("X2") + ((b == percent) ? " ✓ 充电阈值设置成功！" : " ✗ 未生效"));
	}

	public static void SetDualThreshold(byte resumePercent, byte limitPercent, int wait)
	{
		WriteEC_Mach(228, resumePercent, wait);
		WriteEC_Mach(229, limitPercent, wait);
	}

	public static void TestWriteMach(byte offset, byte data, int wait)
	{
		Console.WriteLine("=== Mach 写入序列测试 (目标 EC[0x" + offset.ToString("X2") + "], 数据 0x" + data.ToString("X2") + ") ===");
		for (int i = 1; i <= 5; i++)
		{
			try
			{
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				WaitObfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				byte value = (byte)(i switch
				{
					5 => 128u, 
					3 => 130u, 
					_ => 129u, 
				});
				WritePortWord(MACH_CMD_PORT, value);
				Thread.Sleep(wait);
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				switch (i)
				{
				case 2:
					WritePortWord(MACH_DATA_PORT, (byte)(offset & 0xFFu));
					Thread.Sleep(wait);
					WaitIbfClearWord(MACH_CMD_PORT);
					WritePortWord(MACH_DATA_PORT, (byte)(512 + offset >> 8));
					break;
				case 4:
					WritePortWord(MACH_DATA_PORT, (byte)(512 + offset >> 8));
					Thread.Sleep(wait);
					WaitIbfClearWord(MACH_CMD_PORT);
					WritePortWord(MACH_DATA_PORT, (byte)(offset & 0xFFu));
					break;
				default:
					WritePortWord(MACH_DATA_PORT, (byte)(offset & 0xFFu));
					break;
				}
				Thread.Sleep(wait);
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				WritePortWord(MACH_DATA_PORT, data);
				Thread.Sleep(wait);
				WaitIbfClearWord(MACH_CMD_PORT);
				Thread.Sleep(wait);
				Thread.Sleep(1000);
				byte b = ReadEC_Mach(offset, wait);
				Console.WriteLine("[序列" + i + "] 写后回读 = 0x" + b.ToString("X2") + ((b == data) ? " ✓ 写入生效!" : " ✗"));
			}
			catch (TimeoutException ex)
			{
				Console.WriteLine("[序列" + i + "] 超时: " + ex.Message);
			}
			Thread.Sleep(1000);
		}
	}

	private static void WaitIbfClear()
	{
		for (int i = 0; i < 100; i++)
		{
			byte b = ReadPort(ecCmdPort);
			if ((b & 2) == 0)
			{
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("等待 IBF 清零超时");
	}

	private static void WaitObfSet()
	{
		for (int i = 0; i < 100; i++)
		{
			byte b = ReadPort(ecCmdPort);
			if (((uint)b & (true ? 1u : 0u)) != 0)
			{
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("等待 OBF 置位超时");
	}

	private static void WaitObfClear()
	{
		for (int i = 0; i < 100; i++)
		{
			byte b = ReadPort(ecCmdPort);
			if ((b & 1) == 0)
			{
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("等待 OBF 清零超时");
	}

	public static byte ReadEC(byte address, int wait)
	{
		WaitIbfClear();
		Thread.Sleep(wait);
		WaitObfClear();
		Thread.Sleep(wait);
		WritePort(ecCmdPort, 128);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		WritePort(ecDataPort, address);
		Thread.Sleep(wait);
		WaitObfSet();
		Thread.Sleep(wait);
		return ReadPort(ecDataPort);
	}

	public static void WriteEC(byte address, byte data, int wait)
	{
		WaitIbfClear();
		Thread.Sleep(wait);
		WaitObfClear();
		Thread.Sleep(wait);
		WritePort(ecCmdPort, 129);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		WritePort(ecDataPort, address);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		WritePort(ecDataPort, data);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		Console.WriteLine("写 EC[0x" + address.ToString("X2") + "] = 0x" + data.ToString("X2") + "，回读 0x" + ReadEC(address, wait).ToString("X2"));
	}

	public static byte ReadEC_SRAM(byte address, int wait)
	{
		for (int i = 0; i < 3; i++)
		{
			try
			{
				WaitIbfClear();
				Thread.Sleep(wait);
				WaitObfClear();
				Thread.Sleep(wait);
				WritePort(ecCmdPort, 126);
				Thread.Sleep(wait);
				WaitIbfClear();
				Thread.Sleep(wait);
				WritePort(ecDataPort, 128);
				Thread.Sleep(wait);
				WaitIbfClear();
				Thread.Sleep(wait);
				WritePort(ecDataPort, address);
				Thread.Sleep(wait);
				WaitIbfClear();
				Thread.Sleep(wait);
				WaitObfSet();
				Thread.Sleep(wait);
				byte b = ReadPort(ecDataPort);
				if (b == 184)
				{
					throw new TimeoutException("接口无响应（0xB8 恒值）");
				}
				Console.WriteLine("[SRAM/IF1] EC[0x" + address.ToString("X2") + "] = 0x" + b.ToString("X2"));
				return b;
			}
			catch (TimeoutException)
			{
				Console.WriteLine("[SRAM/IF1] 第 " + (i + 1) + " 次超时，重试...");
				Thread.Sleep(wait * 2);
			}
		}
		throw new TimeoutException("SRAM 读 EC 0x" + address.ToString("X2") + " 失败（3 次尝试均超时）");
	}

	public static void WriteEC_SRAM(byte address, byte data, int wait)
	{
		WaitIbfClear();
		Thread.Sleep(wait);
		WaitObfClear();
		Thread.Sleep(wait);
		WritePort(ecCmdPort, 126);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		WritePort(ecDataPort, 129);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		WritePort(ecDataPort, address);
		Thread.Sleep(wait);
		WaitIbfClear();
		Thread.Sleep(wait);
		WritePort(ecDataPort, data);
		Thread.Sleep(wait);
		Console.WriteLine("[SRAM/IF1] 写 EC[0x" + address.ToString("X2") + "] = 0x" + data.ToString("X2"));
	}

	public static byte ReadEC_IF2(byte address, int wait)
	{
		Console.WriteLine("接口2 (0x68/0x6C) 无响应（0xFF 恒值），返回 0xFF");
		return byte.MaxValue;
	}

	public static void WriteEC_IF2(byte address, byte data, int wait)
	{
		Console.WriteLine("接口2 (0x68/0x6C) 无响应，写入跳过");
	}

	public static bool FindMemIoctls()
	{
		try
		{
			memReadIoctl = 2621456428u;
			memWriteIoctl = 2621489196u;
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static byte ReadMmio(byte offset)
	{
		byte[] array = new byte[5];
		BitConverter.GetBytes((uint)(-25110784 + offset)).CopyTo(array, 0);
		byte[] array2 = new byte[1];
		uint lpBytesReturned = 0u;
		if (!DeviceIoControl(handle, memReadIoctl, array, 4u, array2, 1u, ref lpBytesReturned, IntPtr.Zero))
		{
			throw new Exception("MMIO 读取失败(0x" + offset.ToString("X2") + "): " + Marshal.GetLastWin32Error());
		}
		return array2[0];
	}

	public static void WriteMmio(byte offset, byte value)
	{
		byte[] array = new byte[5];
		BitConverter.GetBytes((uint)(-25110784 + offset)).CopyTo(array, 0);
		array[4] = value;
		uint lpBytesReturned = 0u;
		if (!DeviceIoControl(handle, memWriteIoctl, array, 5u, null, 0u, ref lpBytesReturned, IntPtr.Zero))
		{
			throw new Exception("MMIO 写入失败(0x" + offset.ToString("X2") + "): " + Marshal.GetLastWin32Error());
		}
		Thread.Sleep(20);
		byte b = ReadMmio(offset);
		Console.WriteLine("写入 EC_MMIO[0x" + offset.ToString("X2") + "] = 0x" + value.ToString("X2") + "，回读 0x" + b.ToString("X2") + ((b == value) ? " ✓" : " ✗ 不一致"));
	}

	public static void MemTestTargeted()
	{
		Console.WriteLine("=== MMIO 定向测试 (0x" + 4269856512u.ToString("X8") + ") ===");
		if (!FindMemIoctls())
		{
			Console.WriteLine("MMIO 初始化失败。");
			return;
		}
		for (int i = 0; i < 16; i++)
		{
			byte b = ReadMmio((byte)i);
			Console.WriteLine("EC_MMIO[0x" + i.ToString("X2") + "] = 0x" + b.ToString("X2"));
		}
	}

	public static byte ReadPortWord(ushort port)
	{
		byte[] bytes = BitConverter.GetBytes((uint)port);
		byte[] array = new byte[1];
		uint lpBytesReturned = 0u;
		if (!DeviceIoControl(handle, 2621464780u, bytes, 4u, array, 1u, ref lpBytesReturned, IntPtr.Zero))
		{
			throw new Exception("端口读取失败 0x" + port.ToString("X4") + ": " + Marshal.GetLastWin32Error());
		}
		return array[0];
	}

	public static void WritePortWord(ushort port, byte value)
	{
		byte[] array = new byte[5];
		BitConverter.GetBytes((uint)port).CopyTo(array, 0);
		array[4] = value;
		uint lpBytesReturned = 0u;
		if (!DeviceIoControl(handle, 2621481176u, array, 5u, null, 0u, ref lpBytesReturned, IntPtr.Zero))
		{
			throw new Exception("端口写入失败 0x" + port.ToString("X4") + ": " + Marshal.GetLastWin32Error());
		}
	}

	public static byte ReadPortByte(byte port)
	{
		return ReadPortWord(port);
	}

	public static void WritePortByte(byte port, byte value)
	{
		WritePortWord(port, value);
	}

	public static byte ReadPort(byte port)
	{
		return ReadPortWord(port);
	}

	public static void WritePort(byte port, byte value)
	{
		WritePortWord(port, value);
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tpl);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, byte[] lpInBuffer, uint nInBufferSize, byte[] lpOutBuffer, uint nOutBufferSize, ref uint lpBytesReturned, IntPtr lpOverlapped);
}
