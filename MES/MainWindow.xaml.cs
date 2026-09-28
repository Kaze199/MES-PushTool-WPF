using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Windows;

namespace MESPushTool
{
    public partial class MainWindow : Window
    {
        private AppConfig _config;
        private SerialPortManager _serialManager;
        private TcpServerManager _tcpManager;

        // 后台推送队列，避免HTTP请求卡住界面和接收线程
        private Queue<string> _pushQueue = new Queue<string>();
        private AutoResetEvent _pushSignal = new AutoResetEvent(false);
        private Thread _pushThread;
        private bool _pushRunning;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            InitUI();
            StartPushThread();
        }

        private void LoadConfig()
        {
            _config = ConfigManager.Load();
        }

        private void InitUI()
        {
            // MES配置
            txtMesUrl.Text = _config.MesUrl;
            txtToken.Text = _config.Token;
            txtWsType.Text = _config.WsType;
            txtWsNo.Text = _config.WsNo;
            txtWorkOrder.Text = _config.WorkOrder;

            // 通讯模式
            if (_config.CommMode == "Serial")
                rbSerial.IsChecked = true;
            else
                rbTcp.IsChecked = true;

            // 串口列表
            cmbSerialPort.Items.Clear();
            foreach (string port in SerialPort.GetPortNames())
                cmbSerialPort.Items.Add(port);
            if (cmbSerialPort.Items.Contains(_config.SerialPortName))
                cmbSerialPort.SelectedItem = _config.SerialPortName;
            else if (cmbSerialPort.Items.Count > 0)
                cmbSerialPort.SelectedIndex = 0;

            txtBaudRate.Text = _config.SerialBaudRate.ToString();
            txtDataBits.Text = _config.SerialDataBits.ToString();
            txtTcpPort.Text = _config.TcpPort.ToString();

            // 校验位
            cmbParity.Items.Clear();
            cmbParity.Items.Add("None");
            cmbParity.Items.Add("Odd");
            cmbParity.Items.Add("Even");
            cmbParity.Items.Add("Mark");
            cmbParity.Items.Add("Space");
            if (cmbParity.Items.Contains(_config.SerialParity))
                cmbParity.SelectedItem = _config.SerialParity;
            else
                cmbParity.SelectedItem = "None";

            // 停止位
            cmbStopBits.Items.Clear();
            cmbStopBits.Items.Add("One");
            cmbStopBits.Items.Add("Two");
            cmbStopBits.Items.Add("OnePointFive");
            if (cmbStopBits.Items.Contains(_config.SerialStopBits))
                cmbStopBits.SelectedItem = _config.SerialStopBits;
            else
                cmbStopBits.SelectedItem = "One";

            dpLogDate.SelectedDate = DateTime.Today;

            UpdateUIState();
        }

        private void UpdateUIState()
        {
            bool serialMode = rbSerial.IsChecked == true;
            cmbSerialPort.IsEnabled = serialMode;
            txtBaudRate.IsEnabled = serialMode;
            txtDataBits.IsEnabled = serialMode;
            cmbParity.IsEnabled = serialMode;
            cmbStopBits.IsEnabled = serialMode;
            txtTcpPort.IsEnabled = !serialMode;
        }

        private void CommMode_Checked(object sender, RoutedEventArgs e)
        {
            UpdateUIState();
        }

