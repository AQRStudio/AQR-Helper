using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// OAuth2 回调服务器，接收授权码
/// </summary>
public class OAuth2CallbackServer : MonoBehaviour
{
    private HttpListener listener;
    private bool isListening = false;
    private Action<string, string> onCallbackReceived;

    public void StartServer(int port, Action<string, string> callback)
    {
        onCallbackReceived = callback;
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        isListening = true;

        Task.Run(async () =>
        {
            while (isListening && listener != null && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;
                    var query = request.QueryString;

                    if (query["code"] != null)
                    {
                        string code = query["code"];
                        string state = query["state"] ?? "";

                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            onCallbackReceived?.Invoke(code, state);
                        });

                        string html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body><h1>授权成功！</h1><p>您可以关闭此窗口。</p></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 200;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        string error = query["error"] ?? "unknown_error";
                        string errorDesc = query["error_description"] ?? "";
                        string html = $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body><h1>授权失败</h1><p>{error}: {errorDesc}</p></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 400;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    response.Close();
                }
                catch (ObjectDisposedException) { }
                catch (HttpListenerException) { }
                catch (Exception ex)
                {
                    Debug.LogError($"[OAuth2] 回调服务器错误: {ex.Message}");
                }
            }
        });
    }

    public void StopServer()
    {
        isListening = false;
        try
        {
            listener?.Stop();
            listener?.Close();
        }
        catch { }
        listener = null;
    }

    /// <summary>
    /// OAuth2 回调端口，需与 OAuth 服务端注册的 redirect_uri 匹配（如 http://localhost:38473/callback）
    /// </summary>
    public const int CallbackPort = 38473;

    void OnDestroy()
    {
        StopServer();
    }
}
