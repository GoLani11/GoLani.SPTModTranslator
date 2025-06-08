using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx.Logging;

namespace GoLaniSPTModTranslator.Core
{
    /// <summary>
    /// 런타임에서 동적으로 생성되는 문자열을 인터셉트하여 번역하는 서비스
    /// BepInEx 플러그인 모드의 UI 텍스트 번역을 위해 설계됨
    /// </summary>
    public static class RuntimeStringInterceptor
    {
        private static ManualLogSource _log;
        private static Harmony _harmony;
        private static readonly HashSet<MethodBase> _patchedMethods = new HashSet<MethodBase>();

        public static void Initialize(ManualLogSource logger)
        {
            _log = logger;
            _harmony = new Harmony("com.golani.sptmodtranslator.runtime");
        }

        /// <summary>
        /// EFT.Communications.NotificationManagerClass.DisplayMessageNotification 메서드 패치
        /// </summary>
        public static void PatchNotificationManager()
        {
            try
            {
                // NotificationManagerClass 타입 찾기
                var notificationManagerType = AccessTools.TypeByName("EFT.Communications.NotificationManagerClass");
                if (notificationManagerType == null)
                {
                    _log.LogWarning("NotificationManagerClass를 찾을 수 없습니다.");
                    return;
                }

                // DisplayMessageNotification 메서드 찾기
                var displayMessageMethod = AccessTools.Method(notificationManagerType, "DisplayMessageNotification");
                if (displayMessageMethod == null)
                {
                    _log.LogWarning("DisplayMessageNotification 메서드를 찾을 수 없습니다.");
                    return;
                }

                // 이미 패치되었는지 확인
                if (_patchedMethods.Contains(displayMessageMethod))
                {
                    _log.LogDebug("DisplayMessageNotification은 이미 패치되었습니다.");
                    return;
                }

                // Prefix 패치 적용
                var prefix = new HarmonyMethod(typeof(RuntimeStringInterceptor).GetMethod(
                    nameof(DisplayMessageNotification_Prefix), BindingFlags.Static | BindingFlags.NonPublic));
                
                _harmony.Patch(displayMessageMethod, prefix: prefix);
                _patchedMethods.Add(displayMessageMethod);
                
                _log.LogInfo("NotificationManagerClass.DisplayMessageNotification 패치 성공");
            }
            catch (Exception ex)
            {
                _log.LogError($"NotificationManager 패치 실패: {ex}");
            }
        }

