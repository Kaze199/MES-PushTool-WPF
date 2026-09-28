using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace MESPushTool
{
    public class MESClient
    {
        private string _url;
        private string _token;
        private string _wsType;
        private string _wsNo;
        private string _outDataType;

        public MESClient(string url, string token, string wsType, string wsNo, string outDataType)
        {
            _url = url;
            _token = token;
            _wsType = wsType;
            _wsNo = wsNo;
            _outDataType = outDataType;
        }

        public string PushSN(string workOrder, string gsn, List<string> snList, out string requestBody)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[{\"WORKORDER\":\"");
            sb.Append(workOrder);
            sb.Append("\",\"GSN\":\"");
            sb.Append(gsn);
            sb.Append("\",\"SN\":\"");
            sb.Append(string.Join(",", snList));
            sb.Append("\"}]");
            string varJson = sb.ToString();

            requestBody = string.Format(
                "token={0}&wstype={1}&wsno={2}&varJson={3}&outdatatype={4}",
                Uri.EscapeDataString(_token),
                Uri.EscapeDataString(_wsType),
                Uri.EscapeDataString(_wsNo),
                Uri.EscapeDataString(varJson),
                Uri.EscapeDataString(_outDataType));

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 10000; // 10秒超时，服务器不可达时快速失败

                byte[] postData = Encoding.UTF8.GetBytes(requestBody);
                request.ContentLength = postData.Length;

                using (Stream reqStream = request.GetRequestStream())
                {
                    reqStream.Write(postData, 0, postData.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream respStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(respStream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }
}
