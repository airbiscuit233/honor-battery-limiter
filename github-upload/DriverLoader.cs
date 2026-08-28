using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

public static class DriverLoader
{
	private enum ServiceAccessRights : uint
	{
		SERVICE_ALL_ACCESS = 983551u
	}

	private enum ServiceControlManagerAccessRights : uint
	{
		SC_MANAGER_ALL_ACCESS = 983103u
	}

	private enum ServiceType : uint
	{
		SERVICE_KERNEL_DRIVER = 1u
	}

	private enum StartType : uint
	{
		SERVICE_SYSTEM_START = 1u
	}

	private enum ErrorControl : uint
	{
		SERVICE_ERROR_NORMAL = 1u
	}

	private enum ServiceControl : uint
	{
		SERVICE_CONTROL_STOP = 1u
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct ServiceStatus
	{
		public uint dwServiceType;

		public uint dwCurrentState;

		public uint dwControlsAccepted;

		public uint dwWin32ExitCode;

		public uint dwServiceSpecificExitCode;

		public uint dwCheckPoint;

		public uint dwWaitHint;
	}

	private enum FileAccessFlags : uint
	{
		GENERIC_READ = 2147483648u,
		GENERIC_WRITE = 1073741824u
	}

	private enum CreationDisposition : uint
	{
		OPEN_EXISTING = 3u
	}

	private enum FileAttributesFlags : uint
	{
		FILE_ATTRIBUTE_NORMAL = 0x80u
	}

	private static class NativeMethods
	{
		private const string ADVAPI = "advapi32.dll";

		private const string KERNEL = "kernel32.dll";

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern IntPtr OpenSCManager(string machineName, string databaseName, ServiceControlManagerAccessRights dwDesiredAccess);

		[DllImport("advapi32.dll")]
		public static extern bool CloseServiceHandle(IntPtr hSCObject);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, ServiceType dwServiceType, StartType dwStartType, ErrorControl dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, string lpdwTagId, string lpDependencies, string lpServiceStartName, string lpPassword);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool DeleteService(IntPtr hService);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, string[] lpServiceArgVectors);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool ControlService(IntPtr hService, ServiceControl dwControl, ref ServiceStatus lpServiceStatus);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateFile(string lpFileName, FileAccessFlags dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, CreationDisposition dwCreationDisposition, FileAttributesFlags dwFlagsAndAttributes, IntPtr hTemplateFile);
	}

	private const string DRIVER_ID = "WinRing0_1_2_0";

	private const int MaxRetry = 2;

	private const int ERROR_SERVICE_EXISTS = -2147023823;

	private const int ERROR_SERVICE_ALREADY_RUNNING = -2147023840;

	private static readonly string DriverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinRing0x64.sys");

	public static void InitializeDriver()
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < 2; i++)
		{
			if (TryOpenDriver())
			{
				stringBuilder.AppendLine("驱动已加载并可打开。");
				break;
			}
			if (!File.Exists(DriverPath))
			{
				stringBuilder.AppendLine("驱动文件不存在：" + DriverPath);
				break;
			}
			stringBuilder.AppendLine("第 " + (i + 1) + " 次尝试安装驱动...");
			if (!InstallDriver(DriverPath, out var errorMessage))
			{
				stringBuilder.AppendLine("安装失败：" + errorMessage);
				DeleteDriverService();
				Thread.Sleep(2000);
				continue;
			}
			Thread.Sleep(1000);
			if (TryOpenDriver())
			{
				stringBuilder.AppendLine("驱动安装并打开成功。");
				break;
			}
			stringBuilder.AppendLine("安装成功但无法打开，准备删除后重试...");
			DeleteDriverService();
			Thread.Sleep(2000);
		}
		Console.WriteLine(stringBuilder.ToString());
	}

	private static bool TryOpenDriver()
	{
		SafeFileHandle safeFileHandle = new SafeFileHandle(NativeMethods.CreateFile("\\\\.\\WinRing0_1_2_0", (FileAccessFlags)3221225472u, 0u, IntPtr.Zero, CreationDisposition.OPEN_EXISTING, FileAttributesFlags.FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), ownsHandle: true);
		if (safeFileHandle.IsInvalid)
		{
			safeFileHandle.Dispose();
			return false;
		}
		try
		{
			File.GetAccessControl("\\\\.\\WinRing0_1_2_0");
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool InstallDriver(string path, out string errorMessage)
	{
		IntPtr intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.SC_MANAGER_ALL_ACCESS);
		if (intPtr == IntPtr.Zero)
		{
			errorMessage = "OpenSCManager 返回 NULL。";
			return false;
		}
		IntPtr intPtr2 = NativeMethods.CreateService(intPtr, "WinRing0_1_2_0", "WinRing0_1_2_0", ServiceAccessRights.SERVICE_ALL_ACCESS, ServiceType.SERVICE_KERNEL_DRIVER, StartType.SERVICE_SYSTEM_START, ErrorControl.SERVICE_ERROR_NORMAL, path, null, null, null, null, null);
		if (intPtr2 == IntPtr.Zero)
		{
			int hRForLastWin32Error = Marshal.GetHRForLastWin32Error();
			if (hRForLastWin32Error == -2147023823)
			{
				errorMessage = "服务已存在。";
				NativeMethods.CloseServiceHandle(intPtr);
				return false;
			}
			errorMessage = "CreateService 错误: " + Marshal.GetExceptionForHR(hRForLastWin32Error).Message;
			NativeMethods.CloseServiceHandle(intPtr);
			return false;
		}
		if (!NativeMethods.StartService(intPtr2, 0u, null))
		{
			int hRForLastWin32Error2 = Marshal.GetHRForLastWin32Error();
			if (hRForLastWin32Error2 != -2147023840)
			{
				errorMessage = "StartService 错误: " + Marshal.GetExceptionForHR(hRForLastWin32Error2).Message;
				NativeMethods.CloseServiceHandle(intPtr2);
				NativeMethods.CloseServiceHandle(intPtr);
				return false;
			}
		}
		NativeMethods.CloseServiceHandle(intPtr2);
		NativeMethods.CloseServiceHandle(intPtr);
		try
		{
			FileSecurity accessControl = File.GetAccessControl("\\\\.\\WinRing0_1_2_0");
			accessControl.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
			File.SetAccessControl("\\\\.\\WinRing0_1_2_0", accessControl);
		}
		catch
		{
		}
		errorMessage = null;
		return true;
	}

	private static void DeleteDriverService()
	{
		IntPtr intPtr = NativeMethods.OpenSCManager(null, null, ServiceControlManagerAccessRights.SC_MANAGER_ALL_ACCESS);
		if (!(intPtr == IntPtr.Zero))
		{
			IntPtr intPtr2 = NativeMethods.OpenService(intPtr, "WinRing0_1_2_0", ServiceAccessRights.SERVICE_ALL_ACCESS);
			if (intPtr2 == IntPtr.Zero)
			{
				NativeMethods.CloseServiceHandle(intPtr);
				return;
			}
			ServiceStatus lpServiceStatus = default(ServiceStatus);
			NativeMethods.ControlService(intPtr2, ServiceControl.SERVICE_CONTROL_STOP, ref lpServiceStatus);
			NativeMethods.DeleteService(intPtr2);
			NativeMethods.CloseServiceHandle(intPtr2);
			NativeMethods.CloseServiceHandle(intPtr);
		}
	}
}