        private void btnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();
            if (ConfigManager.Save(_config))
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("保存配置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void SaveConfigFromUI()
        {
            _config.MesUrl = txtMesUrl.Text.Trim();
            _config.Token = txtToken.Text.Trim();
            _config.WsType = txtWsType.Text.Trim();
            _config.WsNo = txtWsNo.Text.Trim();
            _config.WorkOrder = txtWorkOrder.Text.Trim();
            _config.CommMode = (rbSerial.IsChecked == true) ? "Serial" : "Tcp";
            _config.SerialPortName = cmbSerialPort.SelectedItem != null ? cmbSerialPort.SelectedItem.ToString() : "";
            _config.SerialParity = cmbParity.SelectedItem != null ? cmbParity.SelectedItem.ToString() : "None";
            _config.SerialStopBits = cmbStopBits.SelectedItem != null ? cmbStopBits.SelectedItem.ToString() : "One";
            int baudRate, dataBits, tcpPort;
            if (int.TryParse(txtBaudRate.Text.Trim(), out baudRate)) _config.SerialBaudRate = baudRate;
            if (int.TryParse(txtDataBits.Text.Trim(), out dataBits)) _config.SerialDataBits = dataBits;
            if (int.TryParse(txtTcpPort.Text.Trim(), out tcpPort)) _config.TcpPort = tcpPort;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (rbSerial.IsChecked == true)
            {
                if (cmbSerialPort.SelectedItem == null)
                { MessageBox.Show("请选择串口"); return; }
                int baudRate, dataBits;
                if (!int.TryParse(txtBaudRate.Text.Trim(), out baudRate))
                { MessageBox.Show("波特率格式错误"); return; }
                if (!int.TryParse(txtDataBits.Text.Trim(), out dataBits))
                { MessageBox.Show("数据位格式错误"); return; }

                Parity parity = (Parity)Enum.Parse(typeof(Parity), cmbParity.SelectedItem.ToString());
                StopBits stopBits = (StopBits)Enum.Parse(typeof(StopBits), cmbStopBits.SelectedItem.ToString());

                _serialManager = new SerialPortManager();
                _serialManager.DataReceived += OnDataReceived;
                _serialManager.ErrorOccurred += OnErrorOccurred;
                if (!_serialManager.Open(cmbSerialPort.SelectedItem.ToString(), baudRate, dataBits, parity, stopBits))
                { return; }
                AppendReceive("串口监听已启动: " + cmbSerialPort.SelectedItem.ToString());
            }
            else
            {
                int port;
                if (!int.TryParse(txtTcpPort.Text.Trim(), out port))
                { MessageBox.Show("TCP端口格式错误"); return; }

                _tcpManager = new TcpServerManager();
                _tcpManager.DataReceived += OnDataReceived;
                _tcpManager.ErrorOccurred += OnErrorOccurred;
                if (!_tcpManager.Start(port))
                { return; }
                AppendReceive("TCP服务已启动,端口: " + port);
            }

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_serialManager != null)
            {
                _serialManager.Dispose();
                _serialManager = null;
                AppendReceive("串口监听已停止");
            }
            if (_tcpManager != null)
            {
                _tcpManager.Stop();
                _tcpManager = null;
                AppendReceive("TCP服务已停止");
            }
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
        }

