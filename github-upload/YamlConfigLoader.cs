using System;
using System.IO;
using System.Text;

public static class YamlConfigLoader
{
	private static bool exeDirWritable;

	private static string ExeConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.yaml");

	private static string LocalConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryLimiter");

	private static string LocalConfigPath => Path.Combine(LocalConfigDir, "config.yaml");

	private static string ConfigPath
	{
		get
		{
			if (!exeDirWritable)
			{
				return LocalConfigPath;
			}
			return ExeConfigPath;
		}
	}

	private static bool DirWritable(string dir)
	{
		try
		{
			Directory.CreateDirectory(dir);
			string path = Path.Combine(dir, ".probe" + Guid.NewGuid().ToString("N"));
			File.WriteAllText(path, "x");
			File.Delete(path);
			return true;
		}
		catch
		{
			return false;
		}
	}

	static YamlConfigLoader()
	{
		exeDirWritable = DirWritable(AppDomain.CurrentDomain.BaseDirectory);
	}

	public static ConfigData LoadConfig()
	{
		ConfigData configData = new ConfigData();
		string configPath = ConfigPath;
		try
		{
			if (!File.Exists(configPath) && !exeDirWritable && File.Exists(ExeConfigPath))
			{
				Directory.CreateDirectory(LocalConfigDir);
				File.Copy(ExeConfigPath, configPath, overwrite: true);
			}
			if (!File.Exists(configPath))
			{
				return configData;
			}
			string[] array = File.ReadAllLines(configPath, Encoding.UTF8);
			foreach (string text in array)
			{
				string text2 = text.Trim();
				if (text2.Length == 0 || text2.StartsWith("#") || text2.StartsWith("//"))
				{
					continue;
				}
				int num = text2.IndexOf(':');
				if (num <= 0)
				{
					continue;
				}
				string text3 = text2.Substring(0, num).Trim().ToLowerInvariant();
				string text4 = text2.Substring(num + 1).Trim();
				int num2 = text4.IndexOf("#");
				if (num2 >= 0)
				{
					text4 = text4.Substring(0, num2).Trim();
				}
				switch (text3)
				{
				case "timeout":
				{
					if (int.TryParse(text4, out var result6) && result6 > 0)
					{
						configData.timeout = result6;
					}
					break;
				}
				case "startup":
				{
					if (bool.TryParse(text4, out var result2))
					{
						configData.startup = result2;
					}
					break;
				}
				case "debug":
				{
					if (bool.TryParse(text4, out var result3))
					{
						configData.debug = result3;
					}
					break;
				}
				case "wait":
				{
					if (int.TryParse(text4, out var result4) && result4 > 0)
					{
						configData.wait = result4;
					}
					break;
				}
				case "limit":
				{
					if (int.TryParse(text4, out var result5) && result5 >= 0 && result5 <= 100)
					{
						configData.limit = result5;
					}
					break;
				}
				case "resume":
				{
					if (int.TryParse(text4, out var result) && result >= 0 && result <= 100)
					{
						configData.resume = result;
					}
					break;
				}
				}
			}
		}
		catch
		{
		}
		return configData;
	}

	public static void SaveConfig(ConfigData c)
	{
		try
		{
			string configPath = ConfigPath;
			if (!exeDirWritable)
			{
				Directory.CreateDirectory(LocalConfigDir);
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("# battery limiter config");
			stringBuilder.AppendLine("startup: " + c.startup);
			stringBuilder.AppendLine("debug: " + c.debug);
			stringBuilder.AppendLine("timeout: " + c.timeout);
			stringBuilder.AppendLine("wait: " + c.wait);
			stringBuilder.AppendLine("limit: " + c.limit + "   # stop charging at this % (EC[0xE5])");
			stringBuilder.AppendLine("resume: " + c.resume + "   # resume charging below this % (EC[0xE4])");
			File.WriteAllText(configPath, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	public static byte ParseHexByte(string s)
	{
		s = s.Trim();
		if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			s = s.Substring(2);
		}
		try
		{
			return (byte)Convert.ToInt32(s, 16);
		}
		catch
		{
			return (byte)Convert.ToInt32(s, 10);
		}
	}

	// 修正: 百分比按十进制解析,自动限制 0-100。
	// 例: setlimit 80 → 0x50(80%); setlimit 50 → 0x32(50%); "0x50" 仍按十六进制 → 0x50(80%)
	public static byte ParsePercentByte(string s)
	{
		s = s.Trim();
		bool isHex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
		if (isHex)
		{
			s = s.Substring(2);
		}
		int v;
		if (int.TryParse(s, out v))
		{
			if (isHex)
			{
				try
				{
					v = Convert.ToInt32(s, 16);
				}
				catch
				{
				}
			}
			if (v < 0)
			{
				v = 0;
			}
			if (v > 100)
			{
				v = 100;
			}
			return (byte)v;
		}
		return ParseHexByte(s);
	}
}
