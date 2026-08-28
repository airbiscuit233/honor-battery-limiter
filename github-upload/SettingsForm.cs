using System;
using System.Drawing;
using System.Windows.Forms;

public class SettingsForm : Form
{
	private Label lblEcStatus;

	private GroupBox grpThreshold;

	private Label lblLimit;

	private NumericUpDown numLimit;

	private Label lblResume;

	private NumericUpDown numResume;

	private Label lblHint;

	private Button btnApply;

	private Button btnFull;

	private CheckBox chkStartup;

	private Label lblState;

	private int wait;

	public SettingsForm()
	{
		Text = "电池充电限制设置";
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.StartPosition = FormStartPosition.CenterScreen;
		base.ClientSize = new Size(360, 300);
		Font = new Font("Microsoft YaHei UI", 9f);
		try
		{
			wait = YamlConfigLoader.LoadConfig().wait;
			if (wait <= 0)
			{
				wait = 100;
			}
		}
		catch
		{
			wait = 100;
		}
		lblEcStatus = new Label
		{
			Location = new Point(15, 12),
			Size = new Size(330, 20),
			Text = "当前 EC 状态: 读取中..."
		};
		grpThreshold = new GroupBox
		{
			Location = new Point(15, 40),
			Size = new Size(330, 100),
			Text = "充电阈值"
		};
		lblLimit = new Label
		{
			Location = new Point(12, 25),
			Size = new Size(140, 20),
			Text = "停止充电上限 %"
		};
		numLimit = new NumericUpDown
		{
			Location = new Point(155, 23),
			Size = new Size(70, 23),
			Minimum = 0m,
			Maximum = 100m,
			Value = 80m
		};
		lblResume = new Label
		{
			Location = new Point(12, 50),
			Size = new Size(140, 20),
			Text = "恢复充电下限 %"
		};
		numResume = new NumericUpDown
		{
			Location = new Point(155, 48),
			Size = new Size(70, 23),
			Minimum = 0m,
			Maximum = 100m,
			Value = 70m
		};
		lblHint = new Label
		{
			Location = new Point(12, 74),
			Size = new Size(300, 22),
			Text = "低于下限开始充电，充到上限停止（成对生效）",
			ForeColor = Color.DimGray
		};
		grpThreshold.Controls.AddRange(new Control[5] { lblLimit, numLimit, lblResume, numResume, lblHint });
		btnApply = new Button
		{
			Location = new Point(15, 155),
			Size = new Size(110, 32),
			Text = "应用并保存"
		};
		btnFull = new Button
		{
			Location = new Point(135, 155),
			Size = new Size(110, 32),
			Text = "恢复100%"
		};
		chkStartup = new CheckBox
		{
			Location = new Point(15, 200),
			Size = new Size(200, 22),
			Text = "开机自动启动"
		};
		lblState = new Label
		{
			Location = new Point(15, 230),
			Size = new Size(330, 50),
			Text = "",
			ForeColor = Color.FromArgb(0, 122, 204)
		};
		base.Controls.AddRange(new Control[6] { lblEcStatus, grpThreshold, btnApply, btnFull, chkStartup, lblState });
		btnApply.Click += OnApply;
		btnFull.Click += OnFull;
		base.Load += OnLoad;
	}

	private void OnLoad(object sender, EventArgs e)
	{
		try
		{
			ConfigData configData = YamlConfigLoader.LoadConfig();
			numLimit.Value = Math.Min(100, Math.Max(0, configData.limit));
			numResume.Value = Math.Min(100, Math.Max(0, configData.resume));
			chkStartup.Checked = configData.startup;
		}
		catch
		{
		}
		RefreshEcStatus();
	}

	private void RefreshEcStatus()
	{
		try
		{
			EcAccess.Init();
			byte b = EcAccess.ReadEC_Mach(228, wait);
			byte b2 = EcAccess.ReadEC_Mach(229, wait);
			lblEcStatus.Text = "当前 EC 阈值: 停止 " + b2 + "%  恢复 " + b + "%";
		}
		catch (Exception ex)
		{
			lblEcStatus.Text = "当前 EC 状态: 读取失败 (" + ex.Message + ")";
		}
	}

	private void OnApply(object sender, EventArgs e)
	{
		int num = (int)numLimit.Value;
		int num2 = (int)numResume.Value;
		if (num2 > num)
		{
			lblState.ForeColor = Color.Red;
			lblState.Text = "恢复下限不能高于停止上限！";
			return;
		}
		try
		{
			EcAccess.Init();
			EcAccess.BurstOffMach();
			EcAccess.WriteEC_Mach(228, (byte)num2, wait);
			EcAccess.WriteEC_Mach(229, (byte)num, wait);
			EcAccess.WriteEC_Mach(228, (byte)num2, wait);
			EcAccess.WriteEC_Mach(229, (byte)num, wait);
			byte b = EcAccess.ReadEC_Mach(229, wait);
			if (b == (byte)num)
			{
				lblState.ForeColor = Color.FromArgb(0, 122, 204);
				lblState.Text = "已生效：充到 " + num + "% 停，低于 " + num2 + "% 恢复充电";
			}
			else
			{
				lblState.ForeColor = Color.Red;
				lblState.Text = "写入结果异常 (回读 " + b + "%)，请重试";
			}
			ConfigData configData = YamlConfigLoader.LoadConfig();
			configData.limit = num;
			configData.resume = num2;
			configData.startup = chkStartup.Checked;
			YamlConfigLoader.SaveConfig(configData);
			AutoStartHelper.SetAutoStart(chkStartup.Checked);
			RefreshEcStatus();
		}
		catch (Exception ex)
		{
			lblState.ForeColor = Color.Red;
			lblState.Text = "写入失败: " + ex.Message;
		}
	}

	private void OnFull(object sender, EventArgs e)
	{
		try
		{
			EcAccess.Init();
			EcAccess.BurstOffMach();
			EcAccess.WriteEC_Mach(228, 0, wait);
			EcAccess.WriteEC_Mach(229, 100, wait);
			EcAccess.WriteEC_Mach(228, 0, wait);
			EcAccess.WriteEC_Mach(229, 100, wait);
			lblState.ForeColor = Color.FromArgb(0, 122, 204);
			lblState.Text = "已恢复 100%（充到满为止）";
			RefreshEcStatus();
		}
		catch (Exception ex)
		{
			lblState.ForeColor = Color.Red;
			lblState.Text = "写入失败: " + ex.Message;
		}
	}
}
