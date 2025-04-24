using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Newtonsoft.Json;

namespace KoreanPatcher.Core
{
    public static class TranslationService
    {
        private static ManualLogSource log;
        private static Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();
        private static string lastLang = "ko";

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

        // 원문에 대한 번역 반환
        public static string GetTranslation(string modId, string original)
        {
            if (translations.TryGetValue(modId, out var dict) && dict.TryGetValue(original, out var translated))
                return translated;
            return original;
        }
    }
} 