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
                // ===== 1. 停止轮询并等待完成 =====
                if (_isPolling)
                {
                    StopPolling();
                    // 等待轮询循环完全退出（最多等待 2 秒）
                    int waitCount = 0;
                    while (_isPolling && waitCount < 20)
                    {
                        await Task.Delay(100);
                        waitCount++;
                    }
                }

                // ===== 2. 移除连接 =====
                _deviceManager.RemoveConnection(_connectionId);
                _isConnected = false;
                _connectionId = string.Empty;

                // ===== 3. 释放设备代理引用 =====
                _tempProxy = null;
                _acProxy = null;
                _microwaveProxy = null;

                // ===== 4. 重置 UI =====
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
                var (connectionConfig, connectionId) = ParseConnectionString(input);
                _connectionConfig = connectionConfig;
                _connectionId = connectionId;

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

                await ReadAllDevicesAsync();

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
            if (input.Contains(":"))
            {
                var parts = input.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int port))
                {
                    return ($"tcp://{parts[0]}:{parts[1]}", $"TCP_{parts[0]}_{parts[1]}");
                }
                else if (input.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                {
                    var p = input.Split(':');
                    return ($"serial://{p[0]}:{p[1]}", p[0]);
                }
                throw new ArgumentException($"无法解析连接参数: {input}");
            }
            else if (input.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
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
            // 如果连接已断开或设备代理为空，直接返回
            if (!_isConnected || _tempProxy == null || _acProxy == null || _microwaveProxy == null)
                return;

            try
            {
                // 读取温湿度变送器
                if (_tempProxy != null)
                {
                    var temp = await _tempProxy.ReadTemperatureAsync();
                    var humidity = await _tempProxy.ReadHumidityAsync();
                    UpdateTempDisplay(temp, humidity);
                }

                // 读取空调温湿度（只读温湿度，不读状态）
                if (_acProxy != null)
                {
                    var temp = await _acProxy.ReadTemperatureAsync();
                    var humidity = await _acProxy.ReadHumidityAsync();
                    UpdateAcDisplay(temp, humidity);
                }

                // 读取微波探测器
                if (_microwaveProxy != null)
                {
                    var alarm = await _microwaveProxy.ReadAlarmStatusAsync();
                    var delay = await _microwaveProxy.ReadDelayAlarmTimeAsync();
                    var duration = await _microwaveProxy.ReadAlarmDurationAsync();
                    UpdateMicrowaveDisplay(alarm, delay, duration);
                }

                lblLastReadTime.Text = $"最后更新: {DateTime.Now:HH:mm:ss}";
            }
            catch (ObjectDisposedException)
            {
                // 对象已释放，忽略，等待下次连接重建
                lblStatus.Text = "连接已断开";
                lblStatus.ForeColor = System.Drawing.Color.Orange;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"读取异常: {ex.Message}";
                lblStatus.ForeColor = System.Drawing.Color.Red;
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
            // 状态默认显示 ✅，表示温湿度读取成功
            lblAcStatus.Text = "✅";
            lblAcStatus.ForeColor = System.Drawing.Color.Green;
        }

        private async Task UpdateAcStatus()
        {
            try
            {
                var status = await _acProxy!.ReadStatusChannel1Async();
                lblAcStatus.Text = status switch
                {
                    AirConditionerStatus.Stopped => "⏹️ 已停止",
                    AirConditionerStatus.Cooling => "❄️ 制冷中",
                    AirConditionerStatus.Heating => "🔥 制热中",
                    _ => "❓ 未知"
                };
                lblAcStatus.ForeColor = System.Drawing.Color.Blue;
            }
            catch
            {
                lblAcStatus.Text = "❌ 读取失败";
                lblAcStatus.ForeColor = System.Drawing.Color.Red;
            }
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
            SetAcFeedback("就绪", System.Drawing.Color.Gray);

            lblMicrowaveAlarm.Text = "--";
            lblMicrowaveDelayValue.Text = "--";
            lblMicrowaveDurationValue.Text = "--";

            lblLastReadTime.Text = "未读取";
        }

        // ============ 空调发射方法 ============

        private async void btnAcSendCooling_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }
            SetAcFeedback("正在发送制冷指令...", System.Drawing.Color.Blue);

            try
            {
                await _acProxy.SendCoolingOnAsync();
                SetAcFeedback("✅ 制冷指令发送成功", System.Drawing.Color.Green);
                await UpdateAcStatus();
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 发送失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private async void btnAcSendHeating_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }
            SetAcFeedback("正在发送制热指令...", System.Drawing.Color.Blue);

            try
            {
                await _acProxy.SendHeatingOnAsync();
                SetAcFeedback("✅ 制热指令发送成功", System.Drawing.Color.Green);
                await UpdateAcStatus();
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 发送失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private async void btnAcSendOff_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }
            SetAcFeedback("正在发送关机指令...", System.Drawing.Color.Blue);

            try
            {
                await _acProxy.SendOffAsync();
                SetAcFeedback("✅ 关机指令发送成功", System.Drawing.Color.Green);
                await UpdateAcStatus();
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 发送失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private async void btnAcSendCustom_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }

            int index = (int)nudAcSendCustomIndex.Value;
            SetAcFeedback($"正在发送自定义 {index} 指令...", System.Drawing.Color.Blue);

            try
            {
                if (index <= 20)
                {
                    await _acProxy.SendCustomAsync(index);
                }
                else
                {
                    await _acProxy.SendCustomExtendedAsync(index);
                }
                SetAcFeedback($"✅ 自定义 {index} 指令发送成功", System.Drawing.Color.Green);
                await UpdateAcStatus();
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 发送失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        // ============ 空调学习方法 ============

        private async void btnAcLearnCooling_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }

            SetAcFeedback("📖 正在学习制冷指令（请对准遥控器发射）...", System.Drawing.Color.Blue);

            try
            {
                await _acProxy.LearnCoolingAsync(5000);
                SetAcFeedback("✅ 制冷指令学习成功", System.Drawing.Color.Green);
            }
            catch (TimeoutException)
            {
                SetAcFeedback("❌ 学习超时，请检查遥控器是否对准", System.Drawing.Color.Red);
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 学习失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private async void btnAcLearnHeating_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }

            SetAcFeedback("📖 正在学习制热指令（请对准遥控器发射）...", System.Drawing.Color.Blue);

            try
            {
                await _acProxy.LearnHeatingAsync(5000);
                SetAcFeedback("✅ 制热指令学习成功", System.Drawing.Color.Green);
            }
            catch (TimeoutException)
            {
                SetAcFeedback("❌ 学习超时，请检查遥控器是否对准", System.Drawing.Color.Red);
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 学习失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private async void btnAcLearnOff_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }

            SetAcFeedback("📖 正在学习关机指令（请对准遥控器发射）...", System.Drawing.Color.Blue);

            try
            {
                await _acProxy.LearnOffAsync(5000);
                SetAcFeedback("✅ 关机指令学习成功", System.Drawing.Color.Green);
            }
            catch (TimeoutException)
            {
                SetAcFeedback("❌ 学习超时，请检查遥控器是否对准", System.Drawing.Color.Red);
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 学习失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private async void btnAcLearnCustom_Click(object sender, EventArgs e)
        {
            if (_acProxy == null) { MessageBox.Show("设备未初始化"); return; }

            int index = (int)nudAcLearnCustomIndex.Value;
            SetAcFeedback($"📖 正在学习自定义 {index} 指令（请对准遥控器发射）...", System.Drawing.Color.Blue);

            try
            {
                if (index <= 20)
                {
                    await _acProxy.LearnCustomAsync(index, 5000);
                }
                else
                {
                    await _acProxy.LearnCustomExtendedAsync(index, 5000);
                }
                SetAcFeedback($"✅ 自定义 {index} 指令学习成功", System.Drawing.Color.Green);
            }
            catch (TimeoutException)
            {
                SetAcFeedback($"❌ 学习超时，请检查遥控器是否对准", System.Drawing.Color.Red);
            }
            catch (NotSupportedException)
            {
                SetAcFeedback($"❌ 设备不支持自定义 {index}（请检查固件版本）", System.Drawing.Color.Red);
            }
            catch (Exception ex)
            {
                SetAcFeedback($"❌ 学习失败: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        // ============ 空调辅助方法 ============

        private void SetAcFeedback(string message, System.Drawing.Color color)
        {
            if (lblAcFeedback.InvokeRequired)
            {
                lblAcFeedback.Invoke(new Action(() => SetAcFeedback(message, color)));
                return;
            }
            lblAcFeedback.Text = message;
            lblAcFeedback.ForeColor = color;
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
                catch
                {
                    // 静默处理
                }

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