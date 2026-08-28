using System;
using System.Diagnostics;
using Microsoft.Win32;

public static class AutoStartHelper
{
	public static void SetAutoStart(bool enable)
	{
		string friendlyName = AppDomain.CurrentDomain.FriendlyName;
		string fileName = Process.GetCurrentProcess().MainModule.FileName;
		try
		{
			RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (enable)
			{
				registryKey.SetValue(friendlyName, "\"" + fileName + "\"");
			}
			else if (registryKey.GetValue(friendlyName) != null)
			{
				registryKey.DeleteValue(friendlyName);
			}
		}
		catch
		{
		}
	}
}
