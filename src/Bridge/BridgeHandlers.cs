using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AQRLiteHelper.Bridge
{
    internal sealed class BridgeHandlers
    {
        private readonly BridgeRequestQueue _queue;
        private int _busy;

        public BridgeHandlers(BridgeRequestQueue queue)
        {
            _queue = queue;
        }

        public bool IsBusy => Volatile.Read(ref _busy) == 1;

        public HealthResponse BuildHealth(int port)
        {
            var scene = string.Empty;
            var editorReady = false;

            try
            {
                scene = ADOBase.sceneName ?? string.Empty;
                editorReady = ADOBase.isLevelEditor;
            }
            catch
            {
                // The game may still be initializing its first scene.
            }

            return new HealthResponse
            {
                connected = true,
                modVersion = Main.Mod?.Info.Version ?? "unknown",
                port = port,
                scene = scene,
                editorReady = editorReady,
                busy = IsBusy
            };
        }

        public async Task<BridgeResult> HandleOpenLevelAsync(string body)
        {
            if (!BridgeJson.TryDeserializeOpenLevel(body, out var request) || request == null)
            {
                return BridgeResult.BadRequest("INVALID_REQUEST", "请求格式无效。");
            }

            if (request.levelId <= 0 || string.IsNullOrWhiteSpace(request.downloadUrl))
            {
                return BridgeResult.BadRequest("INVALID_REQUEST", "关卡 ID 或下载地址无效。");
            }

            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                return BridgeResult.Conflict("BUSY", "已有谱面正在下载或打开，请稍后重试。");
            }

            try
            {
                if (!Main.Settings.TryPrepareDownloadDirectory(out var directory))
                {
                    return BridgeResult.BadRequest(
                        "DOWNLOAD_DIRECTORY_INVALID",
                        "谱面保存目录不可用，请在 Mod 设置中重新选择。");
                }

                var fileName = string.IsNullOrWhiteSpace(request.fileName)
                    ? $"level_{request.levelId}.zip"
                    : request.fileName;
                var download = await AssetDownloader.DownloadAsync(
                    request.downloadUrl,
                    directory,
                    fileName,
                    CancellationToken.None).ConfigureAwait(false);
                if (!download.Success)
                {
                    Main.Mod?.Logger.Error($"关卡 {request.levelId} 下载失败：{download.Message}");
                    return BridgeResult.Error("DOWNLOAD_FAILED", $"谱面下载失败：{download.Message}");
                }

                var result = await _queue.Enqueue(() => OpenLevel(download.Path, request.levelId)).ConfigureAwait(false);
                return new BridgeResult(200, result);
            }
            catch (Exception ex)
            {
                Main.Mod?.Logger.Error($"关卡 {request.levelId} 打开失败：{ex}");
                return BridgeResult.Error("OPEN_FAILED", "谱面打开失败，请查看游戏日志。");
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        }

        public static void WriteJson(HttpListenerResponse response, int statusCode, string json, string? origin)
        {
            var buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            ApplyCors(response, origin);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteOptions(HttpListenerResponse response, string? origin)
        {
            response.StatusCode = 204;
            ApplyCors(response, origin);
            response.ContentLength64 = 0;
        }

        private static BridgeResponse OpenLevel(string path, int levelId)
        {
            if (ADOBase.isLevelEditor && ADOBase.editor != null)
            {
                ADOBase.editor.CheckUnsavedChanges(() => ADOBase.editor.OpenLevel(path));
                Main.Mod?.Logger.Log($"已请求在编辑器中打开关卡 {levelId}：{path}");
                return BridgeResponse.Ok("谱面已下载，正在等待游戏确认并打开。", "opening");
            }

            if (ADOBase.loader == null)
            {
                return BridgeResponse.Fail("GAME_NOT_READY", "游戏场景尚未准备好，请稍后重试。");
            }

            scnEditor.levelToOpenOnLoad = path;
            ADOBase.loader.LoadSceneWithTransition(WipeDirection.StartsFromRight, "scnEditor");
            Main.Mod?.Logger.Log($"已请求从当前场景切换到编辑器并打开关卡 {levelId}：{path}");
            return BridgeResponse.Ok("谱面已下载，正在切换到编辑器。", "opening");
        }

        private static void ApplyCors(HttpListenerResponse response, string? origin)
        {
            if (!IsAllowedOrigin(origin))
            {
                return;
            }

            response.Headers["Access-Control-Allow-Origin"] = origin;
            response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            response.Headers["Access-Control-Allow-Private-Network"] = "true";
            response.Headers["Access-Control-Max-Age"] = "86400";
        }

        private static bool IsAllowedOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return false;
            }

            var allowedOrigins = new[]
            {
                "https://adofaiaqr.cn",
                "https://www.adofaiaqr.cn",
                "http://localhost:3000",
                "http://127.0.0.1:3000",
                "https://localhost:3000",
                "https://127.0.0.1:3000"
            };

            foreach (var allowedOrigin in allowedOrigins)
            {
                if (origin.Equals(allowedOrigin, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
