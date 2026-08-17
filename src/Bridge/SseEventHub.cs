using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AQRLiteHelper.Bridge
{
    /// <summary>
    /// Maintains the single local SSE stream used by the website.
    /// Every event follows the SSE wire format: event, optional id, one or
    /// more data lines, and a blank line that terminates the event.
    /// </summary>
    internal sealed class SseEventHub : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<SseClient> _clients = new List<SseClient>();
        private readonly System.Threading.Timer _keepAliveTimer;
        private bool _stopped;

        public SseEventHub()
        {
            _keepAliveTimer = new System.Threading.Timer(
                SendKeepAlive,
                null,
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(15));
        }

        public async Task HandleAsync(HttpListenerContext context, string? origin)
        {
            var response = context.Response;
            var client = new SseClient(response);

            response.StatusCode = 200;
            response.ContentType = "text/event-stream; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.SendChunked = true;
            response.KeepAlive = true;
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";
            BridgeHandlers.ApplyCors(response, origin);

            lock (_sync)
            {
                if (_stopped)
                {
                    client.Close();
                    return;
                }

                _clients.Add(client);
            }

            try
            {
                if (!client.TrySend("ready", string.Empty, "{\"state\":\"ready\"}"))
                {
                    return;
                }

                await client.Completion.ConfigureAwait(false);
            }
            finally
            {
                Remove(client);
                client.Close();
            }
        }

        public void Publish(string eventName, string eventId, string data)
        {
            SseClient[] clients;
            lock (_sync)
            {
                if (_stopped)
                {
                    return;
                }

                clients = _clients.ToArray();
            }

            foreach (var client in clients)
            {
                if (!client.TrySend(eventName, eventId, data))
                {
                    Remove(client);
                }
            }
        }

        public void Dispose()
        {
            SseClient[] clients;
            lock (_sync)
            {
                if (_stopped)
                {
                    return;
                }

                _stopped = true;
                clients = _clients.ToArray();
                _clients.Clear();
            }

            _keepAliveTimer.Dispose();
            foreach (var client in clients)
            {
                client.Close();
            }
        }

        private void SendKeepAlive(object? state)
        {
            SseClient[] clients;
            lock (_sync)
            {
                if (_stopped)
                {
                    return;
                }

                clients = _clients.ToArray();
            }

            foreach (var client in clients)
            {
                if (!client.TrySendComment("keep-alive"))
                {
                    Remove(client);
                }
            }
        }

        private void Remove(SseClient client)
        {
            lock (_sync)
            {
                _clients.Remove(client);
            }
        }

        private sealed class SseClient
        {
            private readonly object _sync = new object();
            private readonly HttpListenerResponse _response;
            private readonly TaskCompletionSource<bool> _completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _closed;

            public SseClient(HttpListenerResponse response)
            {
                _response = response;
            }

            public Task Completion => _completion.Task;

            public bool TrySend(string eventName, string eventId, string data)
            {
                var builder = new StringBuilder();
                builder.Append("event: ").Append(eventName).Append('\n');
                if (!string.IsNullOrEmpty(eventId))
                {
                    builder.Append("id: ").Append(eventId).Append('\n');
                }

                var normalizedData = (data ?? string.Empty).Replace("\r", string.Empty);
                var dataLines = normalizedData.Split('\n');
                foreach (var line in dataLines)
                {
                    builder.Append("data: ").Append(line).Append('\n');
                }

                builder.Append('\n');
                return TryWrite(builder.ToString());
            }

            public bool TrySendComment(string comment)
            {
                return TryWrite($": {comment}\n\n");
            }

            public void Close()
            {
                lock (_sync)
                {
                    if (_closed)
                    {
                        return;
                    }

                    _closed = true;
                    _completion.TrySetResult(true);
                    try
                    {
                        _response.OutputStream.Close();
                    }
                    catch
                    {
                        // The browser may have closed the connection already.
                    }
                }
            }

            private bool TryWrite(string text)
            {
                lock (_sync)
                {
                    if (_closed)
                    {
                        return false;
                    }

                    try
                    {
                        var bytes = Encoding.UTF8.GetBytes(text);
                        _response.OutputStream.Write(bytes, 0, bytes.Length);
                        _response.OutputStream.Flush();
                        return true;
                    }
                    catch (IOException)
                    {
                        _closed = true;
                        _completion.TrySetResult(false);
                        return false;
                    }
                    catch (ObjectDisposedException)
                    {
                        _closed = true;
                        _completion.TrySetResult(false);
                        return false;
                    }
                    catch (Exception)
                    {
                        _closed = true;
                        _completion.TrySetResult(false);
                        return false;
                    }
                }
            }
        }
    }
}
