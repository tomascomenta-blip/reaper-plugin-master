// =============================================================================
// Services/SecurityService.cs
// =============================================================================
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ReaperPluginManager.Models;
using Serilog;

namespace ReaperPluginManager.Services
{
    public interface ISecurityService
    {
        Task<SecurityResult> ScanFileAsync(
            string filePath,
            string? expectedSHA256 = null,
            IProgress<string>? progress = null,
            CancellationToken ct = default);
    }

    public class SecurityService : ISecurityService
    {
        private readonly ILogger _log;

        public SecurityService(ILogger logger)
        {
            _log = logger.ForContext<SecurityService>();
        }

        public async Task<SecurityResult> ScanFileAsync(
            string filePath,
            string? expectedSHA256 = null,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var result = new SecurityResult { ScannedAt = DateTime.UtcNow };

            // 1. Hash SHA-256
            progress?.Report("Calculando hash SHA-256...");
            result.ComputedSHA256 = await ComputeSHA256Async(filePath, ct);

            if (!string.IsNullOrWhiteSpace(expectedSHA256))
            {
                result.HashCheckPassed = string.Equals(
                    result.ComputedSHA256, expectedSHA256,
                    StringComparison.OrdinalIgnoreCase);

                if (!result.HashCheckPassed)
                {
                    result.Warnings.Add($"Hash mismatch: esperado {expectedSHA256}, obtenido {result.ComputedSHA256}");
                    _log.Warning("Hash SHA-256 no coincide para {File}", filePath);
                }
            }
            else
            {
                result.HashCheckPassed = true; // Sin hash esperado, no se puede verificar
            }

            // 2. Firma digital (Windows)
            progress?.Report("Verificando firma digital...");
            (result.SignatureValid, result.SignatureSubject) = CheckAuthenticode(filePath);

            // 3. Windows Defender
            progress?.Report("Escaneando con Windows Defender...");
            (result.DefenderThreatFound, result.DefenderOutput) =
                await RunDefenderScanAsync(filePath, ct);

            // Clasificación final
            if (result.DefenderThreatFound)
                result.Classification = SecurityClassification.Blocked;
            else if (!result.HashCheckPassed)
                result.Classification = SecurityClassification.Suspicious;
            else if (result.SignatureValid)
                result.Classification = SecurityClassification.Safe;
            else
                result.Classification = SecurityClassification.Unknown;

            _log.Information("Escaneo de seguridad completado: {File} → {Classification}",
                Path.GetFileName(filePath), result.Classification);

            return result;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private static async Task<string> ComputeSHA256Async(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, ct);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static (bool valid, string subject) CheckAuthenticode(string path)
        {
            try
            {
                var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
                return (true, cert.Subject);
            }
            catch
            {
                return (false, "Sin firma");
            }
        }

        private static async Task<(bool found, string output)> RunDefenderScanAsync(
            string filePath, CancellationToken ct)
        {
            // Buscar MpCmdRun.exe
            var mpCmdRun = FindDefenderPath();
            if (string.IsNullOrEmpty(mpCmdRun))
                return (false, "Windows Defender no encontrado");

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = mpCmdRun,
                    Arguments              = $"-Scan -ScanType 3 -File \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = System.Diagnostics.Process.Start(psi)!;
                var output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                bool threatFound = proc.ExitCode != 0 ||
                    output.Contains("threat", StringComparison.OrdinalIgnoreCase);

                return (threatFound, output.Trim());
            }
            catch (Exception ex)
            {
                return (false, $"Error al ejecutar Defender: {ex.Message}");
            }
        }

        private static string FindDefenderPath()
        {
            var candidates = new[]
            {
                @"C:\Program Files\Windows Defender\MpCmdRun.exe",
                @"C:\Program Files (x86)\Windows Defender\MpCmdRun.exe",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Windows Defender", "MpCmdRun.exe")
            };

            foreach (var p in candidates)
                if (File.Exists(p)) return p;

            return string.Empty;
        }
    }
}
