using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PriceDropCatcher.Communication
{
    public class WebSocketServer : IDisposable
    {
        private readonly int _port;
        private Fleck.WebSocketServer _server;
        private int _connectedClients;
        private volatile bool _started;

        public event EventHandler<ProductUrlReceivedEventArgs> ProductUrlReceived;
        public event EventHandler ClientConnected;
        public event EventHandler ClientDisconnected;

        public WebSocketServer(int port)
        {
            _port = port;
        }

        public Task StartAsync()
        {
            if (_started) return Task.CompletedTask;

            _server = new Fleck.WebSocketServer("ws://127.0.0.1:" + _port)
            {
                RestartAfterListenError = true
            };

            _server.Start(conn =>
            {
                conn.OnOpen = () =>
                {
                    if (!IsValidOrigin(GetOrigin(conn)))
                    {
                        conn.Close();
                        return;
                    }
                    Interlocked.Increment(ref _connectedClients);
                    ClientConnected?.Invoke(this, EventArgs.Empty);
                };

                conn.OnClose = () =>
                {
                    Interlocked.Decrement(ref _connectedClients);
                    ClientDisconnected?.Invoke(this, EventArgs.Empty);
                };

                conn.OnMessage = msg => HandleMessage(conn, msg);
            });

            _started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!_started) return Task.CompletedTask;
            try { _server?.Dispose(); } catch { }
            _server = null;
            _started = false;
            return Task.CompletedTask;
        }

        private static string GetOrigin(IWebSocketConnection conn)
        {
            try
            {
                var info = conn.ConnectionInfo;
                if (info == null) return null;
                var o = info.Origin;
                if (!string.IsNullOrEmpty(o)) return o;
                if (info.Headers != null)
                {
                    foreach (var key in new[] { "Origin", "origin" })
                    {
                        if (!info.Headers.ContainsKey(key)) continue;
                        var v = info.Headers[key];
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsValidOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            return origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase);
        }

        private void HandleMessage(IWebSocketConnection conn, string messageJson)
        {
            try
            {
                var root = JObject.Parse(messageJson);

                var type = root["type"]?.Value<string>();
                if (string.Equals(type, "TRACK_PRODUCT", StringComparison.OrdinalIgnoreCase))
                {
                    var url = root["payload"]?["productUrl"]?.Value<string>()
                              ?? root["payload"]?["url"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        ProductUrlReceived?.Invoke(this, new ProductUrlReceivedEventArgs(url.Trim()));
                        SendAck(conn, url.Trim(), "TRACK_PRODUCT");
                    }
                    return;
                }

                var op = root["op"]?.Value<string>() ?? root["operation"]?.Value<string>();
                if (string.Equals(op, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    var pong = new
                    {
                        id = Guid.NewGuid().ToString(),
                        type = "response",
                        op = "pong",
                        payload = new { requestId = root["id"]?.Value<string>() },
                        ts = DateTime.UtcNow.ToString("o"),
                        v = "1.0"
                    };
                    SendJson(conn, JsonConvert.SerializeObject(pong));
                    return;
                }

                if (string.Equals(op, "product.track", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = root["payload"] as JObject;
                    var url = payload?["url"]?.Value<string>() ?? payload?["productUrl"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        ProductUrlReceived?.Invoke(this, new ProductUrlReceivedEventArgs(url.Trim()));
                        SendLegacyAck(conn, url.Trim());
                    }
                    return;
                }
            }
            catch
            {
                // malformed
            }
        }

        private static void SendAck(IWebSocketConnection conn, string url, string via)
        {
            var ack = new
            {
                id = Guid.NewGuid().ToString(),
                type = "event",
                op = "product.track.ack",
                payload = new { url, receivedAt = DateTime.UtcNow.ToString("o"), via },
                ts = DateTime.UtcNow.ToString("o"),
                v = "1.0"
            };
            SendJson(conn, JsonConvert.SerializeObject(ack));
        }

        private static void SendLegacyAck(IWebSocketConnection conn, string url)
        {
            SendAck(conn, url, "product.track");
        }

        private static void SendJson(IWebSocketConnection conn, string json)
        {
            try
            {
                if (conn == null) return;
                conn.Send(json);
            }
            catch { }
        }

        public bool IsRunning => _started;
        public int ConnectedClientsCount => Volatile.Read(ref _connectedClients);

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }

    public class ProductUrlReceivedEventArgs : EventArgs
    {
        public ProductUrlReceivedEventArgs(string productUrl)
        {
            ProductUrl = productUrl;
        }

        public string ProductUrl { get; }
    }
}
