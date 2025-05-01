using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Newtonsoft.Json;
using GoLaniSPTModTranslator.Models;

namespace GoLaniSPTModTranslator.Core
{
    public static class UntranslatedExtractor
    {
        public static void ExtractAllUntranslated(ManualLogSource log, string lang)
        {
            try
            {
                // 기존 미번역 추출 파일 삭제
                string pluginFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string outDir = Path.Combine(pluginFolder, "untranslations");
                if (Directory.Exists(outDir))
                {
                    foreach (var file in Directory.GetFiles(outDir, "*_untranslated.json"))
                    {
                        try { File.Delete(file); }
                        catch (Exception ex) { log.LogWarning($"기존 미번역 파일 삭제 실패: {file} - {ex.Message}"); }
                    }
                }

                // 미번역 문자열 로깅 활성화
                TranslationService.SetUntranslatedLogging(true);
                log.LogInfo("미번역 문자열 추출 시작됨 - F12 메뉴를 닫고 게임에서 번역이 필요한 메뉴/화면을 탐색하세요");
                log.LogInfo("추출을 완료하려면 다시 F12를 열고 '미번역 문자열 추출' 체크를 해제하세요");

                // 사용자에게 안내 메시지 - 로깅 시작만 하고 종료는 하지 않음
                // 실제 수집은 TranslationService.GetTranslation() 호출 시 자동으로 이루어짐
            }
            catch (Exception ex)
            {
                log.LogError($"미번역 추출 시작 중 오류: {ex}");
            }
        }

        public static void FinishExtraction(ManualLogSource log)
        {
            try
            {
                // 미번역 문자열 로깅 비활성화
                TranslationService.SetUntranslatedLogging(false);
                log.LogInfo("미번역 문자열 추출 완료됨 - untranslations 폴더에 결과가 저장되었습니다");
            }
            catch (Exception ex)
            {
                log.LogError($"미번역 추출 완료 중 오류: {ex}");
            }
        }
    }
} 