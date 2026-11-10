using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using WindowsStats.Model;
using WindowsStats.Monitoring;

namespace WindowsStats.Server
{
    public class HttpServer : IDisposable
    {
        private readonly int _port;
        private readonly HardwareMonitor _monitor;
        private TcpListener _listener;
        private Thread _listenThread;
        private volatile bool _running;
        private string _cachedHtml;
        private readonly List<NetworkStream> _sseStreams = new List<NetworkStream>();
        private readonly object _sseLock = new object();

        private class StaticAsset
        {
            public string ContentType;
            public byte[] Content;
        }

        private readonly Dictionary<string, StaticAsset> _staticAssets = new Dictionary<string, StaticAsset>(StringComparer.OrdinalIgnoreCase);

        public int Port { get { return _port; } }

        public HttpServer(int port, HardwareMonitor monitor)
        {
            _port = port;
            _monitor = monitor;
            LoadEmbeddedAssets();

            _monitor.OnSnapshotUpdated += HandleSnapshotUpdated;
        }

        private void LoadEmbeddedAssets()
        {
            LoadEmbeddedHtml();
            RegisterStaticAsset("/manifest.json", "application/manifest+json; charset=utf-8", "manifest.json", Path.Combine("src", "Web", "manifest.json"));
            RegisterStaticAsset("/sw.js", "application/javascript; charset=utf-8", "sw.js", Path.Combine("src", "Web", "sw.js"));
            RegisterStaticAsset("/assets/icon.svg", "image/svg+xml", "icon.svg", Path.Combine("assets", "icon.svg"));
            RegisterStaticAsset("/assets/icon-192.png", "image/png", "icon-192.png", Path.Combine("assets", "icon-192.png"));
            RegisterStaticAsset("/assets/icon-512.png", "image/png", "icon-512.png", Path.Combine("assets", "icon-512.png"));
            RegisterStaticAsset("/assets/apple-touch-icon.png", "image/png", "apple-touch-icon.png", Path.Combine("assets", "apple-touch-icon.png"));
            RegisterStaticAsset("/favicon.ico", "image/x-icon", "app.ico", Path.Combine("assets", "app.ico"));
        }

        private void RegisterStaticAsset(string urlPath, string contentType, string resourceSuffix, string diskRelativePath)
        {
            try
            {
                byte[] data = null;
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] names = asm.GetManifestResourceNames();
                foreach (var n in names)
                {
                    if (n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = asm.GetManifestResourceStream(n))
                        {
                            if (s != null)
                            {
                                data = new byte[s.Length];
                                s.Read(data, 0, data.Length);
                                break;
                            }
                        }
                    }
                }

                if (data == null)
                {
                    string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, diskRelativePath);
                    if (File.Exists(diskPath))
                    {
                        data = File.ReadAllBytes(diskPath);
                    }
                }

