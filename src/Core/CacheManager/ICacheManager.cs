using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.Core.CacheManager
{
    public interface ICacheManager
    {
        void Initialize();
        void Shutdown();
        void RefreshSettings();
        
        bool TryGetTranslation(string key, out string translation);
        void CacheTranslation(string key, string translation);
        void InvalidateCache(string key = null);
        void ClearCache();
        
        void LoadFromDisk();
        void SaveToDisk();
        
        int CacheHitCount { get; }
        int CacheMissCount { get; }
        int CacheSize { get; }
        float CacheHitRatio { get; }
        
        void StartPerformanceMonitoring();
        void StopPerformanceMonitoring();
        Dictionary<string, object> GetPerformanceMetrics();
    }
}