// =============================================================================
// Services/InstallerService.cs
// =============================================================================
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReaperPluginManager.Models;
using Serilog;
using SharpCompress.Archives;

namespace ReaperPluginManager.Services
{
    public interface IInstallerService
    {
        Task<InstallResult> InstallPluginAsync(
            Plugin plugin,
            InstallPaths? paths = null,
            IProgress<string>? progress = null,
            CancellationToken ct = default);

        Task<bool> UninstallPluginAsync(Plugin plugin, CancellationToken ct = default);
        InstallPaths GetDefaultInstallPaths();
    }

    public class InstallResult
    {
        public bool   Success     { get; set; }
        public string InstallPath { get; set; } = string.Empty;
        public string Error       { get; set; } = string.Empty;
    }

    public class InstallPaths
    {
        public string VST2Path   { get; set; } = string.Empty;
        public string VST3Path   { get; set; } = string.Empty;
        public string JSFXPath   { get; set; } = string.Empty;
        public string ReaperPath { get; set; } = string.Empty;
    }

    public class InstallerService : IInstallerService
    {
        private readonly ILogger _log;

        public InstallerService(ILogger logger)
        {
            _log = logger.ForContext<InstallerService>();
        }

        public InstallPaths GetDefaultInstallPaths()
        {
            var pf     = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var docs   = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            return new InstallPaths
            {
                VST2Path   = Path.Combine(pf, "VSTPlugins"),
                VST3Path   = Path.Combine(common, "VST3"),
                JSFXPath   = Path.Combine(docs, "REAPER", "Effects"),
                ReaperPath = Path.Combine(pf, "REAPER", "reaper.exe")
            };
        }

        public async Task<InstallResult> InstallPluginAsync(
            Plugin plugin,
            InstallPaths? paths = null,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            paths ??= GetDefaultInstallPaths();
            var srcFile = plugin.TempFilePath;

            if (string.IsNullOrEmpty(srcFile) || !File.Exists(srcFile))
                return new InstallResult { Error = $"Archivo temporal no encontrado: {srcFile}" };

            try
            {
                var destDir  = GetDestinationDir(plugin.Format, paths);
                Directory.CreateDirectory(destDir);

                var ext = Path.GetExtension(srcFile).ToLowerInvariant();

                if (ext == ".zip" || ext == ".rar" || ext == ".7z")
                {
                    progress?.Report($"Extrayendo {Path.GetFileName(srcFile)}...");
                    await Task.Run(() => ExtractArchive(srcFile, destDir), ct);

                    // Buscar el archivo de plugin dentro del directorio extraído
                    var installed = FindPluginFile(destDir, plugin.Format);
                    plugin.InstallPath = installed ?? destDir;
                }
                else if (ext == ".exe" || ext == ".msi")
                {
                    progress?.Report("Ejecutando instalador...");
                    await RunInstallerAsync(srcFile, ct);
                    plugin.InstallPath = destDir;
                }
                else
                {
                    // Copia directa (.dll, .vst3, .jsfx, .clap)
                    var destFile = Path.Combine(destDir, Path.GetFileName(srcFile));
                    progress?.Report($"Copiando a {destFile}...");
                    await Task.Run(() => File.Copy(srcFile, destFile, overwrite: true), ct);
                    plugin.InstallPath = destFile;
                }

                _log.Information("Plugin instalado: {Name} → {Path}", plugin.Name, plugin.InstallPath);
                return new InstallResult { Success = true, InstallPath = plugin.InstallPath };
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error instalando {Name}", plugin.Name);
                return new InstallResult { Error = ex.Message };
            }
        }

        public async Task<bool> UninstallPluginAsync(Plugin plugin, CancellationToken ct = default)
        {
            try
            {
                if (!string.IsNullOrEmpty(plugin.InstallPath) && File.Exists(plugin.InstallPath))
                {
                    await Task.Run(() => File.Delete(plugin.InstallPath), ct);
                    _log.Information("Plugin desinstalado: {Name}", plugin.Name);
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error desinstalando {Name}", plugin.Name);
                return false;
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private static string GetDestinationDir(PluginFormat format, InstallPaths paths) =>
            format switch
            {
                PluginFormat.VST3 => paths.VST3Path,
                PluginFormat.JSFX => paths.JSFXPath,
                _                 => paths.VST2Path
            };

        private static void ExtractArchive(string archivePath, string destDir)
        {
            using var archive = ArchiveFactory.Open(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                    entry.WriteToDirectory(destDir,
                        new SharpCompress.Common.ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite       = true
                        });
            }
        }

        private static async Task RunInstallerAsync(string installerPath, CancellationToken ct)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName  = installerPath,
                Arguments = "/S /SILENT /VERYSILENT /NORESTART",
                UseShellExecute = true
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            await proc.WaitForExitAsync(ct);
        }

        private static string? FindPluginFile(string dir, PluginFormat format)
        {
            var ext = format switch
            {
                PluginFormat.VST3 => "*.vst3",
                PluginFormat.JSFX => "*.jsfx",
                _                 => "*.dll"
            };
            var files = Directory.GetFiles(dir, ext, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }
    }
}
