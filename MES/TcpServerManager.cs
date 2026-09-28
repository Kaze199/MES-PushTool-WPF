using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MESPushTool
{
    public class TcpServerManager : IDisposable
    {
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _isRunning;
        private List<TcpClient> _clients = new List<TcpClient>();
        public event EventHandler<DataEventArgs> DataReceived;
        public event EventHandler<DataEventArgs> ErrorOccurred;

        // 收到数据后等待多少毫秒无新数据则视为一帧结束
        private const int IdleFlushMs = 200;

        public bool IsRunning { get { return _isRunning; } }

        public bool Start(int port)
        {
            try
            {
                Stop();
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _isRunning = true;
                _listenThread = new Thread(ListenLoop);
                _listenThread.IsBackground = true;
                _listenThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                if (ErrorOccurred != null) ErrorOccurred(this, new DataEventArgs("TCP服务启动失败: " + ex.Message));
                return false;
            }
        }

        private void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    lock (_clients) { _clients.Add(client); }
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.IsBackground = true;
                    clientThread.Start(client);
                }
                catch
                {
                    if (_isRunning && ErrorOccurred != null)
                        ErrorOccurred(this, new DataEventArgs("TCP接收客户端连接失败"));
                }
            }
        }

        private void HandleClient(object obj)
        {
            TcpClient client = (TcpClient)obj;
            StringBuilder sb = new StringBuilder();
            DateTime lastDataTime = DateTime.MinValue;
            try
            {
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 100; // 短超时轮询，配合空闲判断
                byte[] buffer = new byte[4096];

                // 长连接：持续读取直到客户端断开或服务停止
                while (_isRunning && client.Connected)
                {
                    int bytesRead = 0;
                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException ioex)
                    {
                        // 区分读超时（继续轮询）与真实断线（退出）
                        SocketException se = ioex.InnerException as SocketException;
                        if (se != null && se.SocketErrorCode == SocketError.TimedOut)
                            bytesRead = -2;
                        else
                            break;
                    }

                    if (bytesRead == 0) break; // 客户端正常断开

                    if (bytesRead > 0)
                    {
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        lastDataTime = DateTime.Now;
                    }

                    // 空闲超过阈值且缓冲区有数据 => 视为一帧完整数据，上报
                    if (sb.Length > 0 && lastDataTime != DateTime.MinValue
                        && (DateTime.Now - lastDataTime).TotalMilliseconds >= IdleFlushMs)
                    {
                        string frame = sb.ToString();
                        sb.Length = 0;
                        lastDataTime = DateTime.MinValue;
                        EmitLines(frame);
                    }
                }

                // 连接断开前把残余数据也上报
                if (sb.Length > 0)
                    EmitLines(sb.ToString());
            }
            catch { }
            finally
            {
                lock (_clients) { _clients.Remove(client); }
                try { client.Close(); } catch { }
            }
        }

        private void EmitLines(string frame)
        {
            // 一帧可能含多行，逐行上报
            string[] lines = frame.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string s = line.Trim();
                if (s.Length > 0 && DataReceived != null)
                    DataReceived(this, new DataEventArgs(s));
            }
        }

        public void Stop()
        {
            _isRunning = false;
            if (_listener != null)
            {
                try { _listener.Stop(); } catch { }
                _listener = null;
            }
            lock (_clients)
            {
                foreach (TcpClient c in _clients)
                {
                    try { c.Close(); } catch { }
                }
                _clients.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
