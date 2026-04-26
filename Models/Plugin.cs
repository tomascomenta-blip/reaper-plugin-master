// =============================================================================
// Models/Plugin.cs
// =============================================================================
using System;
using System.Collections.Generic;
using LiteDB;

namespace ReaperPluginManager.Models
{
    public enum PluginFormat
    {
        Unknown,
        VST2,
        VST3,
        JSFX,
        Clap,
        AU
    }

    public enum PluginStatus
    {
        Pending,
        Downloading,
        Scanning,
        Testing,
        Installing,
        Installed,
        UpdateAvailable,
        Failed,
        Blocked
    }

    public enum SecurityClassification
    {
        Unknown,
        Safe,
        Suspicious,
        Blocked
    }

    public enum PluginArchitecture
    {
        Unknown,
        x86,
        x64,
        AnyCPU
    }

    public class Plugin
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name          { get; set; } = string.Empty;
        public string Developer     { get; set; } = string.Empty;
        public string Version       { get; set; } = "1.0.0";
        public string LatestVersion { get; set; } = string.Empty;
        public PluginFormat Format  { get; set; } = PluginFormat.VST3;
        public PluginArchitecture Architecture { get; set; } = PluginArchitecture.x64;
        public PluginStatus Status  { get; set; } = PluginStatus.Pending;

        public string DownloadUrl    { get; set; } = string.Empty;
        public string ExpectedSHA256 { get; set; } = string.Empty;
        public string InstallPath    { get; set; } = string.Empty;
        public string TempFilePath   { get; set; } = string.Empty;
        public string Description    { get; set; } = string.Empty;
        public string Category       { get; set; } = "Sin categoría";

        public List<string> Tags { get; set; } = new();

        public bool IsInstalled { get; set; }
        public bool IsFavorite  { get; set; }

        public int  UserRating    { get; set; }   // 0-5
        public long FileSizeBytes { get; set; }

        public DateTime  AddedDate     { get; set; } = DateTime.UtcNow;
        public DateTime? InstalledDate { get; set; }
        public DateTime? LastScanDate  { get; set; }

        public SecurityClassification SecurityStatus { get; set; } = SecurityClassification.Unknown;
        public SecurityResult?  LastSecurityResult  { get; set; }
        public SandboxResult?   LastSandboxResult   { get; set; }

        public List<PluginVersion> VersionHistory { get; set; } = new();

        public string Notes { get; set; } = string.Empty;
    }

    public class PluginVersion
    {
        public string   Version     { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string   Changelog   { get; set; } = string.Empty;
        public bool     IsInstalled { get; set; }
    }

    public class SandboxResult
    {
        public bool   Passed                { get; set; }
        public string Verdict               { get; set; } = string.Empty;
        public double PeakCpuUsagePercent   { get; set; }
        public long   PeakMemoryUsageMB     { get; set; }
        public bool   TriedNetworkAccess    { get; set; }
        public bool   TriedFileSystem       { get; set; }
        public string RawOutput             { get; set; } = string.Empty;
        public DateTime TestedAt            { get; set; } = DateTime.UtcNow;

        public string GetVerdictDisplay =>
            Passed ? "✅ Sandbox: OK" : $"⚠️ Sandbox: {Verdict}";
    }

    public class PluginCategory
    {
        [BsonId]
        public string Name  { get; set; } = string.Empty;
        public string Color { get; set; } = "#607D8B";
        public int PluginCount { get; set; }
    }
}
