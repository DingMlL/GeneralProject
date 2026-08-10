using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GeneralProject.Transport.Devices.Renke;
using GeneralProject.Transport.Manager;

namespace GeneralProject.UIDemo
{
    public partial class Form1 : Form
    {
        private readonly DeviceManager _deviceManager;

        // 设备实例
        private TemperatureHumidityProxy? _tempProxy;
        private AirConditionerControllerProxy? _acProxy;
        private MicrowaveDetectorProxy? _microwaveProxy;

        // 连接状态
        private bool _isConnected = false;
        private string _connectionId = string.Empty;

        // 轮询控制
        private CancellationTokenSource? _pollingCts;
        private bool _isPolling = false;

        // 连接配置
        private string _connectionConfig = string.Empty;

        public Form1(DeviceManager deviceManager)
        {
            InitializeComponent();
            _deviceManager = deviceManager;
            this.FormClosing += Form1_FormClosing;
        }

        // ============ 连接/断开 ============

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            if (_isConnected)
            {
                // 断开连接
                StopPolling();
                _deviceManager.RemoveConnection(_connectionId);
                _isConnected = false;
                _connectionId = string.Empty;
                btnConnect.Text = "连接";
                btnConnect.BackColor = System.Drawing.Color.DeepSkyBlue;
                lblStatus.Text = "已断开";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                ClearDisplay();
                return;
            }