                if (data != null)
                {
                    _staticAssets[urlPath] = new StaticAsset { ContentType = contentType, Content = data };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning loading static asset " + urlPath + ": " + ex.Message);
            }
        }

        private void LoadEmbeddedHtml()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string[] names = asm.GetManifestResourceNames();
                string targetName = null;
                foreach (var n in names)
                {
                    if (n.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
                    {
                        targetName = n;
                        break;
                    }
                }

                if (targetName != null)
                {
                    using (Stream s = asm.GetManifestResourceStream(targetName))
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    {
                        _cachedHtml = r.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Warning loading index.html: " + ex.Message);
            }

            if (string.IsNullOrEmpty(_cachedHtml))
            {
                // Fallback to loading from disk if running in development mode
                string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Web", "index.html");
                if (File.Exists(localFile))
                {
                    _cachedHtml = File.ReadAllText(localFile, Encoding.UTF8);
                }
                else
                {
                    _cachedHtml = "<!DOCTYPE html><html><head><title>WindowsStats</title></head><body><h1>WindowsStats Running</h1><p><a href='/api/stats'>/api/stats</a></p></body></html>";
                }
            }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;

            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                _listenThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "WindowsStats_HttpListener"
                };
                _listenThread.Start();
                Console.WriteLine("Web server started successfully on http://0.0.0.0:" + _port);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting TcpListener on port " + _port + ": " + ex.Message);
            }
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClientConnection, client);
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private void SafeCloseClient(TcpClient client)
        {
            try
            {
                if (client != null && client.Client != null && client.Connected)
                {
                    client.Client.Shutdown(SocketShutdown.Send);
                }
            }
            catch {}
            try { if (client != null) client.Close(); } catch {}
        }

        private void HandleClientConnection(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[4096];
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    SafeCloseClient(client);
                    return;
                }

                string requestHeader = Encoding.UTF8.GetString(buffer, 0, read);
                string[] lines = requestHeader.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    SafeCloseClient(client);
                    return;
                }

                string firstLine = lines[0];
                string[] parts = firstLine.Split(' ');
                if (parts.Length < 2)
                {
                    SafeCloseClient(client);
                    return;
                }

                string method = parts[0].ToUpperInvariant();
                string url = parts[1];
                int queryIndex = url.IndexOf('?');
                string path = queryIndex >= 0 ? url.Substring(0, queryIndex) : url;

                if (method == "OPTIONS")
                {
                    SendCorsPreflight(stream);
                    SafeCloseClient(client);
                    return;
                }

                if (path == "/" || path == "/index.html")
                {
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(_cachedHtml);
                    SendResponse(stream, "text/html; charset=utf-8", htmlBytes);
                    SafeCloseClient(client);
                }
                else if (_staticAssets.ContainsKey(path))
                {
                    StaticAsset asset = _staticAssets[path];
                    SendResponse(stream, asset.ContentType, asset.Content);
                    SafeCloseClient(client);
                }
                else if (path == "/api/stats")
                {
                    HardwareSnapshot snap = _monitor.GetLatestSnapshot();
                    string json = snap.ToJson();
                    SendResponse(stream, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
                    SafeCloseClient(client);
                }
                else if (path == "/api/stream")
                {
                    // Server-Sent Events (SSE)
                    SendSseHeaders(stream);
                    _monitor.RegisterClientConnected();

                    lock (_sseLock)
                    {
                        _sseStreams.Add(stream);
                    }

                    // Send immediate initial event
                    HardwareSnapshot snap = _monitor.GetLatestSnapshot();
                    string initialEvent = "data: " + snap.ToJson() + "\n\n";
                    byte[] initialBytes = Encoding.UTF8.GetBytes(initialEvent);
                    try
                    {
                        stream.Write(initialBytes, 0, initialBytes.Length);
                        stream.Flush();
                    }
                    catch
                    {
                        RemoveSseClient(stream);
                        SafeCloseClient(client);
                    }
                }
                else if (path == "/api/ping")
                {
                    string json = "{\"status\":\"ok\",\"service\":\"WindowsStats\",\"timestamp\":" + 
                        (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + "}";
                    SendResponse(stream, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
                    SafeCloseClient(client);
                }
                else if (path == "/api/exit" || path == "/api/shutdown")
                {
                    string json = "{\"status\":\"shutting_down\"}";
                    SendResponse(stream, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
                    SafeCloseClient(client);
                    ThreadPool.QueueUserWorkItem(delegate {
                        Thread.Sleep(300);
                        Environment.Exit(0);
                    });
                }
                else
                {
                    Send404(stream);
                    SafeCloseClient(client);
                }
            }
            catch
            {
                SafeCloseClient(client);
            }
        }

        private void HandleSnapshotUpdated(HardwareSnapshot snapshot)
        {
            string sseMessage = "data: " + snapshot.ToJson() + "\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(sseMessage);

            List<NetworkStream> targets;
            lock (_sseLock)
            {
                if (_sseStreams.Count == 0) return;
                targets = new List<NetworkStream>(_sseStreams);
            }

            List<NetworkStream> deadStreams = null;
            foreach (var stream in targets)
            {
                try
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                catch
                {
                    if (deadStreams == null) deadStreams = new List<NetworkStream>();
                    deadStreams.Add(stream);
                }
            }

            if (deadStreams != null)
            {
                lock (_sseLock)
                {
                    foreach (var dead in deadStreams)
                    {
                        _sseStreams.Remove(dead);
                        _monitor.RegisterClientDisconnected();
                        try { dead.Close(); } catch {}
                    }
                }
            }
        }

        private void RemoveSseClient(NetworkStream stream)
        {
            lock (_sseLock)
            {
                if (_sseStreams.Remove(stream))
                {
                    _monitor.RegisterClientDisconnected();
                }
            }
        }

        private void SendResponse(NetworkStream stream, string contentType, byte[] body)
        {
            string header = "HTTP/1.1 200 OK\r\n" +
                            "Content-Type: " + contentType + "\r\n" +
                            "Content-Length: " + body.Length + "\r\n" +
                            "Access-Control-Allow-Origin: *\r\n" +
                            "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
                            "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private void SendSseHeaders(NetworkStream stream)
        {
            string header = "HTTP/1.1 200 OK\r\n" +
                            "Content-Type: text/event-stream; charset=utf-8\r\n" +
                            "Cache-Control: no-cache\r\n" +
                            "Access-Control-Allow-Origin: *\r\n" +
                            "Connection: keep-alive\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Flush();
        }

        private void SendCorsPreflight(NetworkStream stream)
        {
            string header = "HTTP/1.1 204 No Content\r\n" +
                            "Access-Control-Allow-Origin: *\r\n" +
                            "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                            "Access-Control-Allow-Headers: Content-Type\r\n" +
                            "Content-Length: 0\r\n" +
                            "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Flush();
        }

        private void Send404(NetworkStream stream)
        {
            string body = "{\"error\":\"Not Found\"}";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 404 Not Found\r\n" +
                            "Content-Type: application/json; charset=utf-8\r\n" +
                            "Content-Length: " + bodyBytes.Length + "\r\n" +
                            "Access-Control-Allow-Origin: *\r\n" +
                            "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                if (_listener != null) _listener.Stop();
            }
            catch {}

            lock (_sseLock)
            {
                foreach (var s in _sseStreams)
                {
                    try { s.Close(); } catch {}
                }
                _sseStreams.Clear();
            }

            _monitor.OnSnapshotUpdated -= HandleSnapshotUpdated;
        }
    }
}
