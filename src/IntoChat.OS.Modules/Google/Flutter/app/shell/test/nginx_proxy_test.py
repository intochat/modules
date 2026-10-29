"""Same-origin runtime proof; requires Docker, no Flutter build or cloud resources."""

import base64
import http.client
import http.server
import json
import pathlib
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request


LARGE_BODY = bytes(range(256)) * 1024


class Kernel(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_POST(self):
        self.do_GET()

    def do_GET(self):
        request_body = self.rfile.read(int(self.headers.get("Content-Length", "0")))
        if self.path == "/neurons.Neurons/Large":
            self.send_response(200)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Transfer-Encoding", "chunked")
            self.end_headers()
            for offset in range(0, len(LARGE_BODY), 4096):
                chunk = LARGE_BODY[offset:offset + 4096]
                self.wfile.write(f"{len(chunk):x}\r\n".encode() + chunk + b"\r\n")
                self.wfile.flush()
            self.wfile.write(b"0\r\n\r\n")
            self.wfile.flush()
            return
        if self.path == "/transport/websocket":
            if (self.headers.get("Upgrade", "").lower() != "websocket"
                    or self.headers.get("Connection", "").lower() != "upgrade"):
                self.send_error(400)
                return
            self.send_response(101)
            self.send_header("Upgrade", "websocket")
            self.send_header("Connection", "Upgrade")
            self.send_header("Sec-WebSocket-Accept", "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=")
            self.end_headers()
            self.close_connection = True
            return
        body = json.dumps({"path": self.path, "host": self.headers.get("Host"),
                           "forwardedHost": self.headers.get("X-Forwarded-Host"),
                           "scheme": self.headers.get("X-Forwarded-Proto"),
                           "cookie": self.headers.get("Cookie")}).encode()
        if self.path == "/transport/headers":
            body = json.dumps({"connection": self.headers.get("Connection"),
                               "method": self.command,
                               "body": request_body.decode()}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Set-Cookie", "session=proof; HttpOnly; Secure; SameSite=Lax; Path=/")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *_):
        pass


class NginxProxyFacts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.kernel = http.server.ThreadingHTTPServer(("0.0.0.0", 0), Kernel)
        cls.addClassCleanup(cls.kernel.server_close)
        threading.Thread(target=cls.kernel.serve_forever, daemon=True).start()
        cls.addClassCleanup(cls.kernel.shutdown)
        cls.files = tempfile.TemporaryDirectory(prefix="intochat-nginx-proof-")
        cls.addClassCleanup(cls.files.cleanup)
        # TemporaryDirectory is 0700 on Linux; nginx workers must traverse the mount.
        pathlib.Path(cls.files.name).chmod(0o755)
        pathlib.Path(cls.files.name, "index.html").write_text("SHELL", encoding="utf-8")
        shell = pathlib.Path(__file__).resolve().parents[1]
        command = ["docker", "run", "--rm", "-d", "-p", "127.0.0.1::80",
                   "-e", f"INTOCHATOS_KERNEL_UPSTREAM=http://host.docker.internal:{cls.kernel.server_port}",
                   "-e", "NGINX_ENVSUBST_FILTER=^INTOCHATOS_KERNEL_UPSTREAM$",
                   "--mount", f"type=bind,source={cls.files.name},target=/usr/share/nginx/html,readonly",
                   "--mount", f"type=bind,source={shell / 'nginx' / 'default.conf.template'},target=/etc/nginx/templates/default.conf.template,readonly",
                   "nginx:1.27-alpine"]
        if sys.platform.startswith("linux"):
            command[2:2] = ["--add-host", "host.docker.internal:host-gateway"]
        cls.container = subprocess.check_output(command, text=True, stderr=subprocess.PIPE).strip()
        cls.addClassCleanup(subprocess.run, ["docker", "stop", cls.container],
                            check=True, capture_output=True)
        address = subprocess.check_output(["docker", "port", cls.container, "80/tcp"], text=True).strip()
        cls.base = "http://" + address
        last_failure = "no response"
        for _ in range(60):
            try:
                urllib.request.urlopen(cls.base, timeout=1).close()
                return
            except urllib.error.HTTPError as error:
                last_failure = f"HTTP {error.code}"
            except OSError as error:
                last_failure = type(error).__name__
            time.sleep(0.1)
        raise RuntimeError(f"nginx readiness failed: {last_failure}")

    def test_root_stays_static(self):
        with urllib.request.urlopen(self.base) as response:
            self.assertEqual(response.read(), b"SHELL")

    def test_auth_identity_portal_and_transport_share_host_and_cookie(self):
        for path in ["/auth/handoff?t=QUERY-CANARY", "/identity/session", "/portal/assets/auth.js", "/neurons.Neurons/Call"]:
            with self.subTest(path=path):
                request = urllib.request.Request(self.base + path, headers={"Host": "os.example.test", "X-Forwarded-Proto": "https", "Cookie": "session=proof"})
                with urllib.request.urlopen(request) as response:
                    body = json.load(response)
                    self.assertEqual(body, {"path": path, "host": f"host.docker.internal:{self.kernel.server_port}", "forwardedHost": "os.example.test", "scheme": "https", "cookie": "session=proof"})
                    self.assertIn("HttpOnly", response.headers["Set-Cookie"])

    def test_proxy_logs_never_contain_query_tickets(self):
        urllib.request.urlopen(self.base + "/auth/handoff?t=QUERY-CANARY").close()
        logs = subprocess.check_output(["docker", "logs", self.container], text=True, stderr=subprocess.STDOUT)
        self.assertNotIn("QUERY-CANARY", logs)

    def test_ordinary_requests_omit_upstream_connection_even_when_client_closes(self):
        for method, body in [("GET", None), ("POST", b"request-proof")]:
            with self.subTest(method=method):
                request = urllib.request.Request(self.base + "/transport/headers", data=body,
                                                 method=method, headers={"Connection": "close"})
                with urllib.request.urlopen(request, timeout=5) as response:
                    self.assertEqual(json.load(response), {"connection": None, "method": method,
                                                           "body": "" if body is None else "request-proof"})

    def test_large_chunked_response_is_delivered_completely(self):
        request = urllib.request.Request(self.base + "/neurons.Neurons/Large", data=b"call",
                                         headers={"Connection": "close"})
        with urllib.request.urlopen(request, timeout=5) as response:
            self.assertEqual(response.status, 200)
            self.assertEqual(response.headers["Transfer-Encoding"], "chunked")
            self.assertIsNone(response.headers.get("Content-Length"))
            self.assertEqual(response.read(), LARGE_BODY)

    def test_websocket_upgrade_reaches_upstream_and_returns_switching_protocols(self):
        connection = http.client.HTTPConnection(self.base.removeprefix("http://"), timeout=5)
        try:
            connection.request("GET", "/transport/websocket", headers={
                "Connection": "Upgrade", "Upgrade": "websocket",
                # RFC 6455's public sample nonce, not an authentication credential.
                "Sec-WebSocket-Key": base64.b64encode(b"the sample nonce").decode(),
                "Sec-WebSocket-Version": "13"})
            response = connection.getresponse()
            self.assertEqual(response.status, 101)
            self.assertEqual(response.headers["Upgrade"].lower(), "websocket")
            self.assertEqual(response.headers["Connection"].lower(), "upgrade")
            self.assertEqual(response.headers["Sec-WebSocket-Accept"], "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=")
        finally:
            connection.close()


if __name__ == "__main__":
    unittest.main()
