// =============================================================================
// Services/DownloadService.cs
// =============================================================================
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ReaperPluginManager.Models;
using Serilog;

namespace ReaperPluginManager.Services
{
    public interface IDownloadService
    {
        Task<DownloadResult> DownloadPluginAsync(
            Plugin plugin,
            IProgress<double>? progress = null,
            CancellationToken ct = default);
    }

    public class DownloadResult
    {
        public bool   Success  { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Error    { get; set; } = string.Empty;
        public long   Bytes    { get; set; }
    }

    public class DownloadService : IDownloadService
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger _log;
        private readonly string _downloadDir;

        public DownloadService(IHttpClientFactory httpFactory, ILogger logger)
        {
            _http = httpFactory;
            _log  = logger.ForContext<DownloadService>();

            var appData  = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _downloadDir = Path.Combine(appData, "ReaperPluginManager", "Downloads");
            Directory.CreateDirectory(_downloadDir);
        }

        public async Task<DownloadResult> DownloadPluginAsync(
            Plugin plugin,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            var url = plugin.DownloadUrl;

            // Soporte para archivos locales (file:///)
            if (url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = new Uri(url).LocalPath;
                if (!File.Exists(localPath))
                    return new DownloadResult { Error = $"Archivo local no encontrado: {localPath}" };

                return new DownloadResult
                {
                    Success  = true,
                    FilePath = localPath,
                    Bytes    = new FileInfo(localPath).Length
                };
            }

            // Descarga HTTP/HTTPS
            try
            {
                _log.Information("Descargando {Name} desde {Url}", plugin.Name, url);

                var client   = _http.CreateClient("PluginDownloader");
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var fileName   = GetFileName(plugin, url);
                var destPath   = Path.Combine(_downloadDir, fileName);

                await using var src  = await response.Content.ReadAsStreamAsync(ct);
                await using var dest = File.Create(destPath);

                var buffer    = new byte[81920];
                long bytesRead = 0;
                int  read;

                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read), ct);
                    bytesRead += read;

                    if (totalBytes > 0)
                        progress?.Report((double)bytesRead / totalBytes * 100.0);
                }

                _log.Information("Descarga completada: {File} ({Bytes} bytes)", destPath, bytesRead);
                return new DownloadResult { Success = true, FilePath = destPath, Bytes = bytesRead };
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error descargando {Name}", plugin.Name);
                return new DownloadResult { Error = ex.Message };
            }
        }

        private static string GetFileName(Plugin plugin, string url)
        {
            try
            {
                var uri  = new Uri(url);
                var name = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }

            var safe = string.Concat(plugin.Name.Split(Path.GetInvalidFileNameChars()));
            return $"{safe}_{plugin.Format}_{Guid.NewGuid():N}.tmp";
        }
    }
}
