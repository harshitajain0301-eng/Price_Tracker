using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PriceDropCatcher.Communication
{
    public class HttpDiscoveryServer : IDisposable
    {
        private readonly int _port;
        private readonly int _wsPort;
        private readonly string _protoVersion;
        private HttpListener _httpListener;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public HttpDiscoveryServer(int port, int wsPort, string protoVersion = "1.0")
        {
            _port = port;
            _wsPort = wsPort;
            _protoVersion = protoVersion;
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
                    _ = Task.Run(() => HandleRequestAsync(context), _cts.Token);
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

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                if (request.HttpMethod == "GET" && request.Url != null && request.Url.AbsolutePath == "/bridge-info")
                {
                    var payload = new
                    {
                        wsPort = _wsPort,
                        protoVersion = _protoVersion,
                        status = "running",
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    };

                    var json = new JavaScriptSerializer().Serialize(payload);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    response.ContentType = "application/json";
                    response.ContentLength64 = bytes.Length;
                    response.StatusCode = 200;

                    await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    response.Close();
                    return;
                }

                response.StatusCode = 404;
                response.Close();
            }
            catch
            {
                try
                {
                    response.StatusCode = 500;
                    response.Close();
                }
                catch
                {
                    // ignore
                }
            }
        }

        public bool IsRunning => _isRunning;

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }
}

