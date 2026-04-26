// =============================================================================
// Models/SecurityResult.cs
// =============================================================================
using System;
using System.Collections.Generic;

namespace ReaperPluginManager.Models
{
    public class SecurityResult
    {
        public bool   HashCheckPassed    { get; set; }
        public string ComputedSHA256     { get; set; } = string.Empty;
        public bool   SignatureValid     { get; set; }
        public string SignatureSubject   { get; set; } = string.Empty;
        public bool   DefenderThreatFound { get; set; }
        public string DefenderOutput     { get; set; } = string.Empty;
        public SecurityClassification Classification { get; set; } = SecurityClassification.Unknown;
        public List<string> Warnings     { get; set; } = new();
        public DateTime ScannedAt        { get; set; } = DateTime.UtcNow;

        public bool OverallPassed =>
            HashCheckPassed &&
            !DefenderThreatFound &&
            Classification != SecurityClassification.Blocked;
    }
}
