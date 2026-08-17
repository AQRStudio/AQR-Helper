using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AQRLiteHelper.Bridge
{
    internal static class AssetDownloader
    {
        private static readonly HttpClient Client = CreateClient();

        public static async Task<(bool Success, string Path, string Message)> DownloadAsync(
            string url,
            string directory,
            string fileName,
            CancellationToken cancellationToken,
            Action<long, long>? progress = null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return (false, string.Empty, "下载地址必须使用 HTTPS。");
            }

            var safeFileName = NormalizeFileName(fileName);
            var targetPath = Path.Combine(directory, safeFileName);
            var temporaryPath = targetPath + $".{Guid.NewGuid():N}.part";

            try
            {
                Directory.CreateDirectory(directory);
                using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                progress?.Invoke(0L, totalBytes);

                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    var buffer = new byte[81920];
                    long receivedBytes = 0;
                    int bytesRead;
                    while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        receivedBytes += bytesRead;
                        progress?.Invoke(receivedBytes, totalBytes);
                    }

                    progress?.Invoke(receivedBytes, totalBytes);
                }

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(temporaryPath, targetPath);
                return (true, targetPath, string.Empty);
            }
            catch (Exception ex)
            {
                TryDelete(temporaryPath);
                return (false, string.Empty, ex.Message);
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AQR-Helper/1.0");
            return client;
        }

        private static string NormalizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName?.Trim());
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "level.zip";
            }

            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            var extension = Path.GetExtension(name);
            if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".adozip", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".adofai", StringComparison.OrdinalIgnoreCase))
            {
                name += ".zip";
            }

            return name;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
