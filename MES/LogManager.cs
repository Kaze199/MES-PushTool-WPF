using System;
using System.Collections.Generic;
using System.IO;

namespace MESPushTool
{
    // 一条日志记录
    public class LogEntry
    {
        public string Time { get; set; }
        public string Type { get; set; }
        public string RawData { get; set; }
        public string Request { get; set; }
        public string Response { get; set; }
    }

    public static class LogManager
    {
        private static readonly string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        static LogManager()
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
        }

        private static string Clean(string s)
        {
            if (s == null) return "";
            return s.Replace('\t', ' ').Replace("\r", " ").Replace("\n", " ");
        }

        // 推送日志：制表符分隔，方便表格化解析
        public static void WritePush(string rawData, string requestBody, string response)
        {
            WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + "\t推送\t" + Clean(rawData) + "\t" + Clean(requestBody) + "\t" + Clean(response));
        }

        // 普通事件/错误日志
        public static void Write(string message)
        {
            WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + "\t记录\t" + Clean(message) + "\t\t");
        }

        private static void WriteLine(string line)
        {
            string logFile = Path.Combine(LogDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            try
            {
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
            catch { }
        }

        public static string Read(DateTime date)
        {
            string logFile = Path.Combine(LogDir, date.ToString("yyyy-MM-dd") + ".log");
            if (File.Exists(logFile))
            {
                try { return File.ReadAllText(logFile); }
                catch { return "读取日志失败"; }
            }
            return "";
        }

        // 把日志文本解析成表格行
        public static List<LogEntry> Parse(string content)
        {
            List<LogEntry> list = new List<LogEntry>();
            if (string.IsNullOrEmpty(content)) return list;

            string[] lines = content.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                // 新格式：制表符分隔 时间\t类型\t接收数据\t推送内容\t返回结果
                string[] parts = line.Split('\t');
                if (parts.Length >= 5)
                {
                    list.Add(new LogEntry { Time = parts[0], Type = parts[1], RawData = parts[2], Request = parts[3], Response = parts[4] });
                    continue;
                }

                // 旧格式1：时间 | 推送: xxx | 返回: yyy
                int idxPush = line.IndexOf("推送: ");
                int idxResp = line.IndexOf(" | 返回: ");
                if (idxPush >= 0 && idxResp > idxPush)
                {
                    string time = idxPush > 21 ? line.Substring(0, 19) : "";
                    string req = line.Substring(idxPush + 4, idxResp - idxPush - 4);
                    string resp = line.Substring(idxResp + 7);
                    list.Add(new LogEntry { Time = time, Type = "推送", RawData = "", Request = req, Response = resp });
                    continue;
                }

                // 旧格式2：时间 | 消息
                if (line.Length > 22 && line[19] == ' ' && line.IndexOf(" | ") == 19)
                {
                    list.Add(new LogEntry { Time = line.Substring(0, 19), Type = "记录", RawData = line.Substring(22), Request = "", Response = "" });
                    continue;
                }

                // 无法解析的行，显示在"接收数据"列
                list.Add(new LogEntry { Time = "", Type = "", RawData = line, Request = "", Response = "" });
            }
            return list;
        }
    }
}
