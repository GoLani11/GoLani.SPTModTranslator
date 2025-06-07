using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using GoLani.SPTModTranslator.Core.TranslationManager;
using GoLani.SPTModTranslator.Core.CacheManager;
using GoLani.SPTModTranslator.Core.HarmonyPatch;
using GoLani.SPTModTranslator.UI.Settings;
using GoLani.SPTModTranslator.Utils.Logging;

namespace GoLani.SPTModTranslator.Integration.BepInEx
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInProcess("EscapeFromTarkov.exe")]
    public class SKLFPlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.golani.sptmodtranslator.sklf";
        public const string PLUGIN_NAME = "SPT Korean Localization Framework";
        public const string PLUGIN_VERSION = "1.0.0";

        internal static ManualLogSource Logger { get; private set; }
        internal static ConfigFile ConfigFile { get; private set; }
        internal static string PluginPath { get; private set; }

        private ITranslationManager _translationManager;
        private ICacheManager _cacheManager;
        private IHarmonyPatchManager _harmonyPatchManager;
        private ISettingsManager _settingsManager;
        private ILogManager _logManager;

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

            _logManager = new LogManager(Logger);
            _settingsManager = new SettingsManager(ConfigFile);
            _cacheManager = new CacheManager(_settingsManager, _logManager);
            _translationManager = new TranslationManager(_cacheManager, _settingsManager, _logManager);
            _harmonyPatchManager = new HarmonyPatchManager(_translationManager, _logManager);

            Logger.LogInfo("모든 컴포넌트가 초기화되었습니다.");
        }

        private void RegisterEventHandlers()
        {
            Logger.LogInfo("이벤트 핸들러 등록 중...");

            if (_settingsManager != null)
            {
                _settingsManager.OnLanguageChanged += OnLanguageChanged;
                _settingsManager.OnCacheSettingsChanged += OnCacheSettingsChanged;
            }

            Logger.LogInfo("이벤트 핸들러 등록 완료.");
        }

        private void StartSystems()
        {
            Logger.LogInfo("시스템 시작 중...");

            _cacheManager?.Initialize();
            _translationManager?.Initialize();
            _harmonyPatchManager?.ApplyPatches();

            Logger.LogInfo("모든 시스템이 시작되었습니다.");
        }

        private void ShutdownSystems()
        {
            _harmonyPatchManager?.RemovePatches();
            _translationManager?.Shutdown();
            _cacheManager?.Shutdown();

            if (_settingsManager != null)
            {
                _settingsManager.OnLanguageChanged -= OnLanguageChanged;
                _settingsManager.OnCacheSettingsChanged -= OnCacheSettingsChanged;
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

        public static bool IsPluginInitialized => Instance?._isInitialized ?? false;
        public static SKLFPlugin Instance { get; private set; }
    }
}