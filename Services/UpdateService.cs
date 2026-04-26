// =============================================================================
// Services/UpdateService.cs
// Sistema de auto-actualización de plugins:
//   - Consulta endpoint de versiones
//   - Compara versión instalada vs disponible
//   - Descarga delta si está disponible
//   - Rollback en caso de fallo
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ReaperPluginManager.Database;
using ReaperPluginManager.Models;
using Serilog;

namespace ReaperPluginManager.Services
{
    public interface IUpdateService
    {
        Task<IEnumerable<PluginUpdateInfo>> CheckForUpdatesAsync(
            CancellationToken ct = default);

        Task<bool> ApplyUpdateAsync(
            Plugin plugin,
            PluginUpdateInfo updateInfo,
            IProgress<string>? progress = null,
            CancellationToken ct = default);
    }

    public class PluginUpdateInfo
    {
        public Guid PluginId       { get; set; }
        public string PluginName   { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl  { get; set; } = string.Empty;
        public string SHA256       { get; set; } = string.Empty;
        public string Changelog    { get; set; } = string.Empty;
        public bool   IsSecurityUpdate { get; set; } = false;
    }

    /// <summary>
    /// Respuesta del servidor de actualizaciones (JSON)
    /// </summary>
    public class VersionManifest
    {
        [JsonProperty("plugins")]
        public List<PluginVersionEntry> Plugins { get; set; } = new();

        [JsonProperty("manifestVersion")]
        public string ManifestVersion { get; set; } = "1";

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }

    public class PluginVersionEntry
    {
        [JsonProperty("id")]    public string Id      { get; set; } = string.Empty;
        [JsonProperty("name")]  public string Name    { get; set; } = string.Empty;
        [JsonProperty("version")] public string Version { get; set; } = string.Empty;
        [JsonProperty("downloadUrl")] public string DownloadUrl { get; set; } = string.Empty;
        [JsonProperty("sha256")]  public string SHA256  { get; set; } = string.Empty;
        [JsonProperty("changelog")] public string Changelog { get; set; } = string.Empty;
        [JsonProperty("isSecurity")] public bool IsSecurity { get; set; }
    }

    public class UpdateService : IUpdateService
    {
        private readonly IPluginDatabase _db;
        private readonly IHttpClientFactory _http;
        private readonly IDownloadService _downloader;
        private readonly IInstallerService _installer;
        private readonly ISecurityService _security;
        private readonly ILogger _log;

        // En producción apuntar al servidor real
        private const string ManifestUrl = "https://api.reaperpluginmanager.dev/v1/manifest.json";
        private const int ManifestTimeoutSeconds = 30;

        public UpdateService(
            IPluginDatabase db,
            IHttpClientFactory http,
            IDownloadService downloader,
            IInstallerService installer,
            ISecurityService security,
            ILogger logger)
        {
            _db        = db;
            _http      = http;
            _downloader = downloader;
            _installer  = installer;
            _security   = security;
            _log        = logger.ForContext<UpdateService>();
        }

        public async Task<IEnumerable<PluginUpdateInfo>> CheckForUpdatesAsync(
            CancellationToken ct = default)
        {
            var updates = new List<PluginUpdateInfo>();

            try
            {
                _log.Information("Verificando actualizaciones...");

                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(ManifestTimeoutSeconds);

                var json = await client.GetStringAsync(ManifestUrl, ct);
                var manifest = JsonConvert.DeserializeObject<VersionManifest>(json);

                if (manifest?.Plugins == null)
                {
                    _log.Warning("Manifiesto de actualizaciones vacío o inválido.");
                    return updates;
                }

                var installedPlugins = _db.GetAllPlugins()
                    .Where(p => p.IsInstalled)
                    .ToList();

                foreach (var installed in installedPlugins)
                {
                    var remote = manifest.Plugins.FirstOrDefault(r =>
                        r.Id == installed.Id.ToString() ||
                        string.Equals(r.Name, installed.Name, StringComparison.OrdinalIgnoreCase));

                    if (remote == null) continue;

                    // Comparar versiones semánticas
                    if (IsNewerVersion(remote.Version, installed.Version))
                    {
                        updates.Add(new PluginUpdateInfo
                        {
                            PluginId       = installed.Id,
                            PluginName     = installed.Name,
                            CurrentVersion = installed.Version,
                            LatestVersion  = remote.Version,
                            DownloadUrl    = remote.DownloadUrl,
                            SHA256         = remote.SHA256,
                            Changelog      = remote.Changelog,
                            IsSecurityUpdate = remote.IsSecurity
                        });

                        // Marcar en la BD
                        installed.Status        = PluginStatus.UpdateAvailable;
                        installed.LatestVersion = remote.Version;
                        _db.UpsertPlugin(installed);

                        _log.Information("Actualización disponible: {Name} {Current} → {Latest}",
                            installed.Name, installed.Version, remote.Version);
                    }
                }

                _log.Information("{Count} actualizaciones encontradas.", updates.Count);
            }
            catch (HttpRequestException ex)
            {
                _log.Warning(ex, "No se pudo conectar al servidor de actualizaciones.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error verificando actualizaciones.");
            }

            return updates;
        }

