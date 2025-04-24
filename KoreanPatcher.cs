using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using KoreanPatcher.Core;
using System.Collections.Generic;

namespace KoreanPatcher
{
    [BepInPlugin("com.gomim.koreanpatcher", "KoreanPatcher", "1.0.0")]
    public class KoreanPatcher : BaseUnityPlugin
    {
        // 싱글톤 인스턴스
        public static KoreanPatcher Instance { get; private set; }
        private bool _needReapply;

        private ManualLogSource Log;

        // 언어 선택 ConfigEntry
        public static ConfigEntry<string> SelectedLanguage;

        private void Awake()
        {
            Instance = this;
            _needReapply = false;
            Log = Logger;
            Log.LogInfo("KoreanPatcher 초기화 시작");

            // 지원 언어 코드 자동 탐색 (translations/ 하위 폴더)
            var pluginFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var translationsRoot = Path.Combine(pluginFolder, "translations");
            string[] langCodes = Directory.Exists(translationsRoot)
                ? Directory.GetDirectories(translationsRoot).Select(Path.GetFileName).ToArray()
                : new string[] { "ko" };

            SelectedLanguage = Config.Bind("번역", "언어", langCodes.Contains("ko") ? "ko" : langCodes.FirstOrDefault() ?? "ko",
                new ConfigDescription("사용할 번역 언어 (폴더명 기준)", new AcceptableValueList<string>(langCodes)));
            SelectedLanguage.SettingChanged += (s, e) =>
            {
                TranslationService.ReloadTranslations(SelectedLanguage.Value);
                PatchService.ReapplyPatches();
            };

            // 번역 서비스 초기화 (기본 언어)
            TranslationService.Initialize(Log, SelectedLanguage.Value);
            // 패치 정의 로드
            PatchDefinitionService.Initialize(Log);
            // 모드별 번역 활성화 ConfigEntry 생성
            ModTranslationConfigService.Initialize(this);
            // 패치 서비스 초기화 및 적용
            PatchService.Initialize(Log);
            PatchService.ApplyPatches();

            Log.LogInfo("KoreanPatcher 초기화 완료");
        }

        private void Update()
        {
            if (_needReapply)
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
            Log.LogInfo("KoreanPatcher 종료");
        }
    }
} 