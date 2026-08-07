using System;
using System.IO.Ports;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralProject.Transport.Channels
{
    /// <summary>
    /// 串口通信通道
    /// </summary>
    public class SerialChannel : ICommChannel
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private SerialPort? _serialPort;
        private bool _disposed;

        public SerialChannel(string portName, int baudRate)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
            _baudRate = baudRate;
        }

        public bool IsOpen => _serialPort?.IsOpen ?? false;
        public string ChannelName => $"{_portName}@{_baudRate}";

        public event Action<byte[], IPEndPoint?>? DataReceived;
        public event Action<Exception>? ErrorOccurred;
        public event EventHandler? Opened;
        public event EventHandler? Closed;

        public async Task OpenAsync(CancellationToken ct = default)
        {
            if (IsOpen) return;

            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();

                Opened?.Invoke(this, EventArgs.Empty);
            }, ct);
        }

        public async Task CloseAsync()
        {
            if (!IsOpen || _serialPort == null) return;

            await Task.Run(() =>
            {
                _serialPort.DataReceived -= OnDataReceived;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;

                Closed?.Invoke(this, EventArgs.Empty);
            });
        }

        public async Task WriteAsync(byte[] data, CancellationToken ct = default)
        {
            if (!IsOpen || _serialPort == null)
                throw new InvalidOperationException("串口未打开");
            if (data == null || data.Length == 0)
                throw new ArgumentException("数据不能为空", nameof(data));

            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                _serialPort.Write(data, 0, data.Length);
            }, ct);
        }

        /// <summary>
        /// 串口不支持指定目标发送
        /// </summary>
        public Task SendToAsync(byte[] data, IPEndPoint remoteEndPoint, CancellationToken ct = default)
        {
            throw new NotSupportedException("串口通道不支持 SendToAsync，请使用 WriteAsync。");
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var sp = (SerialPort)sender;
            try
            {
                int bytesToRead = sp.BytesToRead;
                if (bytesToRead == 0) return;

                byte[] buffer = new byte[bytesToRead];
                sp.Read(buffer, 0, bytesToRead);

                // 串口场景：IPEndPoint 传 null
                DataReceived?.Invoke(buffer, null);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseAsync().GetAwaiter().GetResult();
        }
    }
}