        public async Task<bool> ApplyUpdateAsync(
            Plugin plugin,
            PluginUpdateInfo updateInfo,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            _log.Information("Aplicando actualización: {Name} → v{Version}",
                plugin.Name, updateInfo.LatestVersion);

            // Guardar ruta de instalación anterior para rollback
            var previousInstallPath = plugin.InstallPath;
            var previousVersion     = plugin.Version;

            try
            {
                progress?.Report($"Descargando {plugin.Name} v{updateInfo.LatestVersion}...");

                // Actualizar URL de descarga para la nueva versión
                plugin.DownloadUrl   = updateInfo.DownloadUrl;
                plugin.ExpectedSHA256 = updateInfo.SHA256;

                var downloadResult = await _downloader.DownloadPluginAsync(plugin, null, ct);
                if (!downloadResult.Success)
                {
                    _log.Error("Fallo descarga para actualización: {Error}", downloadResult.Error);
                    return false;
                }

                plugin.TempFilePath = downloadResult.FilePath;
                progress?.Report("Instalando actualización...");

                // Hacer backup del archivo anterior
                if (!string.IsNullOrEmpty(previousInstallPath) &&
                    System.IO.File.Exists(previousInstallPath))
                {
                    var backupPath = previousInstallPath + ".bak";
                    System.IO.File.Copy(previousInstallPath, backupPath, overwrite: true);
                }

                var installResult = await _installer.InstallPluginAsync(plugin, null, null, ct);

                if (installResult.Success)
                {
                    plugin.Version       = updateInfo.LatestVersion;
                    plugin.Status        = PluginStatus.Installed;
                    plugin.InstalledDate = DateTime.UtcNow;

                    // Agregar al historial de versiones
                    plugin.VersionHistory.Add(new PluginVersion
                    {
                        Version     = updateInfo.LatestVersion,
                        ReleaseDate = DateTime.UtcNow,
                        Changelog   = updateInfo.Changelog,
                        IsInstalled = true
                    });

                    _db.UpsertPlugin(plugin);
                    progress?.Report($"✅ {plugin.Name} actualizado a v{updateInfo.LatestVersion}");
                    _log.Information("Actualización exitosa: {Name} v{Version}", plugin.Name, plugin.Version);
                    return true;
                }
                else
                {
                    // Rollback al archivo backup
                    _log.Error("Fallo instalación de actualización. Haciendo rollback...");
                    TryRollback(previousInstallPath, plugin, previousVersion);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error aplicando actualización para {Plugin}", plugin.Name);
                TryRollback(previousInstallPath, plugin, previousVersion);
                return false;
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private static bool IsNewerVersion(string remote, string installed)
        {
            if (Version.TryParse(remote, out var rv) &&
                Version.TryParse(installed, out var iv))
                return rv > iv;

            // Fallback: comparación de string
            return string.Compare(remote, installed, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private void TryRollback(string? previousPath, Plugin plugin, string previousVersion)
        {
            try
            {
                if (!string.IsNullOrEmpty(previousPath))
                {
                    var backupPath = previousPath + ".bak";
                    if (System.IO.File.Exists(backupPath))
                    {
                        System.IO.File.Copy(backupPath, previousPath, overwrite: true);
                        System.IO.File.Delete(backupPath);
                        plugin.Version = previousVersion;
                        plugin.Status  = PluginStatus.Installed;
                        _db.UpsertPlugin(plugin);
                        _log.Information("Rollback exitoso a v{Version}", previousVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error en rollback para {Plugin}", plugin.Name);
            }
        }
    }
}
