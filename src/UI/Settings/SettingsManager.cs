using System;
using BepInEx.Configuration;

namespace GoLani.SPTModTranslator.UI.Settings
{
    public class SettingsManager : ISettingsManager
    {
        private readonly ConfigFile _configFile;
        
        private ConfigEntry<string> _currentLanguage;
        private ConfigEntry<bool> _enableCaching;
        private ConfigEntry<int> _cacheSize;
        private ConfigEntry<int> _cacheExpirationMinutes;
        private ConfigEntry<bool> _enableDebugMode;
        private ConfigEntry<bool> _showImGuiPanel;

        public event Action<string> OnLanguageChanged;
        public event Action OnCacheSettingsChanged;
        public event Action OnPerformanceSettingsChanged;

        public string CurrentLanguage
        {
            get => _currentLanguage?.Value ?? "ko_KR";
            set
            {
                if (_currentLanguage != null && _currentLanguage.Value != value)
                {
                    _currentLanguage.Value = value;
                    OnLanguageChanged?.Invoke(value);
                }
            }
        }

        public bool EnableCaching
        {
            get => _enableCaching?.Value ?? true;
            set
            {
                if (_enableCaching != null && _enableCaching.Value != value)
                {
                    _enableCaching.Value = value;
                    OnCacheSettingsChanged?.Invoke();
                }
            }
        }

        public int CacheSize
        {
            get => _cacheSize?.Value ?? 1000;
            set
            {
                if (_cacheSize != null && _cacheSize.Value != value)
                {
                    _cacheSize.Value = value;
                    OnCacheSettingsChanged?.Invoke();
                }
            }
        }

        public int CacheExpirationMinutes
        {
            get => _cacheExpirationMinutes?.Value ?? 60;
            set
            {
                if (_cacheExpirationMinutes != null && _cacheExpirationMinutes.Value != value)
                {
                    _cacheExpirationMinutes.Value = value;
                    OnCacheSettingsChanged?.Invoke();
                }
            }
        }

        public bool EnableDebugMode
        {
            get => _enableDebugMode?.Value ?? false;
            set
            {
                if (_enableDebugMode != null && _enableDebugMode.Value != value)
                {
                    _enableDebugMode.Value = value;
                    OnPerformanceSettingsChanged?.Invoke();
                }
            }
        }

        public bool ShowImGuiPanel
        {
            get => _showImGuiPanel?.Value ?? true;
            set
            {
                if (_showImGuiPanel != null)
                {
                    _showImGuiPanel.Value = value;
                }
            }
        }

        public SettingsManager(ConfigFile configFile)
        {
            _configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
        }

        public void Initialize()
        {
            _currentLanguage = _configFile.Bind(
                "Localization",
                "Language",
                "ko_KR",
                "현재 사용할 언어 (ko_KR, en_US)"
            );

            _enableCaching = _configFile.Bind(
                "Performance",
                "EnableCaching",
                true,
                "번역 캐싱 활성화 여부"
            );

            _cacheSize = _configFile.Bind(
                "Performance",
                "CacheSize",
                1000,
                "캐시에 저장할 최대 번역 항목 수"
            );

            _cacheExpirationMinutes = _configFile.Bind(
                "Performance",
                "CacheExpirationMinutes",
                60,
                "캐시 만료 시간 (분)"
            );

            _enableDebugMode = _configFile.Bind(
                "Debug",
                "EnableDebugMode",
                false,
                "디버그 모드 활성화"
            );

            _showImGuiPanel = _configFile.Bind(
                "UI",
                "ShowImGuiPanel",
                true,
                "ImGui 관리 패널 표시 여부"
            );

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            _currentLanguage.SettingChanged += (sender, args) => OnLanguageChanged?.Invoke(_currentLanguage.Value);
            _enableCaching.SettingChanged += (sender, args) => OnCacheSettingsChanged?.Invoke();
            _cacheSize.SettingChanged += (sender, args) => OnCacheSettingsChanged?.Invoke();
            _cacheExpirationMinutes.SettingChanged += (sender, args) => OnCacheSettingsChanged?.Invoke();
            _enableDebugMode.SettingChanged += (sender, args) => OnPerformanceSettingsChanged?.Invoke();
        }

        public void Save()
        {
            _configFile.Save();
        }

        public void Reset()
        {
            _currentLanguage.Value = (string)_currentLanguage.DefaultValue;
            _enableCaching.Value = (bool)_enableCaching.DefaultValue;
            _cacheSize.Value = (int)_cacheSize.DefaultValue;
            _cacheExpirationMinutes.Value = (int)_cacheExpirationMinutes.DefaultValue;
            _enableDebugMode.Value = (bool)_enableDebugMode.DefaultValue;
            _showImGuiPanel.Value = (bool)_showImGuiPanel.DefaultValue;
        }

        public T GetConfigValue<T>(string section, string key, T defaultValue, string description = "")
        {
            return _configFile.Bind(section, key, defaultValue, description).Value;
        }

        public void SetConfigValue<T>(string section, string key, T value)
        {
            var configEntry = _configFile.Bind(section, key, value);
            configEntry.Value = value;
        }
    }
}