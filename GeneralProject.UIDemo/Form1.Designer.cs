using System.Windows.Forms;

namespace GeneralProject.UIDemo
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // ============ 顶部：连接区 ============
            this.lblConnection = new Label();
            this.txtConnection = new TextBox();
            this.btnConnect = new Button();
            this.lblStatus = new Label();

            // ============ 中间：数据显示区 ============
            this.grpTemp = new GroupBox();
            this.lblTempLabel = new Label();
            this.lblHumidityLabel = new Label();
            this.lblTempValue = new Label();
            this.lblHumidityValue = new Label();
            this.lblTempStatus = new Label();

            this.grpAc = new GroupBox();
            this.lblAcTempLabel = new Label();
            this.lblAcHumidityLabel = new Label();
            this.lblAcTempValue = new Label();
            this.lblAcHumidityValue = new Label();
            this.lblAcStatus = new Label();

            this.grpMicrowave = new GroupBox();
            this.lblMicrowaveAlarmLabel = new Label();
            this.lblMicrowaveDelayLabel = new Label();
            this.lblMicrowaveDurationLabel = new Label();
            this.lblMicrowaveAlarm = new Label();
            this.lblMicrowaveDelayValue = new Label();
            this.lblMicrowaveDurationValue = new Label();

            // ============ 底部：控制区 ============
            this.chkPolling = new CheckBox();
            this.nudPollingInterval = new NumericUpDown();
            this.lblInterval = new Label();
            this.btnRefresh = new Button();
            this.lblLastReadTime = new Label();

            // ============ Form ============
            this.SuspendLayout();
            this.Text = "设备通信测试工具";
            this.Size = new System.Drawing.Size(600, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // ===== 连接区 =====
            lblConnection.Text = "连接:";
            lblConnection.Location = new System.Drawing.Point(12, 15);
            lblConnection.Size = new System.Drawing.Size(40, 25);

            txtConnection.Location = new System.Drawing.Point(58, 12);
            txtConnection.Size = new System.Drawing.Size(280, 25);
            txtConnection.Text = "COM3";
            txtConnection.Font = new Font("Consolas", 10);

            btnConnect.Text = "连接";
            btnConnect.Location = new System.Drawing.Point(344, 12);
            btnConnect.Size = new System.Drawing.Size(80, 30);
            btnConnect.Click += btnConnect_Click;

            lblStatus.Text = "未连接";
            lblStatus.Location = new System.Drawing.Point(430, 15);
            lblStatus.Size = new System.Drawing.Size(150, 25);
            lblStatus.ForeColor = System.Drawing.Color.Red;

            // ===== 设备1：送变器 =====
            grpTemp.Text = "🌡️ 送变器温湿度 (地址: 1)";
            grpTemp.Location = new System.Drawing.Point(12, 55);
            grpTemp.Size = new System.Drawing.Size(560, 100);

            lblTempLabel.Text = "温度:";
            lblTempLabel.Location = new System.Drawing.Point(20, 25);
            lblTempLabel.Size = new System.Drawing.Size(50, 25);

            lblTempValue.Text = "--℃";
            lblTempValue.Location = new System.Drawing.Point(75, 25);
            lblTempValue.Size = new System.Drawing.Size(100, 25);
            lblTempValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblHumidityLabel.Text = "湿度:";
            lblHumidityLabel.Location = new System.Drawing.Point(200, 25);
            lblHumidityLabel.Size = new System.Drawing.Size(50, 25);

            lblHumidityValue.Text = "--%";
            lblHumidityValue.Location = new System.Drawing.Point(255, 25);
            lblHumidityValue.Size = new System.Drawing.Size(100, 25);
            lblHumidityValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblTempStatus.Text = "⏳";
            lblTempStatus.Location = new System.Drawing.Point(380, 25);
            lblTempStatus.Size = new System.Drawing.Size(150, 25);
            lblTempStatus.Font = new Font("Consolas", 14);

            grpTemp.Controls.AddRange(new Control[] { lblTempLabel, lblTempValue, lblHumidityLabel, lblHumidityValue, lblTempStatus });

            // ===== 设备2：控制器 =====
            grpAc.Text = "❄️ 控制器温湿度 (地址: 2)";
            grpAc.Location = new System.Drawing.Point(12, 170);
            grpAc.Size = new System.Drawing.Size(560, 100);

            lblAcTempLabel.Text = "温度:";
            lblAcTempLabel.Location = new System.Drawing.Point(20, 25);
            lblAcTempLabel.Size = new System.Drawing.Size(50, 25);

            lblAcTempValue.Text = "--℃";
            lblAcTempValue.Location = new System.Drawing.Point(75, 25);
            lblAcTempValue.Size = new System.Drawing.Size(100, 25);
            lblAcTempValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblAcHumidityLabel.Text = "湿度:";
            lblAcHumidityLabel.Location = new System.Drawing.Point(200, 25);
            lblAcHumidityLabel.Size = new System.Drawing.Size(50, 25);

            lblAcHumidityValue.Text = "--%";
            lblAcHumidityValue.Location = new System.Drawing.Point(255, 25);
            lblAcHumidityValue.Size = new System.Drawing.Size(100, 25);
            lblAcHumidityValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblAcStatus.Text = "⏳";
            lblAcStatus.Location = new System.Drawing.Point(380, 25);
            lblAcStatus.Size = new System.Drawing.Size(150, 25);
            lblAcStatus.Font = new Font("Consolas", 14);

            grpAc.Controls.AddRange(new Control[] { lblAcTempLabel, lblAcTempValue, lblAcHumidityLabel, lblAcHumidityValue, lblAcStatus });

            // ===== 设备3：感应器 =====
            grpMicrowave.Text = "📡 感应器状态 (地址: 3)";
            grpMicrowave.Location = new System.Drawing.Point(12, 285);
            grpMicrowave.Size = new System.Drawing.Size(560, 100);

            lblMicrowaveAlarmLabel.Text = "报警状态:";
            lblMicrowaveAlarmLabel.Location = new System.Drawing.Point(20, 25);
            lblMicrowaveAlarmLabel.Size = new System.Drawing.Size(80, 25);

            lblMicrowaveAlarm.Text = "⏳";
            lblMicrowaveAlarm.Location = new System.Drawing.Point(105, 25);
            lblMicrowaveAlarm.Size = new System.Drawing.Size(120, 25);
            lblMicrowaveAlarm.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblMicrowaveDelayLabel.Text = "延时:";
            lblMicrowaveDelayLabel.Location = new System.Drawing.Point(250, 25);
            lblMicrowaveDelayLabel.Size = new System.Drawing.Size(50, 25);

            lblMicrowaveDelayValue.Text = "--s";
            lblMicrowaveDelayValue.Location = new System.Drawing.Point(305, 25);
            lblMicrowaveDelayValue.Size = new System.Drawing.Size(80, 25);
            lblMicrowaveDelayValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblMicrowaveDurationLabel.Text = "持续时间:";
            lblMicrowaveDurationLabel.Location = new System.Drawing.Point(400, 25);
            lblMicrowaveDurationLabel.Size = new System.Drawing.Size(70, 25);

            lblMicrowaveDurationValue.Text = "--s";
            lblMicrowaveDurationValue.Location = new System.Drawing.Point(475, 25);
            lblMicrowaveDurationValue.Size = new System.Drawing.Size(70, 25);
            lblMicrowaveDurationValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            grpMicrowave.Controls.AddRange(new Control[] {
                lblMicrowaveAlarmLabel, lblMicrowaveAlarm,
                lblMicrowaveDelayLabel, lblMicrowaveDelayValue,
                lblMicrowaveDurationLabel, lblMicrowaveDurationValue
            });

            // ===== 底部：控制区 =====
            chkPolling.Text = "轮询读取";
            chkPolling.Location = new System.Drawing.Point(12, 405);
            chkPolling.Size = new System.Drawing.Size(100, 25);
            chkPolling.CheckedChanged += chkPolling_CheckedChanged;

            lblInterval.Text = "间隔:";
            lblInterval.Location = new System.Drawing.Point(118, 405);
            lblInterval.Size = new System.Drawing.Size(40, 25);

            nudPollingInterval.Location = new System.Drawing.Point(158, 403);
            nudPollingInterval.Size = new System.Drawing.Size(60, 25);
            nudPollingInterval.Minimum = 1;
            nudPollingInterval.Maximum = 60;
            nudPollingInterval.Value = 1;

            lblInterval2 = new Label();
            lblInterval2.Text = "秒";
            lblInterval2.Location = new System.Drawing.Point(224, 405);
            lblInterval2.Size = new System.Drawing.Size(30, 25);

            btnRefresh.Text = "🔄 手动刷新";
            btnRefresh.Location = new System.Drawing.Point(280, 400);
            btnRefresh.Size = new System.Drawing.Size(110, 30);
            btnRefresh.Click += btnRefresh_Click;

            lblLastReadTime.Text = "未读取";
            lblLastReadTime.Location = new System.Drawing.Point(400, 405);
            lblLastReadTime.Size = new System.Drawing.Size(160, 25);
            lblLastReadTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            // ===== 添加到窗体 =====
            this.Controls.AddRange(new Control[] {
                lblConnection, txtConnection, btnConnect, lblStatus,
                grpTemp, grpAc, grpMicrowave,
                chkPolling, lblInterval, nudPollingInterval, lblInterval2, btnRefresh, lblLastReadTime
            });

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ============ 控件声明 ============

        // 连接区
        private Label lblConnection;
        private TextBox txtConnection;
        private Button btnConnect;
        private Label lblStatus;

        // 设备1：送变器
        private GroupBox grpTemp;
        private Label lblTempLabel, lblHumidityLabel;
        private Label lblTempValue, lblHumidityValue;
        private Label lblTempStatus;

        // 设备2：控制器
        private GroupBox grpAc;
        private Label lblAcTempLabel, lblAcHumidityLabel;
        private Label lblAcTempValue, lblAcHumidityValue;
        private Label lblAcStatus;

        // 设备3：感应器
        private GroupBox grpMicrowave;
        private Label lblMicrowaveAlarmLabel, lblMicrowaveDelayLabel, lblMicrowaveDurationLabel;
        private Label lblMicrowaveAlarm, lblMicrowaveDelayValue, lblMicrowaveDurationValue;

        // 控制区
        private CheckBox chkPolling;
        private NumericUpDown nudPollingInterval;
        private Label lblInterval;
        private Label lblInterval2;
        private Button btnRefresh;
        private Label lblLastReadTime;
    }
}