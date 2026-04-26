// =============================================================================
// Services/SandboxService.cs
// =============================================================================
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReaperPluginManager.Models;
using Serilog;

namespace ReaperPluginManager.Services
{
    public interface ISandboxService
    {
        Task<SandboxResult> TestPluginAsync(
            Plugin plugin,
            IProgress<string>? progress = null,
            CancellationToken ct = default);
    }

    public class SandboxService : ISandboxService
    {
        private readonly ILogger _log;
        private readonly string  _vstHostPath;

        public SandboxService(ILogger logger)
        {
            _log = logger.ForContext<SandboxService>();
            _vstHostPath = GetVstHostPath();
        }

        public async Task<SandboxResult> TestPluginAsync(
            Plugin plugin,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var result = new SandboxResult { TestedAt = DateTime.UtcNow };

            // Solo aplicable a VST2/VST3
            if (plugin.Format != PluginFormat.VST2 && plugin.Format != PluginFormat.VST3)
            {
                result.Passed  = true;
                result.Verdict = "No aplica (JSFX/otros)";
                return result;
            }

            if (!File.Exists(_vstHostPath))
            {
                result.Passed  = true;
                result.Verdict = "VSTHost no disponible – omitiendo sandbox";
                _log.Warning("PluginTestHost no encontrado en {Path}", _vstHostPath);
                return result;
            }

            progress?.Report("Iniciando sandbox...");

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(45));

                var psi = new ProcessStartInfo
                {
                    FileName               = _vstHostPath,
                    Arguments              = BuildArgs(plugin),
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc   = Process.Start(psi)!;
                var outputTask   = proc.StandardOutput.ReadToEndAsync(cts.Token);
                await proc.WaitForExitAsync(cts.Token);

                var rawOutput          = await outputTask;
                result.RawOutput       = rawOutput;

                ParseHostOutput(rawOutput, result);

                result.Passed = proc.ExitCode == 0 && !result.TriedNetworkAccess;
                result.Verdict = result.Passed
                    ? "Plugin cargó correctamente"
                    : $"Salida con código {proc.ExitCode}";

                _log.Information("Sandbox completado: {Name} → Passed={Passed}", plugin.Name, result.Passed);
            }
            catch (OperationCanceledException)
            {
                result.Passed  = false;
                result.Verdict = "Timeout del sandbox";
                _log.Warning("Sandbox timeout para {Name}", plugin.Name);
            }
            catch (Exception ex)
            {
                result.Passed  = false;
                result.Verdict = $"Error sandbox: {ex.Message}";
                _log.Error(ex, "Error en sandbox para {Name}", plugin.Name);
            }

            return result;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private static string BuildArgs(Plugin plugin)
        {
            var pluginPath = plugin.TempFilePath ?? plugin.InstallPath ?? string.Empty;
            var tempDir    = Path.GetTempPath();
            return $"--plugin \"{pluginPath}\" --format {plugin.Format} --temp \"{tempDir}\" --timeout 30";
        }

        private static void ParseHostOutput(string output, SandboxResult result)
        {
            if (string.IsNullOrEmpty(output)) return;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                if (l == "LOAD_OK")
                    result.Verdict = "Carga OK";
                else if (l.StartsWith("LOAD_FAIL:"))
                    result.Verdict = l[10..];
                else if (l.StartsWith("CRASH:"))
                    result.Verdict = l[6..];
                else if (l.StartsWith("CPU:") && double.TryParse(l[4..], out var cpu))
                    result.PeakCpuUsagePercent = Math.Max(result.PeakCpuUsagePercent, cpu);
                else if (l.StartsWith("MEM:") && long.TryParse(l[4..], out var mem))
                    result.PeakMemoryUsageMB = Math.Max(result.PeakMemoryUsageMB, mem);
                else if (l == "NET_ACCESS")
                    result.TriedNetworkAccess = true;
            }
        }

        private static string GetVstHostPath()
        {
            var exe = AppContext.BaseDirectory;
            return Path.Combine(exe, "VSTHost", "PluginTestHost.exe");
        }
    }
}
