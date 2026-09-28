# -*- coding: utf-8 -*-
# 模拟MES接口服务器，用于本地测试推送链路
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
import json
from datetime import datetime


class MockMESHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        length = int(self.headers.get('Content-Length', 0))
        body = self.rfile.read(length).decode('utf-8')
        params = parse_qs(body)

        print('[%s] 收到推送:' % datetime.now().strftime('%H:%M:%S'))
        for k, v in params.items():
            print('  %s = %s' % (k, v[0]))

        # 校验必填参数，模拟真实接口行为
        token = params.get('token', [''])[0]
        var_json = params.get('varJson', [''])[0]

        if token != 'HQMESSMT@2025':
            result = {'returncode': '101', 'returnmsg': 'token错误', 'body': ''}
        else:
            try:
                data = json.loads(var_json)
                wo = data[0].get('WORKORDER', '')
                gsn = data[0].get('GSN', '')
                if not wo:
                    result = {'returncode': '102', 'returnmsg': '工单号不能为空', 'body': ''}
                elif not gsn:
                    result = {'returncode': '103', 'returnmsg': '组SN不能为空', 'body': ''}
                else:
                    result = {'returncode': '200', 'returnmsg': 'OK', 'body': ''}
            except Exception as e:
                result = {'returncode': '104', 'returnmsg': 'varJson解析失败: %s' % e, 'body': ''}

        resp = json.dumps(result, ensure_ascii=False).encode('utf-8')
        self.send_response(200)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(resp)))
        self.end_headers()
        self.wfile.write(resp)
        print('  返回: %s' % result)

    def log_message(self, fmt, *args):
        pass  # 屏蔽默认访问日志


if __name__ == '__main__':
    server = HTTPServer(('0.0.0.0', 8021), MockMESHandler)
    print('模拟MES服务器已启动: http://127.0.0.1:8021/MES_WS.asmx')
    print('在MES推送工具中把地址改为 http://127.0.0.1:8021/MES_WS.asmx 即可测试')
    print('按 Ctrl+C 停止')
    server.serve_forever()
