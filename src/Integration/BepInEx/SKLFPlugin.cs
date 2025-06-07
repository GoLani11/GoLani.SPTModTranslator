using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using GoLani.SPTModTranslator.Core.TranslationManager;
using GoLani.SPTModTranslator.Core.CacheManager;
using GoLani.SPTModTranslator.Core.HarmonyPatch;
using GoLani.SPTModTranslator.Core.ModCompatibility;
using GoLani.SPTModTranslator.Core.AutoUpdate;
using GoLani.SPTModTranslator.UI.Settings;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.Data.FileIO;
using GoLani.SPTModTranslator.Data.JsonParser;

namespace GoLani.SPTModTranslator.Integration.BepInEx
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class SKLFPlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.golani.sptmodtranslator.sklf";
        public const string PLUGIN_NAME = "SPT Korean Localization Framework";
        public const string PLUGIN_VERSION = "2.0.0"; // Phase 3 완료

        internal static ManualLogSource Logger { get; private set; }
        internal static ConfigFile ConfigFile { get; private set; }
        internal static string PluginPath { get; private set; }

        private ITranslationManager _translationManager;
        private ICacheManager _cacheManager;
        private IHarmonyPatchManager _harmonyPatchManager;
        private ISettingsManager _settingsManager;
        private ILogManager _logManager;
        private IFileIOManager _fileIOManager;
        private IJsonParser _jsonParser;
        
        // Phase 3 새로운 매니저들
        private IModCompatibilityManager _modCompatibilityManager;
        private ModAnalyzer _modAnalyzer;
        private IAutoUpdateManager _autoUpdateManager;
        private TranslationUpdateManager _translationUpdateManager;

        private bool _isInitialized = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            try
            {
                Logger = base.Logger;
                ConfigFile = Config;
                PluginPath = Path.GetDirectoryName(Info.Location);

                Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} 로딩 시작...");

                InitializeComponents();
                RegisterEventHandlers();

                _isInitialized = true;
                Logger.LogInfo($"{PLUGIN_NAME} 초기화 완료!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"플러그인 초기화 중 오류 발생: {ex}");
                throw;
            }
        }

        void Start()
        {
            if (!_isInitialized)
            {
                Logger.LogError("플러그인이 초기화되지 않았습니다.");
                return;
            }

            try
            {
                StartSystems();
                Logger.LogInfo("모든 시스템이 성공적으로 시작되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"시스템 시작 중 오류 발생: {ex}");
            }
        }

        void OnDestroy()
        {
            try
            {
                Logger.LogInfo("플러그인 종료 중...");
                ShutdownSystems();
                Logger.LogInfo("플러그인이 정상적으로 종료되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"플러그인 종료 중 오류 발생: {ex}");
            }
        }

        private void InitializeComponents()
        {
            Logger.LogInfo("컴포넌트 초기화 중...");

            // 기본 매니저들 초기화
            _logManager = new LogManager(Logger);
            _settingsManager = new SettingsManager(ConfigFile);
            _fileIOManager = new FileIOManager(_logManager);
            _jsonParser = new JsonParser(_logManager);
            _cacheManager = new CacheManager(_settingsManager, _logManager);
            _translationManager = new TranslationManager(_jsonParser, _fileIOManager, _logManager, _settingsManager);
            _harmonyPatchManager = new HarmonyPatchManager(_translationManager, _logManager);

            // Phase 3 새로운 매니저들 초기화
            _modCompatibilityManager = new ModCompatibilityManager(_logManager, _settingsManager, _fileIOManager);
            _modAnalyzer = new ModAnalyzer(_logManager);
            _autoUpdateManager = new AutoUpdateManager(_logManager, _settingsManager, _fileIOManager);
            _translationUpdateManager = new TranslationUpdateManager(_logManager, _settingsManager, _jsonParser, _translationManager);

            Logger.LogInfo("모든 컴포넌트가 초기화되었습니다.");
        }

        private void RegisterEventHandlers()
        {
            Logger.LogInfo("이벤트 핸들러 등록 중...");

            // 기본 이벤트 핸들러
            if (_settingsManager != null)
            {
                _settingsManager.OnLanguageChanged += OnLanguageChanged;
                _settingsManager.OnCacheSettingsChanged += OnCacheSettingsChanged;
            }

            // 모드 호환성 이벤트 핸들러
            if (_modCompatibilityManager != null)
            {
                _modCompatibilityManager.OnModDetected += OnModDetected;
                _modCompatibilityManager.OnCompatibilityIssueDetected += OnCompatibilityIssueDetected;
                _modCompatibilityManager.OnModAnalysisCompleted += OnModAnalysisCompleted;
            }

            // 자동 업데이트 이벤트 핸들러
            if (_autoUpdateManager != null)
            {
                _autoUpdateManager.OnUpdateAvailable += OnUpdateAvailable;
                _autoUpdateManager.OnUpdateCompleted += OnUpdateCompleted;
                _autoUpdateManager.OnUpdateError += OnUpdateError;
            }

            // 번역 업데이트 이벤트 핸들러
            if (_translationUpdateManager != null)
            {
                _translationUpdateManager.OnTranslationUpdateAvailable += OnTranslationUpdateAvailable;
                _translationUpdateManager.OnTranslationUpdateCompleted += OnTranslationUpdateCompleted;
                _translationUpdateManager.OnTranslationUpdateError += OnTranslationUpdateError;
            }

            Logger.LogInfo("이벤트 핸들러 등록 완료.");
        }

        private void StartSystems()
        {
            Logger.LogInfo("시스템 시작 중...");

            // 기본 시스템 시작 (순서 중요)
            _cacheManager?.Initialize();
            _translationManager?.Initialize();
            _harmonyPatchManager?.ApplyPatches();

            // Phase 3 새로운 시스템들 시작
            _modCompatibilityManager?.Initialize();
            _autoUpdateManager?.Initialize();
            _translationUpdateManager?.Initialize();

            // 성능 최적화를 위한 비동기 초기화
            StartAsyncSystems();
            
            // 성능 모니터링 시작
            StartPerformanceMonitoring();

            Logger.LogInfo("모든 시스템이 시작되었습니다.");
        }

        private async void StartAsyncSystems()
        {
            try
            {
                // 백그라운드에서 모드 스캔 및 분석 실행
                if (_modCompatibilityManager != null)
                {
                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        _modCompatibilityManager.ScanForMods();
                        _modCompatibilityManager.AnalyzeModCompatibility();
                    });
                }

                // 백그라운드에서 업데이트 확인
                if (_autoUpdateManager != null)
                {
                    await _autoUpdateManager.CheckForUpdatesAsync();
                }

                // 백그라운드에서 번역 업데이트 확인
                if (_translationUpdateManager != null)
                {
                    await _translationUpdateManager.CheckForTranslationUpdatesAsync();
                }

                Logger.LogInfo("비동기 시스템 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.LogError($"비동기 시스템 초기화 중 오류: {ex}");
            }
        }

        private void ShutdownSystems()
        {
            // 새로운 시스템들 종료
            _translationUpdateManager?.Shutdown();
            _autoUpdateManager?.Shutdown();
            _modCompatibilityManager?.Shutdown();

            // 기본 시스템들 종료
            _harmonyPatchManager?.RemovePatches();
            _translationManager?.Shutdown();
            _cacheManager?.Shutdown();

            // 이벤트 핸들러 제거
            UnregisterEventHandlers();
        }

        private void UnregisterEventHandlers()
        {
            // 기본 이벤트 핸들러 제거
            if (_settingsManager != null)
            {
                _settingsManager.OnLanguageChanged -= OnLanguageChanged;
                _settingsManager.OnCacheSettingsChanged -= OnCacheSettingsChanged;
            }

            // 모드 호환성 이벤트 핸들러 제거
            if (_modCompatibilityManager != null)
            {
                _modCompatibilityManager.OnModDetected -= OnModDetected;
                _modCompatibilityManager.OnCompatibilityIssueDetected -= OnCompatibilityIssueDetected;
                _modCompatibilityManager.OnModAnalysisCompleted -= OnModAnalysisCompleted;
            }

            // 자동 업데이트 이벤트 핸들러 제거
            if (_autoUpdateManager != null)
            {
                _autoUpdateManager.OnUpdateAvailable -= OnUpdateAvailable;
                _autoUpdateManager.OnUpdateCompleted -= OnUpdateCompleted;
                _autoUpdateManager.OnUpdateError -= OnUpdateError;
            }

            // 번역 업데이트 이벤트 핸들러 제거
            if (_translationUpdateManager != null)
            {
                _translationUpdateManager.OnTranslationUpdateAvailable -= OnTranslationUpdateAvailable;
                _translationUpdateManager.OnTranslationUpdateCompleted -= OnTranslationUpdateCompleted;
                _translationUpdateManager.OnTranslationUpdateError -= OnTranslationUpdateError;
            }
        }

        private void OnLanguageChanged(string newLanguage)
        {
            Logger.LogInfo($"언어가 변경되었습니다: {newLanguage}");
            _translationManager?.ReloadTranslations();
        }

        private void OnCacheSettingsChanged()
        {
            Logger.LogInfo("캐시 설정이 변경되었습니다.");
            _cacheManager?.RefreshSettings();
        }

        // 모드 호환성 이벤트 핸들러들
        private void OnModDetected(ModInfo modInfo)
        {
            Logger.LogInfo($"새 모드 감지됨: {modInfo.Name} v{modInfo.Version}");
            
            // 모드 자동 분석 실행
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var analysisResult = await _modAnalyzer.AnalyzeModAsync(modInfo);
                    if (analysisResult.IsSuccess)
                    {
                        var pattern = _modAnalyzer.GenerateTranslationPattern(modInfo, analysisResult);
                        _modCompatibilityManager.RegisterModTranslationPattern(modInfo.Id, pattern);
                        Logger.LogInfo($"모드 번역 패턴 자동 생성 완료: {modInfo.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"모드 자동 분석 실패: {modInfo.Name}", ex);
                }
            });
        }

        private void OnCompatibilityIssueDetected(string issue)
        {
            Logger.LogWarning($"호환성 문제 감지: {issue}");
        }

        private void OnModAnalysisCompleted()
        {
            Logger.LogInfo("모드 호환성 분석 완료");
            
            // 통계 정보 로깅
            var stats = _modCompatibilityManager?.GetCompatibilityStatistics();
            if (stats != null)
            {
                Logger.LogInfo($"호환성 통계 - 전체: {stats["total_mods"]}, 호환: {stats["compatible_mods"]}, 비호환: {stats["incompatible_mods"]}");
            }
        }

        // 자동 업데이트 이벤트 핸들러들
        private void OnUpdateAvailable(UpdateInfo updateInfo)
        {
            Logger.LogInfo($"새 업데이트 발견: {updateInfo.Name} v{updateInfo.Version}");
            
            // 중요한 업데이트인 경우 자동 적용
            if (_autoUpdateManager?.IsUpdateRequired(updateInfo) == true)
            {
                Logger.LogInfo($"중요 업데이트 자동 적용 시작: {updateInfo.Name}");
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await _autoUpdateManager.DownloadAndApplyUpdateAsync(updateInfo);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"자동 업데이트 적용 실패: {updateInfo.Name}", ex);
                    }
                });
            }
        }

        private void OnUpdateCompleted(UpdateResult result)
        {
            if (result.IsSuccess)
            {
                Logger.LogInfo($"업데이트 완료: {result.GetSummary()}");
            }
            else
            {
                Logger.LogError($"업데이트 실패: {result.GetSummary()}");
            }
        }

        private void OnUpdateError(string error)
        {
            Logger.LogError($"업데이트 오류: {error}");
        }

        // 번역 업데이트 이벤트 핸들러들
        private void OnTranslationUpdateAvailable(TranslationUpdateInfo updateInfo)
        {
            Logger.LogInfo($"새 번역 업데이트 발견: {updateInfo.FileName} v{updateInfo.Version}");
            
            // 번역 업데이트 자동 적용 (설정에 따라)
            var autoApplyEnabled = _settingsManager?.GetSetting<bool>("translation_update.auto_apply", false) ?? false;
            if (autoApplyEnabled)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await _translationUpdateManager.ApplyTranslationUpdateAsync(updateInfo);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"번역 업데이트 자동 적용 실패: {updateInfo.FileName}", ex);
                    }
                });
            }
        }

        private void OnTranslationUpdateCompleted(TranslationUpdateResult result)
        {
            if (result.IsSuccess)
            {
                Logger.LogInfo($"번역 업데이트 완료: {result.FileName} (품질점수: {result.QualityScore:F1})");
                
                // 번역 매니저 새로고침
                _translationManager?.ReloadTranslations();
            }
            else
            {
                Logger.LogError($"번역 업데이트 실패: {result.FileName} - {result.ErrorMessage}");
            }
        }

        private void OnTranslationUpdateError(string error)
        {
            Logger.LogError($"번역 업데이트 오류: {error}");
        }

        // 성능 모니터링을 위한 메서드
        private void LogPerformanceStatistics()
        {
            try
            {
                var cacheStats = _cacheManager?.GetCacheStatistics();
                var translationStats = _translationManager?.GetStatistics();
                var compatibilityStats = _modCompatibilityManager?.GetCompatibilityStatistics();
                var updateStats = _autoUpdateManager?.GetUpdateStatistics();

                Logger.LogInfo("=== SKLF 성능 통계 ===");
                
                if (cacheStats != null)
                {
                    Logger.LogInfo($"캐시 - 히트율: {cacheStats.GetValueOrDefault("hit_ratio", 0):P2}, 메모리 사용량: {cacheStats.GetValueOrDefault("memory_usage", 0)} bytes");
                }
                
                if (translationStats != null)
                {
                    Logger.LogInfo($"번역 - 총 번역: {translationStats.GetValueOrDefault("total_translations", 0)}, 히트율: {translationStats.GetValueOrDefault("hit_ratio", 0):P2}");
                }
                
                if (compatibilityStats != null)
                {
                    Logger.LogInfo($"호환성 - 호환 모드: {compatibilityStats.GetValueOrDefault("compatible_mods", 0)}/{compatibilityStats.GetValueOrDefault("total_mods", 0)}");
                }
                
                Logger.LogInfo("====================");
            }
            catch (Exception ex)
            {
                Logger.LogError("성능 통계 출력 실패", ex);
            }
        }

        // 정기적인 성능 모니터링을 위한 타이머
        private void StartPerformanceMonitoring()
        {
            var monitoringEnabled = _settingsManager?.GetSetting<bool>("performance.monitoring_enabled", false) ?? false;
            if (!monitoringEnabled)
                return;

            var monitoringInterval = _settingsManager?.GetSetting<int>("performance.monitoring_interval_minutes", 10) ?? 10;
            
            var timer = new System.Threading.Timer(_ => LogPerformanceStatistics(), null, 
                TimeSpan.FromMinutes(monitoringInterval), 
                TimeSpan.FromMinutes(monitoringInterval));
        }

        public static bool IsPluginInitialized => Instance?._isInitialized ?? false;
        public static SKLFPlugin Instance { get; private set; }
        
        // Phase 3 기능들에 대한 공개 API
        public static IModCompatibilityManager ModCompatibilityManager => Instance?._modCompatibilityManager;
        public static IAutoUpdateManager AutoUpdateManager => Instance?._autoUpdateManager;
        public static TranslationUpdateManager TranslationUpdateManager => Instance?._translationUpdateManager;
    }
}