using System;
using System.IO;
using System.Xml.Serialization;

namespace MESPushTool
{
    [Serializable]
    public class AppConfig
    {
        // MES接口配置
        public string MesUrl { get; set; }
        public string Token { get; set; }
        public string WsType { get; set; }
        public string WsNo { get; set; }
        public string OutDataType { get; set; }
        public string WorkOrder { get; set; }

        // 通讯方式: Serial 或 Tcp
        public string CommMode { get; set; }

        // 串口配置
        public string SerialPortName { get; set; }
        public int SerialBaudRate { get; set; }
        public int SerialDataBits { get; set; }
        public string SerialParity { get; set; }
        public string SerialStopBits { get; set; }

        // TCP配置
        public int TcpPort { get; set; }

        public AppConfig()
        {
            // 默认值
            MesUrl = "http://192.168.1.15:8021/MES_WS.asmx";
            Token = "HQMESSMT@2025";
            WsType = "POST";
            WsNo = "6009";
            OutDataType = "";
            WorkOrder = "";
            CommMode = "Serial";
            SerialPortName = "COM1";
            SerialBaudRate = 19200;
            SerialDataBits = 8;
            SerialParity = "None";
            SerialStopBits = "One";
            TcpPort = 9000;
        }
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.xml");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppConfig();

                XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
                using (FileStream fs = new FileStream(ConfigPath, FileMode.Open))
                {
                    return (AppConfig)serializer.Deserialize(fs);
                }
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static bool Save(AppConfig config)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AppConfig));
                using (FileStream fs = new FileStream(ConfigPath, FileMode.Create))
                {
                    serializer.Serialize(fs, config);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
