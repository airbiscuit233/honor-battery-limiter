using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class TrayIconApp
{
	private class TrayApplicationContext : ApplicationContext
	{
		private NotifyIcon notifyIcon;

		private Icon trayIcon;

		private readonly Action onExitAction;

		private static SettingsForm settingsForm;

		public TrayApplicationContext(Action onExit)
		{
			onExitAction = onExit;
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				using Stream stream = executingAssembly.GetManifestResourceStream("HonorPCManagerisJ8.J8.ico");
				trayIcon = new Icon(stream);
			}
			catch
			{
				MessageBox.Show("无法加载嵌入图标 J8.ico", "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				ExitThread();
				return;
			}
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip
			{
				Items = 
				{
					{
						"设置...",
						(Image)null,
						(EventHandler)OnSettings
					},
					(ToolStripItem)new ToolStripSeparator(),
					{
						"退出",
						(Image)null,
						(EventHandler)OnExit
					}
				}
			};
			notifyIcon = new NotifyIcon
			{
				Icon = trayIcon,
				Text = "电池充电限制 80/70",
				ContextMenuStrip = contextMenuStrip,
				Visible = true
			};
		}

		private void OnSettings(object sender, EventArgs e)
		{
			if (settingsForm == null || settingsForm.IsDisposed)
			{
				settingsForm = new SettingsForm();
			}
			settingsForm.Show();
			settingsForm.Activate();
			settingsForm.BringToFront();
		}

		private void OnExit(object sender, EventArgs e)
		{
			if (onExitAction != null)
			{
				onExitAction();
			}
			notifyIcon.Visible = false;
			notifyIcon.Dispose();
			if (trayIcon != null)
			{
				trayIcon.Dispose();
			}
			Environment.Exit(0);
		}

		protected override void ExitThreadCore()
		{
			if (notifyIcon != null)
			{
				notifyIcon.Visible = false;
				notifyIcon.Dispose();
				if (trayIcon != null)
				{
					trayIcon.Dispose();
				}
			}
			base.ExitThreadCore();
		}
	}

	public static void RunTrayIconInBackground(Action onExitAction)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			Application.Run(new TrayApplicationContext(onExitAction));
		});
		thread.IsBackground = true;
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
	}
}
