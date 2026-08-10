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
            // 设备1：温湿度变送器
            this.grpTemp = new GroupBox();
            this.lblTempLabel = new Label();
            this.lblHumidityLabel = new Label();
            this.lblTempValue = new Label();
            this.lblHumidityValue = new Label();
            this.lblTempStatus = new Label();

            // 设备2：空调控制器（含扩展功能）
            this.grpAc = new GroupBox();
            this.lblAcTempLabel = new Label();
            this.lblAcHumidityLabel = new Label();
            this.lblAcTempValue = new Label();
            this.lblAcHumidityValue = new Label();
            this.lblAcStatus = new Label();

            // 空调控制区域 - 发射
            this.grpAcSend = new GroupBox();
            this.btnAcSendCooling = new Button();
            this.btnAcSendHeating = new Button();
            this.btnAcSendOff = new Button();
            this.lblAcSendCustom = new Label();
            this.nudAcSendCustomIndex = new NumericUpDown();
            this.btnAcSendCustom = new Button();

            // 空调控制区域 - 学习
            this.grpAcLearn = new GroupBox();
            this.btnAcLearnCooling = new Button();
            this.btnAcLearnHeating = new Button();
            this.btnAcLearnOff = new Button();
            this.lblAcLearnCustom = new Label();
            this.nudAcLearnCustomIndex = new NumericUpDown();
            this.btnAcLearnCustom = new Button();

            // 空调状态反馈
            this.lblAcFeedback = new Label();

            // 设备3：微波探测器
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

            // ============ 控件初始化 ============
            this.SuspendLayout();

            // ---------- Form ----------
            this.Text = "设备通信测试工具";
            this.Size = new System.Drawing.Size(620, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // ---------- 连接区 ----------
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
            btnConnect.BackColor = System.Drawing.Color.DeepSkyBlue;
            btnConnect.Click += btnConnect_Click;

            lblStatus.Text = "未连接";
            lblStatus.Location = new System.Drawing.Point(430, 15);
            lblStatus.Size = new System.Drawing.Size(160, 25);
            lblStatus.ForeColor = System.Drawing.Color.Red;

            // ---------- 设备1：温湿度变送器 ----------
            grpTemp.Text = "🌡️ 温湿度变送器 (地址: 1)";
            grpTemp.Location = new System.Drawing.Point(12, 55);
            grpTemp.Size = new System.Drawing.Size(580, 85);

            lblTempLabel.Text = "温度:";
            lblTempLabel.Location = new System.Drawing.Point(20, 25);
            lblTempLabel.Size = new System.Drawing.Size(50, 25);

            lblTempValue.Text = "--℃";
            lblTempValue.Location = new System.Drawing.Point(75, 25);
            lblTempValue.Size = new System.Drawing.Size(120, 25);
            lblTempValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblHumidityLabel.Text = "湿度:";
            lblHumidityLabel.Location = new System.Drawing.Point(210, 25);
            lblHumidityLabel.Size = new System.Drawing.Size(50, 25);

            lblHumidityValue.Text = "--%";
            lblHumidityValue.Location = new System.Drawing.Point(265, 25);
            lblHumidityValue.Size = new System.Drawing.Size(120, 25);
            lblHumidityValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblTempStatus.Text = "⏳";
            lblTempStatus.Location = new System.Drawing.Point(400, 25);
            lblTempStatus.Size = new System.Drawing.Size(150, 25);
            lblTempStatus.Font = new Font("Consolas", 14);

            grpTemp.Controls.AddRange(new Control[] {
                lblTempLabel, lblTempValue,
                lblHumidityLabel, lblHumidityValue,
                lblTempStatus
            });

            // ---------- 设备2：空调控制器 ----------
            grpAc.Text = "❄️ 空调控制器 (地址: 2)";
            grpAc.Location = new System.Drawing.Point(12, 150);
            grpAc.Size = new System.Drawing.Size(580, 315);

            // 温湿度显示
            lblAcTempLabel.Text = "温度:";
            lblAcTempLabel.Location = new System.Drawing.Point(20, 20);
            lblAcTempLabel.Size = new System.Drawing.Size(50, 25);

            lblAcTempValue.Text = "--℃";
            lblAcTempValue.Location = new System.Drawing.Point(75, 20);
            lblAcTempValue.Size = new System.Drawing.Size(120, 25);
            lblAcTempValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblAcHumidityLabel.Text = "湿度:";
            lblAcHumidityLabel.Location = new System.Drawing.Point(210, 20);
            lblAcHumidityLabel.Size = new System.Drawing.Size(50, 25);

            lblAcHumidityValue.Text = "--%";
            lblAcHumidityValue.Location = new System.Drawing.Point(265, 20);
            lblAcHumidityValue.Size = new System.Drawing.Size(120, 25);
            lblAcHumidityValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            lblAcStatus.Text = "⏳";
            lblAcStatus.Location = new System.Drawing.Point(400, 20);
            lblAcStatus.Size = new System.Drawing.Size(150, 25);
            lblAcStatus.Font = new Font("Consolas", 12);

            // ----- 发射区域 -----
            grpAcSend.Text = "📡 发射指令";
            grpAcSend.Location = new System.Drawing.Point(12, 55);
            grpAcSend.Size = new System.Drawing.Size(556, 55);

            btnAcSendCooling.Text = "❄️ 制冷";
            btnAcSendCooling.Location = new System.Drawing.Point(10, 18);
            btnAcSendCooling.Size = new System.Drawing.Size(75, 28);
            btnAcSendCooling.Click += btnAcSendCooling_Click;

            btnAcSendHeating.Text = "🔥 制热";
            btnAcSendHeating.Location = new System.Drawing.Point(95, 18);
            btnAcSendHeating.Size = new System.Drawing.Size(75, 28);
            btnAcSendHeating.Click += btnAcSendHeating_Click;

            btnAcSendOff.Text = "⏹️ 关机";
            btnAcSendOff.Location = new System.Drawing.Point(180, 18);
            btnAcSendOff.Size = new System.Drawing.Size(75, 28);
            btnAcSendOff.Click += btnAcSendOff_Click;

            lblAcSendCustom.Text = "自定义:";
            lblAcSendCustom.Location = new System.Drawing.Point(270, 20);
            lblAcSendCustom.Size = new System.Drawing.Size(45, 25);

            nudAcSendCustomIndex.Location = new System.Drawing.Point(318, 20);
            nudAcSendCustomIndex.Size = new System.Drawing.Size(50, 23);
            nudAcSendCustomIndex.Minimum = 1;
            nudAcSendCustomIndex.Maximum = 29;
            nudAcSendCustomIndex.Value = 1;

            btnAcSendCustom.Text = "📡 发射";
            btnAcSendCustom.Location = new System.Drawing.Point(375, 18);
            btnAcSendCustom.Size = new System.Drawing.Size(80, 28);
            btnAcSendCustom.Click += btnAcSendCustom_Click;

            grpAcSend.Controls.AddRange(new Control[] {
                btnAcSendCooling, btnAcSendHeating, btnAcSendOff,
                lblAcSendCustom, nudAcSendCustomIndex, btnAcSendCustom
            });

            // ----- 学习区域 -----
            grpAcLearn.Text = "📖 学习指令 (超时 5 秒)";
            grpAcLearn.Location = new System.Drawing.Point(12, 115);
            grpAcLearn.Size = new System.Drawing.Size(556, 55);

            btnAcLearnCooling.Text = "📖 学习制冷";
            btnAcLearnCooling.Location = new System.Drawing.Point(10, 18);
            btnAcLearnCooling.Size = new System.Drawing.Size(85, 28);
            btnAcLearnCooling.Click += btnAcLearnCooling_Click;

            btnAcLearnHeating.Text = "📖 学习制热";
            btnAcLearnHeating.Location = new System.Drawing.Point(105, 18);
            btnAcLearnHeating.Size = new System.Drawing.Size(85, 28);
            btnAcLearnHeating.Click += btnAcLearnHeating_Click;

            btnAcLearnOff.Text = "📖 学习关机";
            btnAcLearnOff.Location = new System.Drawing.Point(200, 18);
            btnAcLearnOff.Size = new System.Drawing.Size(85, 28);
            btnAcLearnOff.Click += btnAcLearnOff_Click;

            lblAcLearnCustom.Text = "自定义:";
            lblAcLearnCustom.Location = new System.Drawing.Point(295, 20);
            lblAcLearnCustom.Size = new System.Drawing.Size(45, 25);

            nudAcLearnCustomIndex.Location = new System.Drawing.Point(342, 20);
            nudAcLearnCustomIndex.Size = new System.Drawing.Size(50, 23);
            nudAcLearnCustomIndex.Minimum = 1;
            nudAcLearnCustomIndex.Maximum = 29;
            nudAcLearnCustomIndex.Value = 1;

            btnAcLearnCustom.Text = "📖 学习";
            btnAcLearnCustom.Location = new System.Drawing.Point(398, 18);
            btnAcLearnCustom.Size = new System.Drawing.Size(80, 28);
            btnAcLearnCustom.Click += btnAcLearnCustom_Click;

            grpAcLearn.Controls.AddRange(new Control[] {
                btnAcLearnCooling, btnAcLearnHeating, btnAcLearnOff,
                lblAcLearnCustom, nudAcLearnCustomIndex, btnAcLearnCustom
            });

            // ----- 状态反馈 -----
            lblAcFeedback.Text = "就绪";
            lblAcFeedback.Location = new System.Drawing.Point(15, 180);
            lblAcFeedback.Size = new System.Drawing.Size(550, 20);
            lblAcFeedback.ForeColor = System.Drawing.Color.Gray;
            lblAcFeedback.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // 组装空调控制器
            grpAc.Controls.AddRange(new Control[] {
                lblAcTempLabel, lblAcTempValue,
                lblAcHumidityLabel, lblAcHumidityValue,
                lblAcStatus,
                grpAcSend, grpAcLearn, lblAcFeedback
            });

            // ---------- 设备3：微波探测器 ----------
            grpMicrowave.Text = "📡 微波探测器 (地址: 3)";
            grpMicrowave.Location = new System.Drawing.Point(12, 475);
            grpMicrowave.Size = new System.Drawing.Size(580, 85);

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
            lblMicrowaveDurationValue.Size = new System.Drawing.Size(80, 25);
            lblMicrowaveDurationValue.Font = new Font("Consolas", 12, FontStyle.Bold);

            grpMicrowave.Controls.AddRange(new Control[] {
                lblMicrowaveAlarmLabel, lblMicrowaveAlarm,
                lblMicrowaveDelayLabel, lblMicrowaveDelayValue,
                lblMicrowaveDurationLabel, lblMicrowaveDurationValue
            });

            // ---------- 底部控制区 ----------
            chkPolling.Text = "轮询读取";
            chkPolling.Location = new System.Drawing.Point(12, 575);
            chkPolling.Size = new System.Drawing.Size(90, 25);
            chkPolling.CheckedChanged += chkPolling_CheckedChanged;

            lblInterval.Text = "间隔:";
            lblInterval.Location = new System.Drawing.Point(108, 575);
            lblInterval.Size = new System.Drawing.Size(35, 25);

            nudPollingInterval.Location = new System.Drawing.Point(145, 573);
            nudPollingInterval.Size = new System.Drawing.Size(55, 23);
            nudPollingInterval.Minimum = 1;
            nudPollingInterval.Maximum = 60;
            nudPollingInterval.Value = 1;

            lblInterval2 = new Label();
            lblInterval2.Text = "秒";
            lblInterval2.Location = new System.Drawing.Point(205, 575);
            lblInterval2.Size = new System.Drawing.Size(30, 25);

            btnRefresh.Text = "🔄 手动刷新";
            btnRefresh.Location = new System.Drawing.Point(260, 570);
            btnRefresh.Size = new System.Drawing.Size(110, 30);
            btnRefresh.Click += btnRefresh_Click;

            lblLastReadTime.Text = "未读取";
            lblLastReadTime.Location = new System.Drawing.Point(390, 575);
            lblLastReadTime.Size = new System.Drawing.Size(190, 25);
            lblLastReadTime.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

            // ---------- 添加到窗体 ----------
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

        // 设备1：温湿度变送器
        private GroupBox grpTemp;
        private Label lblTempLabel, lblHumidityLabel;
        private Label lblTempValue, lblHumidityValue;
        private Label lblTempStatus;

        // 设备2：空调控制器
        private GroupBox grpAc;
        private Label lblAcTempLabel, lblAcHumidityLabel;
        private Label lblAcTempValue, lblAcHumidityValue;
        private Label lblAcStatus;

        // 空调 - 发射
        private GroupBox grpAcSend;
        private Button btnAcSendCooling;
        private Button btnAcSendHeating;
        private Button btnAcSendOff;
        private Label lblAcSendCustom;
        private NumericUpDown nudAcSendCustomIndex;
        private Button btnAcSendCustom;

        // 空调 - 学习
        private GroupBox grpAcLearn;
        private Button btnAcLearnCooling;
        private Button btnAcLearnHeating;
        private Button btnAcLearnOff;
        private Label lblAcLearnCustom;
        private NumericUpDown nudAcLearnCustomIndex;
        private Button btnAcLearnCustom;

        // 空调 - 反馈
        private Label lblAcFeedback;

        // 设备3：微波探测器
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