            string input = txtConnection.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("请输入连接参数", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 解析连接参数
                var (connectionConfig, connectionId) = ParseConnectionString(input);
                _connectionConfig = connectionConfig;
                _connectionId = connectionId;

                // 创建设备
                _tempProxy = await Task.Run(() =>
                    _deviceManager.GetOrCreateDevice<TemperatureHumidityProxy>(
                        _connectionId, "1", _connectionConfig
                    )
                );

                _acProxy = await Task.Run(() =>
                    _deviceManager.GetOrCreateDevice<AirConditionerControllerProxy>(
                        _connectionId, "2", _connectionConfig
                    )
                );

                _microwaveProxy = await Task.Run(() =>
                    _deviceManager.GetOrCreateDevice<MicrowaveDetectorProxy>(
                        _connectionId, "3", _connectionConfig
                    )
                );

                _isConnected = true;
                btnConnect.Text = "断开";
                btnConnect.BackColor = System.Drawing.Color.Red;
                lblStatus.Text = $"已连接 ({connectionId})";
                lblStatus.ForeColor = System.Drawing.Color.Green;

                // 连接成功后立即读取一次
                await ReadAllDevicesAsync();

                // 如果轮询已勾选，自动启动
                if (chkPolling.Checked)
                {
                    StartPolling();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = $"连接失败: {ex.Message}";
                lblStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        // ============ 解析连接字符串 ============

        private (string connectionConfig, string connectionId) ParseConnectionString(string input)
        {
            // 判断是 TCP 还是 串口
            if (input.Contains(":"))
            {
                // TCP: 192.168.1.113:8234
                var parts = input.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    return ($"tcp://{parts[0]}:{parts[1]}", $"TCP_{parts[0]}_{parts[1]}");
                }
                else if (input.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                {
                    // 串口带波特率: COM3:9600
                    var p = input.Split(':');
                    return ($"serial://{p[0]}:{p[1]}", p[0]);
                }
                throw new ArgumentException($"无法解析连接参数: {input}");
            }
            else if (input.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                // 串口: COM3（默认9600）
                return ($"serial://{input}:9600", input);
            }
            else
            {
                throw new ArgumentException($"无法解析连接参数: {input}");
            }
        }

        // ============ 读取所有设备 ============

        private async Task ReadAllDevicesAsync()
        {
            if (!_isConnected) return;

            try
            {
                // 读取送变器温湿度（地址1）
                if (_tempProxy != null)
                {
                    var temp = await _tempProxy.ReadTemperatureAsync();
                    var humidity = await _tempProxy.ReadHumidityAsync();
                    UpdateTempDisplay(temp, humidity);
                }

                // 读取控制器温湿度（地址2）
                if (_acProxy != null)
                {
                    var temp = await _acProxy.ReadTemperatureAsync();
                    var humidity = await _acProxy.ReadHumidityAsync();
                    UpdateAcDisplay(temp, humidity);
                }

                // 读取感应器状态（地址3）
                if (_microwaveProxy != null)
                {
                    var alarm = await _microwaveProxy.ReadAlarmStatusAsync();
                    var delay = await _microwaveProxy.ReadDelayAlarmTimeAsync();
                    var duration = await _microwaveProxy.ReadAlarmDurationAsync();
                    UpdateMicrowaveDisplay(alarm, delay, duration);
                }

                // 更新读取时间
                lblLastReadTime.Text = $"最后更新: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                // 读取失败不弹窗，只更新状态
                lblStatus.Text = $"读取异常: {ex.Message}";
                lblStatus.ForeColor = System.Drawing.Color.Orange;
            }
        }

        // ============ 更新显示 ============

        private void UpdateTempDisplay(float temp, float humidity)
        {
            lblTempValue.Text = $"{temp:F1} ℃";
            lblHumidityValue.Text = $"{humidity:F1} %";
            lblTempStatus.Text = "✅";
            lblTempStatus.ForeColor = System.Drawing.Color.Green;
        }

        private void UpdateAcDisplay(float temp, float humidity)
        {
            lblAcTempValue.Text = $"{temp:F1} ℃";
            lblAcHumidityValue.Text = $"{humidity:F1} %";
            lblAcStatus.Text = "✅";
            lblAcStatus.ForeColor = System.Drawing.Color.Green;
        }

        private void UpdateMicrowaveDisplay(bool alarm, int delay, int duration)
        {
            lblMicrowaveAlarm.Text = alarm ? "🚨 报警" : "✅ 正常";
            lblMicrowaveAlarm.ForeColor = alarm ? System.Drawing.Color.Red : System.Drawing.Color.Green;
            lblMicrowaveDelayValue.Text = $"{delay}s";
            lblMicrowaveDurationValue.Text = $"{duration}s";
        }

        private void ClearDisplay()
        {
            lblTempValue.Text = "--";
            lblHumidityValue.Text = "--";
            lblTempStatus.Text = "⏳";

            lblAcTempValue.Text = "--";
            lblAcHumidityValue.Text = "--";
            lblAcStatus.Text = "⏳";

            lblMicrowaveAlarm.Text = "⏳";
            lblMicrowaveDelayValue.Text = "--";
            lblMicrowaveDurationValue.Text = "--";

            lblLastReadTime.Text = "未读取";
        }

        // ============ 轮询控制 ============

        private void chkPolling_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPolling.Checked)
            {
                if (_isConnected)
                {
                    StartPolling();
                }
                else
                {
                    // 未连接时勾选，提示用户先连接
                    chkPolling.Checked = false;
                    MessageBox.Show("请先点击「连接」按钮建立连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                StopPolling();
            }
        }

        private void StartPolling()
        {
            if (_isPolling) return;
            if (!_isConnected) return;

            _isPolling = true;
            _pollingCts = new CancellationTokenSource();
            _ = PollingLoop(_pollingCts.Token);
        }

        private void StopPolling()
        {
            if (!_isPolling) return;

            _isPolling = false;
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;
        }

        private async Task PollingLoop(CancellationToken ct)
        {
            // 读取间隔：1秒
            int interval = (int)nudPollingInterval.Value * 1000;

            while (!ct.IsCancellationRequested && _isConnected && _isPolling)
            {
                try
                {
                    await ReadAllDevicesAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // 轮询中的异常静默处理，避免循环中断
                }

                // 等待间隔（支持提前取消）
                try
                {
                    await Task.Delay(interval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _isPolling = false;
        }

        // ============ 手动刷新 ============

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                MessageBox.Show("请先连接设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnRefresh.Enabled = false;
            try
            {
                await ReadAllDevicesAsync();
                lblStatus.Text = "刷新成功";
                lblStatus.ForeColor = System.Drawing.Color.Green;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"刷新失败: {ex.Message}";
                lblStatus.ForeColor = System.Drawing.Color.Red;
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        // ============ 窗体关闭 ============

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopPolling();
            _deviceManager.Dispose();
        }
    }
}