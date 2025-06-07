using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.Core.ModCompatibility
{
    public interface IModCompatibilityManager
    {
        bool IsInitialized { get; }
        
        event Action<ModInfo> OnModDetected;
        event Action<ModInfo> OnModRemoved;
        event Action<string> OnCompatibilityIssueDetected;
        event Action OnModAnalysisCompleted;
        
        void Initialize();
        void Shutdown();
        void ScanForMods();
        void AnalyzeModCompatibility();
        void RefreshModList();
        
        List<ModInfo> GetDetectedMods();
        List<ModInfo> GetCompatibleMods();
        List<ModInfo> GetIncompatibleMods();
        List<string> GetCompatibilityIssues();
        
        ModCompatibilityResult CheckModCompatibility(ModInfo modInfo);
        bool IsModSupported(string modId);
        bool IsModConflicting(string modId);
        
        void RegisterModTranslationPattern(string modId, ModTranslationPattern pattern);
        void UnregisterModTranslationPattern(string modId);
        ModTranslationPattern GetModTranslationPattern(string modId);
        
        void ApplyCompatibilityPatch(string modId);
        void RemoveCompatibilityPatch(string modId);
        bool HasCompatibilityPatch(string modId);
        
        Dictionary<string, object> GetCompatibilityStatistics();
        void GenerateCompatibilityReport();
    }
}