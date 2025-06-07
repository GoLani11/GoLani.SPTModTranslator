using System;
using BepInEx.Configuration;

namespace GoLani.SPTModTranslator.UI.Settings
{
    public interface ISettingsManager
    {
        event Action<string> OnLanguageChanged;
        event Action OnCacheSettingsChanged;
        event Action OnPerformanceSettingsChanged;
        
        string CurrentLanguage { get; set; }
        bool EnableCaching { get; set; }
        int CacheSize { get; set; }
        int CacheExpirationMinutes { get; set; }
        bool EnableDebugMode { get; set; }
        bool ShowImGuiPanel { get; set; }
        
        void Initialize();
        void Save();
        void Reset();
        
        T GetConfigValue<T>(string section, string key, T defaultValue, string description = "");
        void SetConfigValue<T>(string section, string key, T value);
    }
}