using System;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace MESPushTool
{
    public class SerialPortManager : IDisposable
    {
        private SerialPort _serialPort;
        private StringBuilder _receiveBuffer = new StringBuilder();
        private DateTime _lastDataTime = DateTime.MinValue;
        private Thread _flushThread;
        private bool _isRunning;
        public event EventHandler<DataEventArgs> DataReceived;
        public event EventHandler<DataEventArgs> ErrorOccurred;

        // 串口接收空闲超过多少毫秒无新数据则视为一帧结束
        private const int IdleFlushMs = 200;

        public bool IsOpen { get { return _serialPort != null && _serialPort.IsOpen; } }

        public bool Open(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits)
        {
            try
            {
                Close();
                _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();
                _isRunning = true;
                _flushThread = new Thread(FlushLoop);
                _flushThread.IsBackground = true;
                _flushThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null) ErrorOccurred(this, new DataEventArgs("串口打开失败: " + ex.Message));
                return false;
            }
        }

        public void Close()
        {
            _isRunning = false;
            if (_serialPort != null)
            {
                try
                {
                    _serialPort.DataReceived -= OnDataReceived;
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    _serialPort.Dispose();
                }
                catch { }
                _serialPort = null;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    lock (_receiveBuffer)
                    {
                        _receiveBuffer.Append(data);
                        _lastDataTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null) ErrorOccurred(this, new DataEventArgs("串口读取错误: " + ex.Message));
            }
        }

        private void FlushLoop()
        {
            while (_isRunning)
            {
                Thread.Sleep(IdleFlushMs);
                string frame = null;
                lock (_receiveBuffer)
                {
                    if (_receiveBuffer.Length > 0 && _lastDataTime != DateTime.MinValue
                        && (DateTime.Now - _lastDataTime).TotalMilliseconds >= IdleFlushMs)
                    {
                        frame = _receiveBuffer.ToString();
                        _receiveBuffer.Length = 0;
                        _lastDataTime = DateTime.MinValue;
                    }
                }
                if (!string.IsNullOrEmpty(frame))
                {
                    string[] lines = frame.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string s = line.Trim();
                        if (s.Length > 0 && DataReceived != null)
                            DataReceived(this, new DataEventArgs(s));
                    }
                }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
