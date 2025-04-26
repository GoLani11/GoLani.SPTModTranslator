using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace GoLaniSPTModTranslator.Core
{
    public static class TranslationService
    {
        private static ManualLogSource log;
        private static Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();
        private static string lastLang = "ko";
        private static HashSet<string> loggedUntranslated = new HashSet<string>();
        private static object untranslatedLock = new object();
        private static bool enableUntranslatedLogging = false;

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
            lastLang = lang;
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string folder = Path.Combine(pluginFolder, "translations", lang);
            if (!Directory.Exists(folder))
            {
                log.LogWarning($"번역 폴더가 존재하지 않음: {folder}");
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*_" + lang + ".json"))
            {
                try
                {
                    string modId = Path.GetFileNameWithoutExtension(file).Replace("_" + lang, "");
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
                enableUntranslatedLogging = enable;
                if (enable) loggedUntranslated.Clear();
            }
        }

        // 미번역 문자열 기록
        private static void LogUntranslated(string modId, string original)
        {
            if (string.IsNullOrWhiteSpace(original) || !enableUntranslatedLogging) return;
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
                if (!dict.ContainsKey(original))
                    dict[original] = "";
                File.WriteAllText(outPath, JsonConvert.SerializeObject(dict, Formatting.Indented));
            }
        }

        // 원문에 대한 번역 반환
        public static string GetTranslation(string modId, string original)
        {
            if (modId == "ConfigManu")
            {
                foreach (var translationDict in translations.Values)
                    if (translationDict.TryGetValue(original, out var translated))
                        return translated;
                LogUntranslated(modId, original);
                return original;
            }
            if (translations.TryGetValue(modId, out var dict) && dict.TryGetValue(original, out var translated2))
                return translated2;
            LogUntranslated(modId, original);
            return original;
        }
    }
} 