        /// <summary>
        /// 일반적인 UI 텍스트 표시 메서드들을 패치
        /// UnityEngine.UI.Text, TMPro.TextMeshProUGUI 등
        /// </summary>
        public static void PatchUITextSetters()
        {
            // UnityEngine.UI.Text.text setter 패치
            try
            {
                var textType = AccessTools.TypeByName("UnityEngine.UI.Text");
                if (textType != null)
                {
                    var textProperty = AccessTools.Property(textType, "text");
                    if (textProperty != null)
                    {
                        var setter = textProperty.GetSetMethod();
                        if (setter != null && !_patchedMethods.Contains(setter))
                        {
                            var prefix = new HarmonyMethod(typeof(RuntimeStringInterceptor).GetMethod(
                                nameof(Text_set_text_Prefix), BindingFlags.Static | BindingFlags.NonPublic));
                            
                            _harmony.Patch(setter, prefix: prefix);
                            _patchedMethods.Add(setter);
                            
                            _log.LogInfo("UnityEngine.UI.Text.text setter 패치 성공");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"UI.Text 패치 실패: {ex}");
            }

            // TMPro.TextMeshProUGUI.text setter 패치
            try
            {
                var tmpType = AccessTools.TypeByName("TMPro.TextMeshProUGUI");
                if (tmpType != null)
                {
                    var textProperty = AccessTools.Property(tmpType, "text");
                    if (textProperty != null)
                    {
                        var setter = textProperty.GetSetMethod();
                        if (setter != null && !_patchedMethods.Contains(setter))
                        {
                            var prefix = new HarmonyMethod(typeof(RuntimeStringInterceptor).GetMethod(
                                nameof(TMP_set_text_Prefix), BindingFlags.Static | BindingFlags.NonPublic));
                            
                            _harmony.Patch(setter, prefix: prefix);
                            _patchedMethods.Add(setter);
                            
                            _log.LogInfo("TMPro.TextMeshProUGUI.text setter 패치 성공");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"TMPro 패치 실패: {ex}");
            }
        }

        /// <summary>
        /// 특정 플러그인의 문자열 출력 메서드를 동적으로 패치
        /// </summary>
        public static void PatchPluginMethods(string pluginTypeName, string methodName, string modId)
        {
            try
            {
                var pluginType = AccessTools.TypeByName(pluginTypeName);
                if (pluginType == null)
                {
                    _log.LogWarning($"플러그인 타입을 찾을 수 없습니다: {pluginTypeName}");
                    return;
                }

                var method = AccessTools.Method(pluginType, methodName);
                if (method == null)
                {
                    _log.LogWarning($"메서드를 찾을 수 없습니다: {pluginTypeName}.{methodName}");
                    return;
                }

                if (_patchedMethods.Contains(method))
                {
                    _log.LogDebug($"{pluginTypeName}.{methodName}은 이미 패치되었습니다.");
                    return;
                }

                // 메서드의 파라미터를 분석하여 적절한 패치 적용
                var parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == typeof(string))
                    {
                        // string 파라미터가 있는 경우 해당 인덱스로 패치
                        var prefix = new HarmonyMethod(typeof(RuntimeStringInterceptor).GetMethod(
                            nameof(GenericStringParameter_Prefix), BindingFlags.Static | BindingFlags.NonPublic));
                        
                        _harmony.Patch(method, prefix: prefix);
                        _patchedMethods.Add(method);
                        
                        // 패치 정보를 저장하여 번역 시 사용
                        _methodModIdMap[method] = modId;
                        _methodParameterIndexMap[method] = i;
                        
                        _log.LogInfo($"{pluginTypeName}.{methodName} 패치 성공 (파라미터 인덱스: {i})");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"플러그인 메서드 패치 실패: {ex}");
            }
        }

        // 메서드별 ModId 및 파라미터 인덱스 매핑
        private static readonly Dictionary<MethodBase, string> _methodModIdMap = new Dictionary<MethodBase, string>();
        private static readonly Dictionary<MethodBase, int> _methodParameterIndexMap = new Dictionary<MethodBase, int>();

        // Prefix 핸들러들
        private static void DisplayMessageNotification_Prefix(ref string message)
        {
            // BossNotifier와 같은 플러그인의 알림 메시지 번역
            message = TranslateRuntimeString(message, "BossNotifier");
        }

        private static void Text_set_text_Prefix(ref string value)
        {
            // UI 텍스트 번역 (일반적인 UI 요소)
            value = TranslateRuntimeString(value, "UI");
        }

        private static void TMP_set_text_Prefix(ref string value)
        {
            // TMPro 텍스트 번역
            value = TranslateRuntimeString(value, "UI");
        }

        private static void GenericStringParameter_Prefix(MethodBase __originalMethod, ref object[] __args)
        {
            if (_methodModIdMap.TryGetValue(__originalMethod, out var modId) &&
                _methodParameterIndexMap.TryGetValue(__originalMethod, out var index))
            {
                if (index >= 0 && index < __args.Length && __args[index] is string str)
                {
                    __args[index] = TranslateRuntimeString(str, modId);
                }
            }
        }

        /// <summary>
        /// 런타임 문자열을 번역하는 핵심 메서드
        /// </summary>
        private static string TranslateRuntimeString(string original, string modId)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            // 특정 패턴을 감지하여 적절한 ModId로 라우팅
            if (modId == "UI" || modId == "BossNotifier")
            {
                // BossNotifier 패턴 감지
                if (original.Contains("Boss") || original.Contains("located") || 
                    original.Contains("detected") || original.Contains("vicinity"))
                {
                    return TranslateBossNotifierMessage(original);
                }
            }

            // 기본 번역 처리
            return TranslationService.GetTranslation(modId, original);
        }

        /// <summary>
        /// BossNotifier의 특별한 메시지 형식을 처리
        /// </summary>
        private static string TranslateBossNotifierMessage(string original)
        {
            // "No Bosses Located" 처리
            if (original == "No Bosses Located")
            {
                return TranslationService.GetTranslation("BossNotifier", original);
            }

            // 패턴: "{Boss} {have/has} been located."
            var simplePattern = System.Text.RegularExpressions.Regex.Match(original, 
                @"^(.+) (have|has) been located\.$");
            if (simplePattern.Success)
            {
                var bossName = simplePattern.Groups[1].Value;
                var verb = simplePattern.Groups[2].Value;
                
                // 보스 이름 번역
                var translatedBoss = TranslationService.GetTranslation("BossNotifier", bossName);
                
                // 번역된 메시지 템플릿 가져오기
                var template = TranslationService.GetTranslation("BossNotifier", "{0} {1} been located.");
                
                // {0}에 보스 이름, {1}에 동사 삽입
                return string.Format(template, translatedBoss, 
                    TranslationService.GetTranslation("BossNotifier", verb));
            }

            // 패턴: "{Boss} {have/has} been located near {Location}" (with checkmark)
            var locationPattern = System.Text.RegularExpressions.Regex.Match(original, 
                @"^(.+) (have|has) been located near (.+?)(\s*✓)?$");
            if (locationPattern.Success)
            {
                var bossName = locationPattern.Groups[1].Value;
                var verb = locationPattern.Groups[2].Value;
                var location = locationPattern.Groups[3].Value;
                var checkmark = locationPattern.Groups[4].Value;
                
                // 각 부분 번역
                var translatedBoss = TranslationService.GetTranslation("BossNotifier", bossName);
                var translatedLocation = TranslationService.GetTranslation("BossNotifier", location);
                
                // 번역된 메시지 템플릿 가져오기
                var template = TranslationService.GetTranslation("BossNotifier", "{0} {1} been located near {2}");
                
                // 템플릿에 값 삽입
                var result = string.Format(template, translatedBoss, 
                    TranslationService.GetTranslation("BossNotifier", verb), translatedLocation);
                
                // 체크마크가 있으면 추가
                if (!string.IsNullOrEmpty(checkmark))
                {
                    result += checkmark;
                }
                
                return result;
            }

            // 패턴: "{Boss} {have/has} been detected in your vicinity."
            var detectedPattern = System.Text.RegularExpressions.Regex.Match(original, 
                @"^(.+) (have|has) been detected in your vicinity\.$");
            if (detectedPattern.Success)
            {
                var bossName = detectedPattern.Groups[1].Value;
                var verb = detectedPattern.Groups[2].Value;
                
                // 보스 이름 번역
                var translatedBoss = TranslationService.GetTranslation("BossNotifier", bossName);
                
                // 번역된 메시지 템플릿 가져오기
                var template = TranslationService.GetTranslation("BossNotifier", "{0} {1} been detected in your vicinity.");
                
                // 템플릿에 값 삽입
                return string.Format(template, translatedBoss, 
                    TranslationService.GetTranslation("BossNotifier", verb));
            }

            // 패턴에 맞지 않으면 기본 번역 시도
            return TranslationService.GetTranslation("BossNotifier", original);
        }

        /// <summary>
        /// 모든 런타임 패치 해제
        /// </summary>
        public static void UnpatchAll()
        {
            _harmony?.UnpatchSelf();
            _patchedMethods.Clear();
            _methodModIdMap.Clear();
            _methodParameterIndexMap.Clear();
        }
    }
}