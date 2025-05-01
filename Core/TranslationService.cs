using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace GoLaniSPTModTranslator.Core
{
    public static class TranslationService
    {
        private static ManualLogSource log;
        private static Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();
        private static HashSet<string> loggedUntranslated = new HashSet<string>();
        private static object untranslatedLock = new object();
        private static bool enableUntranslatedLogging = false;
        
        // 동적 값이 포함된 문자열 패턴 (예: "Maximum Width (1920p)")
        private static readonly Regex DynamicValuePattern = new Regex(@"\((\d+)p\)");

        // 초기화 및 번역 로드
        public static void Initialize(ManualLogSource logger, string lang)
        {
            log = logger;
            ReloadTranslations(lang);
        }

        // 언어 변경 시 번역 재로드
        public static void ReloadTranslations(string lang)
        {
            translations.Clear();
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string folder = Path.Combine(pluginFolder, "translations", lang);
            if (!Directory.Exists(folder))
            {
                log.LogWarning($"번역 폴더가 존재하지 않음: {folder}");
                return;
            }

            // 새 방식: [ModID].json
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                string modId = Path.GetFileNameWithoutExtension(file);
                // 하위 호환: _ko 등 언어코드가 붙은 파일명은 modId에서 제거
                if (modId.EndsWith("_" + lang))
                    modId = modId.Substring(0, modId.Length - (lang.Length + 1));
                try
                {
                    string json = File.ReadAllText(file);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    translations[modId] = dict;
                    log.LogInfo($"번역 로드: {modId} ({lang}), 총 {dict.Count}개");
                }
                catch (Exception ex)
                {
                    log.LogError($"번역 로드 실패 ({file}): {ex}");
                }
            }
        }

        // 미번역 로깅 활성화/비활성화
        public static void SetUntranslatedLogging(bool enable)
        {
            lock(untranslatedLock)
            {
                // 이전 상태와 다를 때만 초기화
                if (enableUntranslatedLogging != enable)
                {
                    enableUntranslatedLogging = enable;
                    // 활성화 시 로그 캐시 초기화
                    loggedUntranslated.Clear();
                    
                    // 활성화 시 안내 로그 출력
                    if (enable && log != null)
                    {
                        log.LogInfo("미번역 문자열 추출 활성화: untranslations 폴더에 기록됩니다");
                    }
                }
            }
        }

        // 동적 값을 처리하기 위한 문자열 표준화 함수
        private static string NormalizeStringWithDynamicValues(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // "Maximum Width (1920p)" -> "Maximum Width ({RESOLUTION}p)"
            return DynamicValuePattern.Replace(input, "({RESOLUTION}p)");
        }

        // 동적 값이 있는 문자열에서 값을 추출
        private static Dictionary<string, string> ExtractDynamicValues(string input)
        {
            var result = new Dictionary<string, string>();
            
            // 해상도 추출
            var resolutionMatch = DynamicValuePattern.Match(input);
            if (resolutionMatch.Success && resolutionMatch.Groups.Count > 1)
            {
                result["RESOLUTION"] = resolutionMatch.Groups[1].Value;
            }
            
            return result;
        }

        // 표준화된 문자열에 동적 값을 다시 삽입
        private static string ReinsertDynamicValues(string template, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template)) return template;
            
            string result = template;
            foreach (var pair in values)
            {
                result = result.Replace($"{{{pair.Key}}}", pair.Value);
            }
            
            return result;
        }

        // 미번역 문자열 기록
        private static void LogUntranslated(string modId, string original)
        {
            if (string.IsNullOrWhiteSpace(original) || !enableUntranslatedLogging) return;
            
            // 동적 값이 포함된 문자열 표준화
            string normalizedOriginal = NormalizeStringWithDynamicValues(original);
            
            // 한 번 더 모든 번역 딕셔너리를 검색하여 진짜 미번역인지 확인
            // ConfigMenu의 경우 모든 딕셔너리에서 찾아봄
            if (modId == "ConfigMenu")
            {
                foreach (var translationDict in translations.Values)
                {
                    if (translationDict.TryGetValue(normalizedOriginal, out _) || translationDict.TryGetValue(original, out _))
                    {
                        // 이미 번역이 존재하므로 기록하지 않음
                        return;
                    }
                }
            }
            // 특정 모드 ID인 경우 해당 모드의 번역 딕셔너리만 확인
            else if (translations.TryGetValue(modId, out var dict) && 
                    (dict.TryGetValue(normalizedOriginal, out _) || dict.TryGetValue(original, out _)))
            {
                // 이미 번역이 존재하므로 기록하지 않음
                return;
            }

            lock (untranslatedLock)
            {
                string key = $"{modId}|{original}";
                if (loggedUntranslated.Contains(key)) return;
                loggedUntranslated.Add(key);
                string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string outDir = Path.Combine(pluginFolder, "untranslations");
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                string outPath = Path.Combine(outDir, $"{modId}_untranslated.json");
                Dictionary<string, string> dict = new Dictionary<string, string>();
                if (File.Exists(outPath))
                {
                    try { dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(outPath)); }
                    catch { dict = new Dictionary<string, string>(); }
                }
                
                // 표준화된 문자열을 저장 (동적 값이 있는 경우)
                if (normalizedOriginal != original && !dict.ContainsKey(normalizedOriginal))
                {
                    dict[normalizedOriginal] = "";
                }
                // 원본 문자열도 함께 저장 (기존 호환성 유지)
                else if (!dict.ContainsKey(original))
                {
                    dict[original] = "";
                }
                
                File.WriteAllText(outPath, JsonConvert.SerializeObject(dict, Formatting.Indented));
            }
        }

        // 원문에 대한 번역 반환
        public static string GetTranslation(string modId, string original)
        {
            if (string.IsNullOrEmpty(original)) return original;
            
            // 동적 값 추출
            Dictionary<string, string> dynamicValues = ExtractDynamicValues(original);
            
            // 동적 값이 있는 경우 표준화된 문자열로 변환
            string normalizedOriginal = NormalizeStringWithDynamicValues(original);
            
            // ConfigMenu: BepInEx ConfigurationManager 등 공용 UI 번역용
            if (modId == "ConfigMenu")
            {
                foreach (var translationDict in translations.Values)
                {
                    // 표준화된 문자열로 먼저 검색
                    if (translationDict.TryGetValue(normalizedOriginal, out var translated))
                    {
                        // 동적 값을 다시 삽입하여 반환
                        return ReinsertDynamicValues(translated, dynamicValues);
                    }
                    
                    // 기존 방식으로도 검색 (호환성 유지)
                    if (translationDict.TryGetValue(original, out var translatedOriginal))
                        return translatedOriginal;
                }
                // 모든 딕셔너리에서 찾지 못한 경우만 미번역으로 기록
                if (enableUntranslatedLogging)
                    LogUntranslated(modId, original);
                return original;
            }
            
            // 일반 모듈 번역: 해당 모듈의 딕셔너리에서만 검색
            if (translations.TryGetValue(modId, out var dict))
            {
                // 표준화된 문자열로 먼저 검색
                if (dict.TryGetValue(normalizedOriginal, out var translated))
                {
                    // 동적 값을 다시 삽입하여 반환
                    return ReinsertDynamicValues(translated, dynamicValues);
                }
                
                // 기존 방식으로도 검색 (호환성 유지)
                if (dict.TryGetValue(original, out var translatedOriginal))
                    return translatedOriginal;
            }
                
            // 번역이 없을 때만 미번역으로 기록
            if (enableUntranslatedLogging)
                LogUntranslated(modId, original);
            return original;
        }

        // UI용 Enum 표시 문자열 가져오기 (번역된 문자열)
        // public static string GetEnumDisplayText(object enumValue)
        // {
        //     if (enumValue == null) return string.Empty;
        //     string strValue = enumValue.ToString();
        //     return GetTranslation("ConfigMenu", strValue);
        // }

        // Enum의 원본 값 가져오기 (내부 처리용)
        // public static string GetOriginalEnumValue(object enumValue)
        // {
        //     return PatchService.GetOriginalEnumValue(enumValue) ?? enumValue?.ToString();
        // }
    }
} 