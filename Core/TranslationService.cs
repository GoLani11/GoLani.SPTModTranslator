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

        // 초기화 및 번역 로드
        public static void Initialize(ManualLogSource logger)
        {
            log = logger;
            LoadTranslations();
        }

        // 번역 JSON 파일 로드
        private static void LoadTranslations()
        {
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string folder = Path.Combine(pluginFolder, "translations");
            if (!Directory.Exists(folder))
            {
                log.LogWarning($"번역 폴더가 존재하지 않음: {folder}");
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*_ko.json"))
            {
                try
                {
                    string modId = Path.GetFileNameWithoutExtension(file).Replace("_ko", string.Empty);
                    string json = File.ReadAllText(file);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    translations[modId] = dict;
                    log.LogInfo($"번역 로드: {modId}, 총 {dict.Count}개");
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