        private void OnDataReceived(object sender, DataEventArgs e)
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                AppendReceive("收到: " + e.Data);
                EnqueuePush(e.Data);
            }));
        }

        private void OnErrorOccurred(object sender, DataEventArgs e)
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                AppendReceive("错误: " + e.Data);
                LogManager.Write("错误: " + e.Data);
            }));
        }

        // ========== 异步推送队列 ==========

        private void StartPushThread()
        {
            _pushRunning = true;
            _pushThread = new Thread(PushLoop);
            _pushThread.IsBackground = true;
            _pushThread.Start();
        }

        private void EnqueuePush(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData)) return;
            lock (_pushQueue)
            {
                _pushQueue.Enqueue(rawData);
            }
            _pushSignal.Set();
        }

        private void PushLoop()
        {
            while (_pushRunning)
            {
                _pushSignal.WaitOne(500);
                while (true)
                {
                    string rawData = null;
                    lock (_pushQueue)
                    {
                        if (_pushQueue.Count > 0)
                            rawData = _pushQueue.Dequeue();
                    }
                    if (rawData == null) break;
                    DoPush(rawData);
                }
            }
        }

        private void DoPush(string rawData)
        {
            // 解析SN：@分隔
            string[] parts = rawData.Split('@');
            List<string> snList = new List<string>();
            foreach (string p in parts)
            {
                string sn = p.Trim();
                if (!string.IsNullOrEmpty(sn))
                    snList.Add(sn);
            }
            if (snList.Count == 0) return;

            // 读取界面参数（需切回UI线程）
            string url = "", token = "", wsType = "", wsNo = "", workOrder = "";
            Dispatcher.Invoke(new ThreadStart(delegate
            {
                url = txtMesUrl.Text.Trim();
                token = txtToken.Text.Trim();
                wsType = txtWsType.Text.Trim();
                wsNo = txtWsNo.Text.Trim();
                workOrder = txtWorkOrder.Text.Trim();
            }));

            string gsn = snList[0]; // 组SN=第一个小板SN

            MESClient client = new MESClient(url, token, wsType, wsNo, "");
            string requestBody;
            string response = client.PushSN(workOrder, gsn, snList, out requestBody);

            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                AppendReceive("推送内容: " + requestBody);
                AppendResult("返回: " + response);
            }));
            LogManager.WritePush(rawData, requestBody, response);
        }

        // ========== 其他 ==========

        private void btnManualPush_Click(object sender, RoutedEventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox("请输入SN数据(多个用@分隔):", "手动推送", "A0001@A0002@A0003");
            if (!string.IsNullOrEmpty(input))
                EnqueuePush(input);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtReceive.Clear();
            txtResult.Clear();
        }

        private void AppendReceive(string msg)
        {
            txtReceive.AppendText(DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n");
            txtReceive.ScrollToEnd();
        }

        private void AppendResult(string msg)
        {
            txtResult.AppendText(DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n");
            txtResult.ScrollToEnd();
        }

        private void btnQueryLog_Click(object sender, RoutedEventArgs e)
        {
            if (!dpLogDate.SelectedDate.HasValue) return;
            string log = LogManager.Read(dpLogDate.SelectedDate.Value);
            ShowLogWindow(dpLogDate.SelectedDate.Value, log);
        }

        private void ShowLogWindow(DateTime date, string content)
        {
            Window win = new Window();
            win.Title = "日志 - " + date.ToString("yyyy-MM-dd");
            win.Width = 1000;
            win.Height = 560;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.Owner = this;

            System.Windows.Controls.DataGrid grid = new System.Windows.Controls.DataGrid();
            grid.IsReadOnly = true;                    // 只读但可选中复制
            grid.AutoGenerateColumns = false;
            grid.CanUserAddRows = false;
            grid.GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.All;
            grid.HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column;
            grid.SelectionMode = System.Windows.Controls.DataGridSelectionMode.Single;
            grid.SelectionUnit = System.Windows.Controls.DataGridSelectionUnit.CellOrRowHeader;
            grid.CanUserSortColumns = false;   // 点击列头不排序
            grid.Margin = new Thickness(4);
            // RightToLeft 使垂直滚动条显示在左侧
            grid.FlowDirection = FlowDirection.RightToLeft;
            // 滚动条始终显示，方便拖拽
            grid.SetValue(System.Windows.Controls.ScrollViewer.VerticalScrollBarVisibilityProperty, System.Windows.Controls.ScrollBarVisibility.Visible);
            grid.SetValue(System.Windows.Controls.ScrollViewer.HorizontalScrollBarVisibilityProperty, System.Windows.Controls.ScrollBarVisibility.Auto);

            // RightToLeft 会让列从右往左排，所以倒序添加保持原有视觉顺序
            grid.Columns.Add(MakeColumn("返回结果", "Response", 200));
            grid.Columns.Add(MakeColumn("推送内容", "Request", new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)));
            grid.Columns.Add(MakeColumn("接收数据", "RawData", 180));
            grid.Columns.Add(MakeColumn("类型", "Type", 50));
            grid.Columns.Add(MakeColumn("时间", "Time", 150));

            System.Collections.Generic.List<LogEntry> entries = LogManager.Parse(content);
            grid.ItemsSource = entries;

            if (entries.Count == 0)
            {
                grid.Columns.Clear();
                grid.ItemsSource = null;
                System.Windows.Controls.TextBlock empty = new System.Windows.Controls.TextBlock();
                empty.Text = "当日无日志";
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.VerticalAlignment = VerticalAlignment.Center;
                win.Content = empty;
            }
            else
            {
                win.Content = grid;
            }
            win.Show();
        }

        private System.Windows.Controls.DataGridTextColumn MakeColumn(string header, string binding, System.Windows.Controls.DataGridLength width)
        {
            System.Windows.Controls.DataGridTextColumn col = new System.Windows.Controls.DataGridTextColumn();
            col.Header = header;
            col.Binding = new System.Windows.Data.Binding(binding);
            col.Width = width;
            // 表格整体RightToLeft（滚动条在左），单元格内容保持从左到右
            Style cellStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            cellStyle.Setters.Add(new Setter(FlowDirectionProperty, FlowDirection.LeftToRight));
            col.ElementStyle = cellStyle;
            return col;
        }

        private void btnExportLog_Click(object sender, RoutedEventArgs e)
        {
            if (!dpLogDate.SelectedDate.HasValue) return;
            string log = LogManager.Read(dpLogDate.SelectedDate.Value);
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "文本文件|*.txt|所有文件|*.*";
            dlg.FileName = dpLogDate.SelectedDate.Value.ToString("yyyy-MM-dd") + ".txt";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // UTF8带BOM，记事本打开中文不乱码
                    File.WriteAllText(dlg.FileName, log, new System.Text.UTF8Encoding(true));
                    MessageBox.Show("导出成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show("导出失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _pushRunning = false;
            _pushSignal.Set();
            btnStop_Click(null, null);
            base.OnClosing(e);
        }
    }
}
