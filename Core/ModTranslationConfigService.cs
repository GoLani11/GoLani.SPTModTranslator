using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using KoreanPatcher.Core;

namespace KoreanPatcher.Core
{
    public static class ModTranslationConfigService
    {
        public static Dictionary<string, ConfigEntry<bool>> ModTranslationEnabled = new Dictionary<string, ConfigEntry<bool>>();

        public static void Initialize(BepInEx.BaseUnityPlugin plugin)
        {
            // 패치 대상 모드ID 추출 및 ConfigEntry 생성
            var modIds = PatchDefinitionService.PatchDefinitions
                .Select(d => d.TranslationModID)
                .Distinct();
            foreach (var modId in modIds)
            {
                var entry = plugin.Config.Bind("번역 활성화", $"{modId} 번역 사용", true, $"{modId} 모드의 번역을 활성화/비활성화합니다.");
                ModTranslationEnabled[modId] = entry;
            }
        }
    }
} 