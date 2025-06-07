using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.Core.ModCompatibility
{
    public class ModCompatibilityResult
    {
        public string ModId { get; set; }
        public ModCompatibilityLevel CompatibilityLevel { get; set; }
        public bool IsSupported { get; set; }
        public bool RequiresPatch { get; set; }
        public bool HasConflicts { get; set; }
        
        public List<string> Issues { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> Recommendations { get; set; }
        public List<string> ConflictingMods { get; set; }
        public List<string> RequiredPatches { get; set; }
        
        public Dictionary<string, object> AnalysisData { get; set; }
        public DateTime AnalysisDate { get; set; }
        public TimeSpan AnalysisDuration { get; set; }
        
        public ModCompatibilityResult()
        {
            Issues = new List<string>();
            Warnings = new List<string>();
            Recommendations = new List<string>();
            ConflictingMods = new List<string>();
            RequiredPatches = new List<string>();
            AnalysisData = new Dictionary<string, object>();
            AnalysisDate = DateTime.Now;
        }
        
        public void AddIssue(string issue)
        {
            if (!string.IsNullOrEmpty(issue) && !Issues.Contains(issue))
            {
                Issues.Add(issue);
            }
        }
        
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning) && !Warnings.Contains(warning))
            {
                Warnings.Add(warning);
            }
        }
        
        public void AddRecommendation(string recommendation)
        {
            if (!string.IsNullOrEmpty(recommendation) && !Recommendations.Contains(recommendation))
            {
                Recommendations.Add(recommendation);
            }
        }
        
        public void AddConflictingMod(string modId)
        {
            if (!string.IsNullOrEmpty(modId) && !ConflictingMods.Contains(modId))
            {
                ConflictingMods.Add(modId);
                HasConflicts = true;
            }
        }
        
        public void AddRequiredPatch(string patchName)
        {
            if (!string.IsNullOrEmpty(patchName) && !RequiredPatches.Contains(patchName))
            {
                RequiredPatches.Add(patchName);
                RequiresPatch = true;
            }
        }
        
        public bool HasCriticalIssues => Issues.Count > 0 && CompatibilityLevel == ModCompatibilityLevel.Incompatible;
        public bool HasMinorIssues => Warnings.Count > 0;
        public int TotalIssueCount => Issues.Count + Warnings.Count;
        
        public string GetSummary()
        {
            var status = CompatibilityLevel switch
            {
                ModCompatibilityLevel.FullyCompatible => "완전 호환",
                ModCompatibilityLevel.PartiallyCompatible => "부분 호환",
                ModCompatibilityLevel.RequiresPatch => "패치 필요",
                ModCompatibilityLevel.Incompatible => "비호환",
                ModCompatibilityLevel.Conflicting => "충돌",
                _ => "알 수 없음"
            };
            
            return $"모드 호환성: {status} (이슈: {Issues.Count}, 경고: {Warnings.Count})";
        }
    }
}