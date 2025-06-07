using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.Core.TranslationManager
{
    public interface ITranslationManager
    {
        void Initialize();
        void Shutdown();
        void ReloadTranslations();
        
        string GetTranslation(string key, string fallback = null);
        string GetTranslation(string key, params object[] formatArgs);
        bool HasTranslation(string key);
        
        void RegisterTranslationFile(string filePath);
        void UnregisterTranslationFile(string filePath);
        
        void SetLanguage(string language);
        string CurrentLanguage { get; }
        
        void AddDynamicTranslation(string key, string value);
        void RemoveDynamicTranslation(string key);
        
        void SetFallbackChain(params string[] languages);
        List<string> GetAvailableLanguages();
        
        event Action<string> OnTranslationMissing;
        event Action<string> OnLanguageChanged;
        event Action OnTranslationsReloaded;
        
        Dictionary<string, object> GetStatistics();
    }
}