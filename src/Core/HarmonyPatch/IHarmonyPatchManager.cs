using System;
using System.Collections.Generic;
using System.Reflection;

namespace GoLani.SPTModTranslator.Core.HarmonyPatch
{
    public interface IHarmonyPatchManager
    {
        void ApplyPatches();
        void RemovePatches();
        void ReapplyPatches();
        
        void RegisterPatch(string patchId, Type patchClass);
        void UnregisterPatch(string patchId);
        bool IsPatchApplied(string patchId);
        
        void EnablePatch(string patchId);
        void DisablePatch(string patchId);
        
        List<string> GetAppliedPatches();
        Dictionary<string, PatchInfo> GetPatchStatistics();
        
        event Action<string> OnPatchApplied;
        event Action<string> OnPatchRemoved;
        event Action<string, Exception> OnPatchError;
    }

    public class PatchInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Type PatchClass { get; set; }
        public MethodInfo TargetMethod { get; set; }
        public bool IsApplied { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime AppliedAt { get; set; }
        public int HookCount { get; set; }
        public Exception LastError { get; set; }
    }
}