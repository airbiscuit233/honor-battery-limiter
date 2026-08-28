using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Program
{
	private class TeeWriter : TextWriter
	{
		private TextWriter consoleWriter;

		private StreamWriter fileWriter;

		public override Encoding Encoding => Encoding.UTF8;

		public TeeWriter(TextWriter console, string file)
		{
			consoleWriter = console;
			string text = file;
			if (!Path.IsPathRooted(text))
			{
				text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, text);
			}
			FileStream stream = new FileStream(text, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			fileWriter = new StreamWriter(stream, Encoding.UTF8);
			fileWriter.AutoFlush = true;
		}

		public override void Write(char value)
		{
			consoleWriter.Write(value);
			fileWriter.Write(value);
		}

		public override void Write(string value)
		{
			consoleWriter.Write(value);
			fileWriter.Write(value);
		}

		public override void WriteLine(string value)
		{
			consoleWriter.WriteLine(value);
			fileWriter.WriteLine(value);
		}
	}

	private const int SW_HIDE = 0;

	private const int SW_SHOW = 5;

	[DllImport("kernel32.dll")]
	private static extern IntPtr GetConsoleWindow();

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[STAThread]
	private static void Main(string[] args)
	{
		IntPtr consoleWindow = GetConsoleWindow();
		ShowWindow(consoleWindow, 0);
		if (!IsAdministrator() && !CanOpenDriver())
		{
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo();
				processStartInfo.FileName = Assembly.GetExecutingAssembly().Location;
				processStartInfo.UseShellExecute = true;
				processStartInfo.Verb = "runas";
				ProcessStartInfo startInfo = processStartInfo;
				Process.Start(startInfo);
				return;
			}
			catch
			{
				return;
			}
		}
		if (args.Length > 0 && (args[0] == "dump" || args[0] == "write" || args[0] == "probe" || args[0] == "mmio" || args[0] == "portwrite" || args[0] == "cmos" || args[0] == "sram" || args[0] == "idx" || args[0] == "idxscan" || args[0] == "idxw" || args[0] == "mtest" || args[0] == "pread" || args[0] == "mach"))
		{
			IntPtr consoleWindow2 = GetConsoleWindow();
			ShowWindow(consoleWindow2, 5);
			for (int i = 0; i + 1 < args.Length; i++)
			{
				if (args[i] == "--out")
				{
					try
					{
						Console.SetOut(new TeeWriter(Console.Out, args[i + 1]));
						Console.SetError(Console.Out);
					}
					catch
					{
					}
					break;
				}
			}
			try
			{
				DriverLoader.InitializeDriver();
				EcAccess.Init();
				for (int j = 1; j + 2 < args.Length; j++)
				{
					if (args[j] == "--ports")
					{
						EcAccess.SetPorts(YamlConfigLoader.ParseHexByte(args[j + 1]), YamlConfigLoader.ParseHexByte(args[j + 2]));
						break;
					}
				}
				if (args[0] == "mach")
				{
					if (args.Length < 2)
					{
						Console.WriteLine("用法: mach read <地址> [<地址>...] | mach write <地址> <值> [<地址> <值>...] | mach watch <秒数> [起始] [结束]");
						return;
					}
					if (args[1] == "setburst")
					{
						if (args.Length < 3)
						{
							Console.WriteLine("用法: mach setburst <百分比>");
							return;
						}
						byte percent = YamlConfigLoader.ParsePercentByte(args[2]);
						EcAccess.SetLimitBurst(percent, 100);
					}
					else if (args[1] == "setlimit")
					{
						if (args.Length < 3)
						{
							Console.WriteLine("用法: mach setlimit <百分比>");
							return;
						}
						byte percent2 = YamlConfigLoader.ParsePercentByte(args[2]);
						EcAccess.SetLimitMach(percent2, 100);
					}
					else if (args[1] == "burstoff")
					{
						EcAccess.BurstOffMach();
					}
					else if (args[1] == "drain")
					{
						EcAccess.DrainMach();
					}
					else if (args[1] == "testwrite")
					{
						if (args.Length < 4)
						{
							Console.WriteLine("用法: mach testwrite <地址> <值>");
							return;
						}
						byte offset = YamlConfigLoader.ParseHexByte(args[2]);
						byte data = YamlConfigLoader.ParseHexByte(args[3]);
						EcAccess.TestWriteMach(offset, data, 100);
					}
					else if (args[1] == "dump")
					{
						Console.WriteLine("=== Mach EC 空间全扫描 (0x00-0xFF) ===");
						for (int k = 0; k < 256; k += 16)
						{
							string text = "0x" + k.ToString("X2") + ": ";
							for (int l = 0; l < 16; l++)
							{
								try
								{
									text = text + EcAccess.ReadEC_Mach((byte)(k + l), 50).ToString("X2") + " ";
								}
								catch
								{
									text += "?? ";
								}
							}
							Console.WriteLine(text);
						}
					}
					else if (args[1] == "watch")
					{
						int num = 60;
						int num2 = 0;
						int num3 = 255;
						try
						{
							if (args.Length >= 3)
							{
								num = Math.Max(1, Convert.ToInt32(args[2]));
							}
							if (args.Length >= 4)
							{
								num2 = Convert.ToInt32(args[3], 16);
							}
							if (args.Length >= 5)
							{
								num3 = Convert.ToInt32(args[4], 16);
							}
						}
						catch
						{
						}
						if (num2 < 0 || num2 > 255 || num3 < num2 || num3 > 255)
						{
							Console.WriteLine("参数越界: 起始/结束必须是 00-FF 且结束 >= 起始");
							return;
						}
						int[] array = new int[256];
						byte[] array2 = new byte[256];
						int[] array3 = new int[256];
						for (int m = 0; m < 256; m++)
						{
							array[m] = -1;
							array2[m] = 0;
							array3[m] = 0;
						}
						Stopwatch stopwatch = Stopwatch.StartNew();
						Console.WriteLine("=== Mach EC 监视: 区间 0x" + num2.ToString("X2") + "-0x" + num3.ToString("X2") + ", 时长 " + num + " 秒 ===");
						Console.WriteLine("=== 基线读入中... ===");
						for (int n = num2; n <= num3; n++)
						{
							try
							{
								array2[n] = (byte)(array[n] = EcAccess.ReadEC_MachQuiet((byte)n, 1));
							}
							catch (TimeoutException)
							{
							}
						}
						Console.Write("基线: ");
						for (int num4 = num2; num4 <= num3; num4++)
						{
							Console.Write(((array[num4] >= 0) ? array[num4].ToString("X2") : "??") + " ");
						}
						Console.WriteLine();
						Console.WriteLine("=== 监视开始: 请现在执行模式切换，然后等待结束 ===");
						int num5 = 0;
						while (stopwatch.Elapsed.TotalSeconds < (double)num)
						{
							num5++;
							for (int num6 = num2; num6 <= num3; num6++)
							{
								try
								{
									byte b = EcAccess.ReadEC_MachQuiet((byte)num6, 1);
									if (array[num6] < 0)
									{
										array[num6] = b;
										array2[num6] = b;
										Console.WriteLine("[+" + stopwatch.Elapsed.TotalSeconds.ToString("F2") + "s] EC[0x" + num6.ToString("X2") + "]: 首次读取成功 = 0x" + b.ToString("X2"));
									}
									else if (b != array2[num6])
									{
										array3[num6]++;
										Console.WriteLine("[+" + stopwatch.Elapsed.TotalSeconds.ToString("F2") + "s] EC[0x" + num6.ToString("X2") + "]: 0x" + array2[num6].ToString("X2") + " -> 0x" + b.ToString("X2"));
										array2[num6] = b;
									}
								}
								catch (TimeoutException)
								{
								}
							}
							if (num5 % 4 == 0)
							{
								Console.WriteLine("[+" + stopwatch.Elapsed.TotalSeconds.ToString("F2") + "s] 已扫描 " + num5 + " 轮");
							}
						}
						Console.WriteLine("=== 监视结束, 共 " + num5 + " 轮。变化汇总: ===");
						int num7 = 0;
						for (int num8 = num2; num8 <= num3; num8++)
						{
							if (array3[num8] > 0)
							{
								num7++;
								Console.WriteLine("EC[0x" + num8.ToString("X2") + "]: 初值 " + ((array[num8] >= 0) ? ("0x" + array[num8].ToString("X2")) : "??") + " -> 终值 0x" + array2[num8].ToString("X2") + "  (变化 " + array3[num8] + " 次)");
							}
						}
						if (num7 == 0)
						{
							Console.WriteLine("（区间内没有任何字节发生变化）");
						}
					}
					else if (args[1] == "read")
					{
						for (int num9 = 2; num9 < args.Length; num9++)
						{
							byte offset2 = YamlConfigLoader.ParseHexByte(args[num9]);
							byte b2 = EcAccess.ReadEC_Mach(offset2, 100);
							Console.WriteLine("EC[0x" + offset2.ToString("X2") + "] = 0x" + b2.ToString("X2"));
						}
					}
					else if (args[1] == "write")
					{
						List<string> list = new List<string>();
						for (int num10 = 2; num10 < args.Length; num10++)
						{
							if (args[num10] == "--out" && num10 + 1 < args.Length)
							{
								num10++;
							}
							else
							{
								list.Add(args[num10]);
							}
						}
						if (list.Count % 2 != 0)
						{
							Console.WriteLine("用法: mach write <地址> <值> [<地址> <值>...]");
							return;
						}
						for (int num11 = 0; num11 < list.Count; num11 += 2)
						{
							byte offset3 = YamlConfigLoader.ParseHexByte(list[num11]);
							byte data2 = YamlConfigLoader.ParseHexByte(list[num11 + 1]);
							EcAccess.WriteEC_Mach(offset3, data2, 100);
						}
					}
					else
					{
						Console.WriteLine("用法: mach read <地址> [<地址>...] | mach write <地址> <值> [<地址> <值>...] | mach watch <秒数> [起始] [结束]");
					}
				}
				else if (args[0] == "pread")
				{
					if (args.Length < 2)
					{
						Console.WriteLine("用法: pread <端口> [端口...]  （十六进制）");
						return;
					}
					for (int num12 = 1; num12 < args.Length; num12++)
					{
						ushort port = (ushort)Convert.ToInt32(args[num12], 16);
						byte b3 = EcAccess.ReadPortWord(port);
						Console.WriteLine("端口 0x" + port.ToString("X4") + " = 0x" + b3.ToString("X2"));
					}
				}
				else if (args[0] == "idxscan")
				{
					Console.WriteLine("=== 索引/数据口全扫描 (索引口 0x72, 数据口 0x73) ===");
					for (int num13 = 0; num13 < 256; num13 += 16)
					{
						string text2 = "0x" + num13.ToString("X2") + ": ";
						for (int num14 = 0; num14 < 16; num14++)
						{
							EcAccess.WritePortByte(114, (byte)(num13 + num14));
							Thread.Sleep(5);
							text2 = text2 + EcAccess.ReadPortByte(115).ToString("X2") + " ";
						}
						Console.WriteLine(text2);
					}
				}
				else if (args[0] == "idxw")
				{
					if (args.Length < 3)
					{
						Console.WriteLine("用法: idxw <索引> <值>  （十六进制）");
						return;
					}
					byte value = YamlConfigLoader.ParseHexByte(args[1]);
					byte b4 = YamlConfigLoader.ParseHexByte(args[2]);
					EcAccess.WritePortByte(114, value);
					Thread.Sleep(20);
					EcAccess.WritePortByte(115, b4);
					Thread.Sleep(20);
					EcAccess.WritePortByte(114, value);
					Thread.Sleep(20);
					byte b5 = EcAccess.ReadPortByte(115);
					Console.WriteLine("写入 IDX[0x" + value.ToString("X2") + "] = 0x" + b4.ToString("X2") + "，回读 0x" + b5.ToString("X2") + ((b5 == b4) ? " ✓" : " ✗"));
				}
				else if (args[0] == "idx")
				{
					if (args.Length < 2)
					{
						Console.WriteLine("用法: idx <索引> [字节数]  （十六进制；索引口默认 0x72，数据口 0x73）");
						return;
					}
					int num15 = Convert.ToInt32(args[1], 16);
					int num16 = ((args.Length <= 2) ? 1 : Convert.ToInt32(args[2], 16));
					for (int num17 = 0; num17 < num16; num17++)
					{
						EcAccess.WritePortByte(114, (byte)(num15 + num17));
						Thread.Sleep(20);
						byte b6 = EcAccess.ReadPortByte(115);
						Console.WriteLine("IDX[0x" + (num15 + num17).ToString("X2") + "] = 0x" + b6.ToString("X2"));
					}
				}
				else if (args[0] == "mtest")
				{
					EcAccess.MemTestTargeted();
				}
				else if (args[0] == "sram")
				{
					bool flag = args.Length >= 2 && args[1] == "if2";
					int num18 = ((!flag) ? 1 : 2);
					if (args.Length <= num18)
					{
						Console.WriteLine("用法: sram [if2] read <地址>... | sram [if2] write <地址> <值>...");
						return;
					}
					if (args[num18] == "read")
					{
						for (int num19 = num18 + 1; num19 < args.Length; num19++)
						{
							byte address = YamlConfigLoader.ParseHexByte(args[num19]);
							byte b7 = (flag ? EcAccess.ReadEC_IF2(address, 100) : EcAccess.ReadEC_SRAM(address, 100));
							Console.WriteLine("EC[0x" + address.ToString("X2") + "] = 0x" + b7.ToString("X2"));
						}
					}
					else if (args[num18] == "write")
					{
						if ((args.Length - num18 - 1) % 2 != 0)
						{
							Console.WriteLine("用法: sram [if2] write <地址> <值> [<地址> <值>...]");
							return;
						}
						for (int num20 = num18 + 1; num20 < args.Length; num20 += 2)
						{
							byte address2 = YamlConfigLoader.ParseHexByte(args[num20]);
							byte data3 = YamlConfigLoader.ParseHexByte(args[num20 + 1]);
							if (flag)
							{
								EcAccess.WriteEC_IF2(address2, data3, 100);
							}
							else
							{
								EcAccess.WriteEC_SRAM(address2, data3, 100);
							}
						}
					}
					else
					{
						Console.WriteLine("用法: sram [if2] read <地址>... | sram [if2] write <地址> <值>...");
					}
				}
				else if (args[0] == "portwrite")
				{
					if (args.Length < 3)
					{
						Console.WriteLine("用法: portwrite <端口> <值>  （十六进制）");
						return;
					}
					byte port2 = YamlConfigLoader.ParseHexByte(args[1]);
					byte value2 = YamlConfigLoader.ParseHexByte(args[2]);
					EcAccess.WritePortByte(port2, value2);
					Console.WriteLine("已写入端口 0x" + port2.ToString("X2") + " = 0x" + value2.ToString("X2"));
				}
				else if (args[0] == "cmos")
				{
					EcAccess.WritePortByte(112, 13);
					Thread.Sleep(50);
					byte b8 = EcAccess.ReadPortByte(113);
					EcAccess.WritePortByte(112, 0);
					Thread.Sleep(50);
					byte b9 = EcAccess.ReadPortByte(113);
					Console.WriteLine("CMOS 状态D (0x70=0x0D → 0x71) = 0x" + b8.ToString("X2"));
					Console.WriteLine("CMOS 状态A (0x70=0x00 → 0x71) = 0x" + b9.ToString("X2"));
					Console.WriteLine("判断: 状态D 的 bit7 应为 1 (CMOS 电池正常, 典型值 0x80~0xFF)");
					Console.WriteLine("      状态A 典型值为 0x26 (26.5ms 基准)");
				}
				else if (args[0] == "mmio")
				{
					if (!EcAccess.FindMemIoctls())
					{
						Console.WriteLine("MMIO 初始化失败。");
					}
					else if (args.Length >= 2 && args[1] == "dump")
					{
						int num21 = 0;
						int num22 = 256;
						if (args.Length >= 3)
						{
							num21 = Convert.ToInt32(args[2], 16);
						}
						if (args.Length >= 4)
						{
							num22 = Convert.ToInt32(args[3], 16);
						}
						if (num21 < 0 || num22 <= 0 || num21 + num22 > 256)
						{
							Console.WriteLine("参数越界：偏移+长度不能超过 0x100");
							return;
						}
						Console.WriteLine("=== EC MMIO 窗口 (0x" + 4269856512u.ToString("X8") + ") 偏移 0x" + num21.ToString("X2") + " - 0x" + (num21 + num22 - 1).ToString("X2") + " ===");
						for (int num23 = 0; num23 < num22; num23++)
						{
							if (num23 % 16 == 0)
							{
								if (num23 > 0)
								{
									Console.WriteLine();
								}
								Console.Write("0x" + ((num21 + num23) & 0xFF0).ToString("X3") + ": ");
							}
							Console.Write(EcAccess.ReadMmio((byte)(num21 + num23)).ToString("X2") + " ");
						}
						Console.WriteLine();
					}
					else if (args.Length >= 2 && args[1] == "write")
					{
						if ((args.Length - 2) % 2 != 0 || args.Length < 4)
						{
							Console.WriteLine("用法: mmio write <偏移> <值> [<偏移> <值> ...]  （十六进制）");
							return;
						}
						for (int num24 = 2; num24 < args.Length; num24 += 2)
						{
							byte offset4 = YamlConfigLoader.ParseHexByte(args[num24]);
							byte value3 = YamlConfigLoader.ParseHexByte(args[num24 + 1]);
							EcAccess.WriteMmio(offset4, value3);
						}
					}
					else
					{
						Console.WriteLine("用法: mmio dump [起始偏移] [长度] | mmio write <偏移> <值> [...]");
					}
				}
				else if (args[0] == "probe")
				{
					ushort port3 = 98;
					ushort port4 = 102;
					if (args.Length >= 3)
					{
						port3 = (ushort)Convert.ToInt32(args[1], 16);
						port4 = (ushort)Convert.ToInt32(args[2], 16);
					}
					Console.WriteLine("=== EC 端口探测 (数据端口=0x" + port3.ToString("X4") + ", 状态端口=0x" + port4.ToString("X4") + ", 采样 10 次/100ms) ===");
					for (int num25 = 0; num25 < 10; num25++)
					{
						byte b10 = EcAccess.ReadPortWord(port4);
						byte b11 = EcAccess.ReadPortWord(port3);
						Console.WriteLine("第 " + (num25 + 1) + " 次: 0x" + port4.ToString("X4") + "=" + b10.ToString("X2") + "  0x" + port3.ToString("X4") + "=" + b11.ToString("X2"));
						Thread.Sleep(100);
					}
				}
				else if (args[0] == "dump")
				{
					if (args.Length == 1)
					{
						byte[] array4 = new byte[7] { 146, 147, 92, 93, 33, 36, 37 };
						byte[] array5 = array4;
						for (int num26 = 0; num26 < array5.Length; num26++)
						{
							byte address3 = array5[num26];
							Console.WriteLine("EC[0x" + address3.ToString("X2") + "] = 0x" + EcAccess.ReadEC(address3, 100).ToString("X2"));
						}
					}
					else
					{
						for (int num27 = 1; num27 < args.Length; num27++)
						{
							byte address4 = YamlConfigLoader.ParseHexByte(args[num27]);
							Console.WriteLine("EC[0x" + address4.ToString("X2") + "] = 0x" + EcAccess.ReadEC(address4, 100).ToString("X2"));
						}
					}
				}
				else
				{
					if ((args.Length - 1) % 2 != 0)
					{
						Console.WriteLine("用法: HonorPCManagerisJ8.exe write <地址> <值> [<地址> <值> ...]");
						return;
					}
					for (int num28 = 1; num28 < args.Length; num28 += 2)
					{
						byte address5 = YamlConfigLoader.ParseHexByte(args[num28]);
						byte data4 = YamlConfigLoader.ParseHexByte(args[num28 + 1]);
						Console.WriteLine("写入 EC[0x" + address5.ToString("X2") + "] = 0x" + data4.ToString("X2"));
						EcAccess.WriteEC(address5, data4, 100);
					}
				}
			}
			catch (Exception ex3)
			{
				Console.WriteLine("操作失败: " + ex3.Message);
			}
			Console.WriteLine();
			bool flag2 = false;
			for (int num29 = 0; num29 + 1 < args.Length; num29++)
			{
				if (args[num29] == "--out")
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				Console.WriteLine("按回车键退出...");
				Console.ReadLine();
			}
			return;
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
		{
			try
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.txt"), string.Concat(DateTime.Now, "\r\n", e.ExceptionObject));
			}
			catch
			{
			}
		};
		ConfigData configData = YamlConfigLoader.LoadConfig();
		int timeout = configData.timeout;
		int wait = configData.wait;
		bool debug = configData.debug;
		byte b12 = (byte)((uint)configData.limit & 0xFFu);
		byte data5 = (byte)((uint)configData.resume & 0xFFu);
		TrayIconApp.RunTrayIconInBackground(delegate
		{
			try
			{
				DriverLoader.InitializeDriver();
				EcAccess.Init();
				EcAccess.BurstOffMach();
				EcAccess.WriteEC_Mach(229, 100, wait);
			}
			catch (Exception ex5)
			{
				MessageBox.Show("EC 写入失败：" + ex5.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		});
		if (debug)
		{
			ShowWindow(consoleWindow, 5);
		}
		AutoStartHelper.SetAutoStart(configData.startup);
		while (true)
		{
			try
			{
				DriverLoader.InitializeDriver();
				EcAccess.Init();
				EcAccess.BurstOffMach();
				EcAccess.WriteEC_Mach(228, data5, wait);
				EcAccess.WriteEC_Mach(229, b12, wait);
				byte b13 = EcAccess.ReadEC_Mach(229, wait);
				Console.WriteLine("充电限制(停/恢) = 0x" + b13.ToString("X2") + "/0x" + data5.ToString("X2") + ((b13 == b12) ? " ✓" : " ✗"));
			}
			catch (Exception ex4)
			{
				Console.WriteLine("EC 写入失败：" + ex4.Message);
			}
			Console.WriteLine("跑一次：" + DateTime.Now);
			Thread.Sleep(timeout);
		}
	}

	private static bool IsAdministrator()
	{
		using WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent();
		WindowsPrincipal windowsPrincipal = new WindowsPrincipal(ntIdentity);
		return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
	}

	private static bool CanOpenDriver()
	{
		try
		{
			IntPtr h = CreateFileW("\\\\.\\WinRing0_1_2_0", 3221225472u, 0u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
			if (h.ToInt64() == -1)
			{
				return false;
			}
			CloseHandle(h);
			return true;
		}
		catch
		{
			return false;
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tpl);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);
}
