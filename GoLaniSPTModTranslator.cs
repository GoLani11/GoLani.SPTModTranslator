using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using GoLaniSPTModTranslator.Core;
using System.Collections.Generic;

namespace GoLaniSPTModTranslator
{
    [BepInPlugin("com.golani.sptmodtranslator", "GoLani.SPTModTranslator", "1.1.0")]
    public class GoLaniSPTModTranslator : BaseUnityPlugin
    {
        // 싱글톤 인스턴스
        public static GoLaniSPTModTranslator Instance { get; private set; }
        private bool _needReapply;

        private ManualLogSource Log;

        // 언어 선택 ConfigEntry
        public static ConfigEntry<string> SelectedLanguage;

        // 모드 전체 활성화 ConfigEntry
        public static ConfigEntry<bool> ModEnabled;

        // 미번역 추출 ConfigEntry
        public static ConfigEntry<bool> ExtractUntranslated;
        
        // 런타임 인터셉션 ConfigEntry
        public static ConfigEntry<bool> EnableRuntimeInterception;

        private void Awake()
        {
            Instance = this;
            _needReapply = false;
            Log = Logger;
            Log.LogInfo("GoLani SPT Mod Translator 초기화 시작");

            // 지원 언어 코드 자동 탐색 (translations/ 하위 폴더)
            var pluginFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var translationsRoot = Path.Combine(pluginFolder, "translations");
            string[] langCodes = Directory.Exists(translationsRoot)
                ? Directory.GetDirectories(translationsRoot).Select(Path.GetFileName).ToArray()
                : new string[] { "ko" };

            SelectedLanguage = Config.Bind("번역 (※ 변경 후 메뉴를 껐다 켜세요! F12 다시 열기)", "언어", langCodes.Contains("ko") ? "ko" : langCodes.FirstOrDefault() ?? "ko",
                new ConfigDescription("사용할 번역 언어 (폴더명 기준)", new AcceptableValueList<string>(langCodes)));
            SelectedLanguage.SettingChanged += (s, e) =>
            {
                TranslationService.ReloadTranslations(SelectedLanguage.Value);
                PatchService.ReapplyPatches();
            };

            // 모드 전체 활성화 ConfigEntry
            ModEnabled = Config.Bind("기본 (※ 변경 후 메뉴를 껐다 켜세요! F12 다시 열기)", "모드 활성화", true, "이 모드를 전체적으로 활성화/비활성화합니다.");
            ModEnabled.SettingChanged += (s, e) =>
            {
                if (ModEnabled.Value)
                {
                    PatchService.ApplyPatches();
                }
                else
                {
                    PatchService.UnpatchAll();
                    PatchService.ClearPatchMap();
                }
            };

            // 미번역 추출 ConfigEntry 추가
            ExtractUntranslated = Config.Bind("도구", "미번역 문자열 추출 (체크 후 F12 닫기)", false, "이 항목을 체크하면 미번역 문자열을 untranslations 폴더에 모드별로 추출합니다. 완료 후 자동으로 체크 해제됩니다.");
            ExtractUntranslated.SettingChanged += (s, e) =>
            {
                if (ExtractUntranslated.Value)
                {
                    // 체크되었을 때 - 미번역 추출 시작
                    UntranslatedExtractor.ExtractAllUntranslated(Log, SelectedLanguage.Value);
                }
                else
                {
                    // 체크 해제됐을 때 - 미번역 추출 종료
                    UntranslatedExtractor.FinishExtraction(Log);
                }
            };
            
            // 런타임 인터셉션 ConfigEntry 추가
            EnableRuntimeInterception = Config.Bind("고급 설정", "런타임 문자열 인터셉션", true, "BepInEx 플러그인의 UI 텍스트 및 알림 메시지를 실시간으로 번역합니다.");
            EnableRuntimeInterception.SettingChanged += (s, e) =>
            {
                if (EnableRuntimeInterception.Value)
                {
                    RuntimeStringInterceptor.Initialize(Log);
                    RuntimeStringInterceptor.PatchNotificationManager();
                    RuntimeStringInterceptor.PatchUITextSetters();
                }
                else
                {
                    RuntimeStringInterceptor.UnpatchAll();
                }
            };

            // 번역 서비스 초기화 (기본 언어)
            TranslationService.Initialize(Log, SelectedLanguage.Value);
            // 패치 정의 로드
            PatchDefinitionService.Initialize(Log);
            // 패치 서비스 초기화 및 적용
            PatchService.Initialize(Log);
            if (ModEnabled.Value)
                PatchService.ApplyPatches();
            
            // 런타임 인터셉터 초기화
            if (EnableRuntimeInterception.Value)
            {
                RuntimeStringInterceptor.Initialize(Log);
                RuntimeStringInterceptor.PatchNotificationManager();
                RuntimeStringInterceptor.PatchUITextSetters();
            }

            Log.LogInfo("GoLani SPT Mod Translator 초기화 완료");
        }

        private void Update()
        {
            if (_needReapply && ModEnabled.Value)
            {
                PatchService.ReapplyPatches();
                _needReapply = false;
            }
        }

        // 외부 호출용: 설정 변경 시 패치 재적용 요청
        public void RequestReapply()
        {
            _needReapply = true;
        }

        private void OnDestroy()
        {
            PatchService.UnpatchAll();
            RuntimeStringInterceptor.UnpatchAll();
            Log.LogInfo("GoLani SPT Mod Translator 종료");
        }
    }
} 