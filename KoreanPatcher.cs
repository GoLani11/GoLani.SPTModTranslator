using BepInEx;
using BepInEx.Logging;
using KoreanPatcher.Core;

namespace KoreanPatcher
{
    [BepInPlugin("com.gomim.koreanpatcher", "KoreanPatcher", "1.0.0")]
    public class KoreanPatcher : BaseUnityPlugin
    {
        // 싱글톤 인스턴스
        public static KoreanPatcher Instance { get; private set; }
        private bool _needReapply;

        private ManualLogSource Log;

        private void Awake()
        {
            Instance = this;
            _needReapply = false;
            Log = Logger;
            Log.LogInfo("KoreanPatcher 초기화 시작");

            // 번역 서비스 초기화
            TranslationService.Initialize(Log);
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