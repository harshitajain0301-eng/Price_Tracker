using PriceDropCatcher.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PriceDropCatcher.Communication
{
    public class WebSocketServer : IDisposable
    {
        private readonly int _port;
        private HttpListener _httpListener;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private int _connectedClients;

        public event EventHandler<TrackedProduct> ProductTracked;
        public event EventHandler ClientConnected;
        public event EventHandler ClientDisconnected;

        public WebSocketServer(int port)
        {
            _port = port;
        }

        public Task StartAsync()
        {
            if (_isRunning) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _httpListener.Start();
            _isRunning = true;

            _ = Task.Run(ListenLoopAsync, _cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!_isRunning) return Task.CompletedTask;

            _isRunning = false;
            try { _cts?.Cancel(); } catch { }

            try { _httpListener?.Stop(); } catch { }
            try { _httpListener?.Close(); } catch { }
            _httpListener = null;

            try { _cts?.Dispose(); } catch { }
            _cts = null;

            return Task.CompletedTask;
        }

        private async Task ListenLoopAsync()
        {
            while (_isRunning && _httpListener != null && !_cts.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleConnectionAsync(context), _cts.Token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    if (_isRunning) continue;
                    break;
                }
                catch
                {
                    // swallow to keep server alive
                }
            }
        }

        private bool IsValidOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            return origin.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase);
        }

        private async Task HandleConnectionAsync(HttpListenerContext context)
        {
            WebSocket socket = null;
            try
            {
                var origin = context.Request.Headers["Origin"];
                if (!IsValidOrigin(origin))
                {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                    return;
                }

                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                socket = wsContext.WebSocket;

                Interlocked.Increment(ref _connectedClients);
                ClientConnected?.Invoke(this, EventArgs.Empty);

                await HandleSocketAsync(socket);
            }
            catch
            {
                // ignore
            }
            finally
            {
                if (socket != null)
                {
                    Interlocked.Decrement(ref _connectedClients);
                    ClientDisconnected?.Invoke(this, EventArgs.Empty);
                }
                try { socket?.Dispose(); } catch { }
            }
        }

        private async Task HandleSocketAsync(WebSocket socket)
        {
            var buffer = new byte[4096];
            var messageBuffer = new List<byte>();

            while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                WebSocketReceiveResult result = null;
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                }
                catch
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
                    }
                    catch { }
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                if (!result.EndOfMessage) continue;

                var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();

                TryHandleMessage(socket, json);
            }
        }

        private void TryHandleMessage(WebSocket socket, string messageJson)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var dict = serializer.DeserializeObject(messageJson) as Dictionary<string, object>;
                if (dict == null) return;

                // Extension uses `op`; CookieSheriff desktop uses `operation` internally.
                var op = GetString(dict, "op") ?? GetString(dict, "operation");
                if (string.Equals(op, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    // CookieSheriff-style pong response
                    var pong = new
                    {
                        id = Guid.NewGuid().ToString(),
                        type = "response",
                        op = "pong",
                        payload = new { requestId = GetString(dict, "id") },
                        ts = DateTime.UtcNow.ToString("o"),
                        v = "1.0"
                    };
                    _ = SendJsonAsync(socket, serializer.Serialize(pong));
                    return;
                }
                if (!string.Equals(op, "product.track", StringComparison.OrdinalIgnoreCase)) return;

                var payload = dict.ContainsKey("payload") ? dict["payload"] as Dictionary<string, object> : null;
                if (payload == null) return;

                var tracked = new TrackedProduct
                {
                    AddedAt = DateTime.Now,
                    Url = GetString(payload, "url"),
                    Title = GetString(payload, "title"),
                    Host = GetString(payload, "host"),
                    Price = GetString(payload, "price"),
                    Currency = GetString(payload, "currency")
                };

                if (string.IsNullOrWhiteSpace(tracked.Url)) return;

                ProductTracked?.Invoke(this, tracked);

                // Optional ack (doesn't break if extension ignores it)
                var ack = new
                {
                    id = Guid.NewGuid().ToString(),
                    type = "event",
                    op = "product.track.ack",
                    payload = new { url = tracked.Url, receivedAt = DateTime.UtcNow.ToString("o") },
                    ts = DateTime.UtcNow.ToString("o"),
                    v = "1.0"
                };
                _ = SendJsonAsync(socket, serializer.Serialize(ack));
            }
            catch
            {
                // ignore malformed messages
            }
        }

        private async Task SendJsonAsync(WebSocket socket, string json)
        {
            try
            {
                if (socket == null || socket.State != WebSocketState.Open) return;
                var bytes = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                // ignore
            }
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key) || dict[key] == null) return null;
            return dict[key].ToString();
        }

        public bool IsRunning => _isRunning;
        public int ConnectedClientsCount => Volatile.Read(ref _connectedClients);

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }
}

