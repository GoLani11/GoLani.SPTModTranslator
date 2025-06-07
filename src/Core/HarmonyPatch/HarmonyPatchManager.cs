using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using GoLani.SPTModTranslator.Core.TranslationManager;
using GoLani.SPTModTranslator.Utils.Logging;

namespace GoLani.SPTModTranslator.Core.HarmonyPatch
{
    public class HarmonyPatchManager : IHarmonyPatchManager
    {
        private readonly ITranslationManager _translationManager;
        private readonly ILogManager _logManager;
        private readonly Dictionary<string, PatchInfo> _registeredPatches;
        private readonly Dictionary<string, PatchInfo> _appliedPatches;
        private Harmony _harmonyInstance;

        public event Action<string> OnPatchApplied;
        public event Action<string> OnPatchRemoved;
        public event Action<string, Exception> OnPatchError;

        public HarmonyPatchManager(ITranslationManager translationManager, ILogManager logManager)
        {
            _translationManager = translationManager ?? throw new ArgumentNullException(nameof(translationManager));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _registeredPatches = new Dictionary<string, PatchInfo>();
            _appliedPatches = new Dictionary<string, PatchInfo>();
        }

        public void ApplyPatches()
        {
            try
            {
                _logManager.SetContext("HarmonyPatchManager");
                _logManager.LogInfo("Harmony 패치 적용 시작...");

                InitializeHarmony();
                RegisterBuiltInPatches();
                ApplyRegisteredPatches();

                _logManager.LogInfo($"총 {_appliedPatches.Count}개의 패치가 적용되었습니다.");
            }
            catch (Exception ex)
            {
                _logManager.LogError("Harmony 패치 적용 중 오류 발생", ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void RemovePatches()
        {
            try
            {
                _logManager.SetContext("HarmonyPatchManager");
                _logManager.LogInfo("Harmony 패치 제거 중...");

                if (_harmonyInstance != null)
                {
                    _harmonyInstance.UnpatchAll(_harmonyInstance.Id);
                    
                    foreach (var patch in _appliedPatches.Values.ToList())
                    {
                        RemovePatchInternal(patch.Id);
                    }
                }

                _logManager.LogInfo("모든 Harmony 패치가 제거되었습니다.");
            }
            catch (Exception ex)
            {
                _logManager.LogError("Harmony 패치 제거 중 오류 발생", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void ReapplyPatches()
        {
            _logManager.LogInfo("Harmony 패치 재적용 중...");
            RemovePatches();
            ApplyPatches();
        }

        public void RegisterPatch(string patchId, Type patchClass)
        {
            if (string.IsNullOrEmpty(patchId))
                throw new ArgumentException("패치 ID는 null이거나 빈 문자열일 수 없습니다.", nameof(patchId));
            
            if (patchClass == null)
                throw new ArgumentNullException(nameof(patchClass));

            var patchInfo = new PatchInfo
            {
                Id = patchId,
                Name = patchClass.Name,
                PatchClass = patchClass,
                IsApplied = false,
                IsEnabled = true
            };

            _registeredPatches[patchId] = patchInfo;
            _logManager.LogDebug($"패치 등록됨: {patchId} ({patchClass.Name})");
        }

        public void UnregisterPatch(string patchId)
        {
            if (_registeredPatches.ContainsKey(patchId))
            {
                if (_appliedPatches.ContainsKey(patchId))
                {
                    RemovePatchInternal(patchId);
                }
                
                _registeredPatches.Remove(patchId);
                _logManager.LogDebug($"패치 등록 해제됨: {patchId}");
            }
        }

        public bool IsPatchApplied(string patchId)
        {
            return _appliedPatches.ContainsKey(patchId) && _appliedPatches[patchId].IsApplied;
        }

        public void EnablePatch(string patchId)
        {
            if (_registeredPatches.ContainsKey(patchId))
            {
                _registeredPatches[patchId].IsEnabled = true;
                if (!_appliedPatches.ContainsKey(patchId))
                {
                    ApplyPatchInternal(_registeredPatches[patchId]);
                }
            }
        }

        public void DisablePatch(string patchId)
        {
            if (_registeredPatches.ContainsKey(patchId))
            {
                _registeredPatches[patchId].IsEnabled = false;
                if (_appliedPatches.ContainsKey(patchId))
                {
                    RemovePatchInternal(patchId);
                }
            }
        }

        public List<string> GetAppliedPatches()
        {
            return _appliedPatches.Keys.ToList();
        }

        public Dictionary<string, PatchInfo> GetPatchStatistics()
        {
            return new Dictionary<string, PatchInfo>(_appliedPatches);
        }

        private void InitializeHarmony()
        {
            var harmonyId = "com.golani.sptmodtranslator.sklf.harmony";
            _harmonyInstance = new Harmony(harmonyId);
            _logManager.LogDebug($"Harmony 인스턴스 초기화됨: {harmonyId}");
        }

        private void RegisterBuiltInPatches()
        {
            RegisterPatch("unity.ui.text", typeof(UnityUITextPatch));
            RegisterPatch("textmeshpro.text", typeof(TextMeshProPatch));
            RegisterPatch("imgui.text", typeof(ImGuiTextPatch));
            RegisterPatch("legacy.gui.text", typeof(LegacyGUITextPatch));
        }

        private void ApplyRegisteredPatches()
        {
            foreach (var patch in _registeredPatches.Values.Where(p => p.IsEnabled))
            {
                ApplyPatchInternal(patch);
            }
        }

        private void ApplyPatchInternal(PatchInfo patchInfo)
        {
            try
            {
                _harmonyInstance.PatchAll(patchInfo.PatchClass);
                
                patchInfo.IsApplied = true;
                patchInfo.AppliedAt = DateTime.Now;
                _appliedPatches[patchInfo.Id] = patchInfo;

                OnPatchApplied?.Invoke(patchInfo.Id);
                _logManager.LogDebug($"패치 적용 성공: {patchInfo.Id}");
            }
            catch (Exception ex)
            {
                patchInfo.LastError = ex;
                OnPatchError?.Invoke(patchInfo.Id, ex);
                _logManager.LogError($"패치 적용 실패: {patchInfo.Id}", ex);
            }
        }

        private void RemovePatchInternal(string patchId)
        {
            if (_appliedPatches.ContainsKey(patchId))
            {
                var patchInfo = _appliedPatches[patchId];
                
                try
                {
                    _harmonyInstance.Unpatch(patchInfo.TargetMethod, HarmonyPatchType.All, _harmonyInstance.Id);
                    
                    patchInfo.IsApplied = false;
                    _appliedPatches.Remove(patchId);

                    OnPatchRemoved?.Invoke(patchId);
                    _logManager.LogDebug($"패치 제거 성공: {patchId}");
                }
                catch (Exception ex)
                {
                    OnPatchError?.Invoke(patchId, ex);
                    _logManager.LogError($"패치 제거 실패: {patchId}", ex);
                }
            }
        }
    }
}