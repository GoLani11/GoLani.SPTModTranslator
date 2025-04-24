using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx.Logging;
using KoreanPatcher.Models;
using KoreanPatcher;

namespace KoreanPatcher.Core
{
    public static class PatchService
    {
        private static ManualLogSource _log;
        private static Harmony _harmony;
        // 원본 메소드와 PatchDefinition 매핑
        private static Dictionary<MethodBase, PatchDefinition> PatchDefinitionMap = new Dictionary<MethodBase, PatchDefinition>();

        // 초기화: Harmony 인스턴스 생성
        public static void Initialize(ManualLogSource logger)
        {
            _log = logger;
            _harmony = new Harmony("com.gomim.koreanpatcher");
        }

        // 모든 패치 적용
        public static void ApplyPatches()
        {
            foreach (var def in PatchDefinitionService.PatchDefinitions)
            {
                if (!def.Enabled)
                    continue;

                // 대상 타입 찾기
                Type targetType = null;
                if (!string.IsNullOrEmpty(def.TargetAssembly))
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Equals(def.TargetAssembly, StringComparison.OrdinalIgnoreCase));
                    if (asm != null)
                        targetType = asm.GetType(def.TargetType);
                }
                if (targetType == null)
                    targetType = AccessTools.TypeByName(def.TargetType);

                if (targetType == null)
                {
                    _log.LogError($"타입 미발견: {def.TargetType}");
                    continue;
                }

                // 대상 메소드 찾기
                var originalMethod = AccessTools.Method(targetType, def.TargetMethod);
                if (originalMethod == null)
                {
                    _log.LogError($"메소드 미발견: {def.TargetType}.{def.TargetMethod}");
                    continue;
                }

                // 패치 중복 방지
                if (PatchDefinitionMap.ContainsKey(originalMethod))
                {
                    _log.LogWarning($"이미 패치된 메소드: {def.TargetType}.{def.TargetMethod} (중복 패치 방지)");
                    continue;
                }

                // Prefix/Postfix 메소드 설정
                HarmonyMethod prefix = null;
                HarmonyMethod postfix = null;
                switch (def.PatchType)
                {
                    case "PostfixReturnString":
                        postfix = new HarmonyMethod(typeof(PatchService).GetMethod(
                            nameof(HandlePostfixReturnString), BindingFlags.Static | BindingFlags.NonPublic));
                        break;
                    case "PrefixRefStringParameter":
                        prefix = new HarmonyMethod(typeof(PatchService).GetMethod(
                            nameof(HandlePrefixRefStringParameter), BindingFlags.Static | BindingFlags.NonPublic));
                        break;
                    case "PrefixStringParameter":
                        prefix = new HarmonyMethod(typeof(PatchService).GetMethod(
                            nameof(HandlePrefixStringParameter), BindingFlags.Static | BindingFlags.NonPublic));
                        break;
                    default:
                        _log.LogWarning($"알 수 없는 PatchType: {def.PatchType}");
                        continue;
                }

                // 매핑 저장 후 패치 적용
                PatchDefinitionMap[originalMethod] = def;
                _harmony.Patch(originalMethod, prefix, postfix);
                _log.LogInfo($"패치 적용: {def.TargetType}.{def.TargetMethod} ({def.PatchType})");
            }
        }

        // 모든 패치 언패치
        public static void UnpatchAll()
        {
            _harmony.UnpatchSelf();
        }

        // 모든 패치 언패치 후 다시 적용하는 메소드 추가
        public static void ReapplyPatches()
        {
            _log.LogInfo("번역 설정 변경 감지: 패치 다시 적용 중...");
            
            // 기존 패치 언패치
            _harmony.UnpatchSelf();
            PatchDefinitionMap.Clear();
            
            // 패치 다시 적용
            ApplyPatches();
            
            _log.LogInfo("패치 다시 적용 완료.");
        }

        // Postfix 핸들러: 반환 문자열 번역
        private static void HandlePostfixReturnString(MethodBase __originalMethod, ref string __result)
        {
            PatchDefinition def;
            if (PatchDefinitionMap.TryGetValue(__originalMethod, out def))
            {
                // 번역 활성화 설정 체크
                if (ModTranslationConfigService.ModTranslationEnabled.ContainsKey(def.TranslationModID) &&
                    !ModTranslationConfigService.ModTranslationEnabled[def.TranslationModID].Value)
                    return;
                __result = TranslationService.GetTranslation(def.TranslationModID, __result);
            }
        }

        // Prefix 핸들러: ref/out string 파라미터 번역
        private static void HandlePrefixRefStringParameter(MethodBase __originalMethod, ref object[] __args)
        {
            PatchDefinition def;
            if (PatchDefinitionMap.TryGetValue(__originalMethod, out def) && def.ParameterIndex.HasValue)
            {
                if (ModTranslationConfigService.ModTranslationEnabled.ContainsKey(def.TranslationModID) &&
                    !ModTranslationConfigService.ModTranslationEnabled[def.TranslationModID].Value)
                    return;
                int idx = def.ParameterIndex.Value;
                if (idx >= 0 && idx < __args.Length && __args[idx] is string)
                    __args[idx] = TranslationService.GetTranslation(def.TranslationModID, (string)__args[idx]);
            }
        }

        // Prefix 핸들러: 일반 string 파라미터 번역
        private static void HandlePrefixStringParameter(MethodBase __originalMethod, ref object[] __args)
        {
            PatchDefinition def;
            if (PatchDefinitionMap.TryGetValue(__originalMethod, out def) && def.ParameterIndex.HasValue)
            {
                if (ModTranslationConfigService.ModTranslationEnabled.ContainsKey(def.TranslationModID) &&
                    !ModTranslationConfigService.ModTranslationEnabled[def.TranslationModID].Value)
                    return;
                int idx = def.ParameterIndex.Value;
                if (idx >= 0 && idx < __args.Length && __args[idx] is string)
                    __args[idx] = TranslationService.GetTranslation(def.TranslationModID, (string)__args[idx]);
            }
        }
    }
} 