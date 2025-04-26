using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Newtonsoft.Json;
using GoLaniSPTModTranslator.Models;

namespace GoLaniSPTModTranslator.Core
{
    public static class PatchDefinitionService
    {
        private static ManualLogSource log;
        private static List<PatchDefinition> patchDefinitions = new List<PatchDefinition>();

        public static IReadOnlyList<PatchDefinition> PatchDefinitions => patchDefinitions;

        public static void Initialize(ManualLogSource logger)
        {
            log = logger;
            LoadDefinitions();
        }

        private static void LoadDefinitions()
        {
            string pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string folder = Path.Combine(pluginFolder, "patch_definitions");
            if (!Directory.Exists(folder))
            {
                log.LogWarning($"패치 정의 폴더가 존재하지 않음: {folder}");
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var defs = JsonConvert.DeserializeObject<List<PatchDefinition>>(json);
                    patchDefinitions.AddRange(defs);
                    log.LogInfo($"패치 정의 로드: {Path.GetFileName(file)} => {defs.Count}개");
                }
                catch (Exception ex)
                {
                    log.LogError($"패치 정의 로드 실패 ({file}): {ex}");
                }
            }
        }
    }
} 