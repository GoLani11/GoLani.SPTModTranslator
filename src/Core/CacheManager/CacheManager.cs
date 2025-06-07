using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using GoLani.SPTModTranslator.Data.FileIO;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.UI.Settings;

namespace GoLani.SPTModTranslator.Core.CacheManager
{
    public class CacheManager : ICacheManager
    {
        private readonly IFileIOManager _fileIOManager;
        private readonly ILogManager _logManager;
        private readonly ISettingsManager _settingsManager;
        
        private readonly LRUCache<string, string> _memoryCache;
        private readonly object _lockObject = new object();
        private readonly Timer _saveTimer;
        private readonly Timer _cleanupTimer;
        
        private volatile bool _isInitialized;
        private volatile bool _isPerformanceMonitoringEnabled;
        private DateTime _performanceMonitoringStartTime;
        
        private int _cacheHitCount;
        private int _cacheMissCount;
        private readonly Dictionary<string, object> _performanceMetrics;
        private string _diskCacheFilePath;

        public int CacheHitCount => _cacheHitCount;
        public int CacheMissCount => _cacheMissCount;
        public int CacheSize => _memoryCache?.Count ?? 0;
        public float CacheHitRatio => 
            _cacheHitCount + _cacheMissCount == 0 ? 0 : 
            (float)_cacheHitCount / (_cacheHitCount + _cacheMissCount);

        public CacheManager(
            IFileIOManager fileIOManager, 
            ILogManager logManager, 
            ISettingsManager settingsManager)
        {
            _fileIOManager = fileIOManager ?? throw new ArgumentNullException(nameof(fileIOManager));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            
            var maxCacheSize = _settingsManager.GetSetting<int>("cache.memory.max_size", 10000);
            _memoryCache = new LRUCache<string, string>(maxCacheSize);
            
            _performanceMetrics = new Dictionary<string, object>();
            
            var autoSaveInterval = _settingsManager.GetSetting<int>("cache.disk.auto_save_interval_minutes", 5);
            _saveTimer = new Timer(AutoSave, null, TimeSpan.FromMinutes(autoSaveInterval), TimeSpan.FromMinutes(autoSaveInterval));
            
            var cleanupInterval = _settingsManager.GetSetting<int>("cache.memory.cleanup_interval_minutes", 10);
            _cleanupTimer = new Timer(AutoCleanup, null, TimeSpan.FromMinutes(cleanupInterval), TimeSpan.FromMinutes(cleanupInterval));
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                _logManager.SetContext("CacheManager");
                _logManager.LogInfo("CacheManager 초기화 시작...");

                var cacheDirectory = _settingsManager.GetSetting<string>("cache.disk.directory", "cache");
                _diskCacheFilePath = Path.Combine(cacheDirectory, "translation_cache.json");
                
                Directory.CreateDirectory(cacheDirectory);
                
                LoadFromDisk();
                RefreshSettings();
                
                _isInitialized = true;
                _logManager.LogInfo($"CacheManager 초기화 완료 - 메모리 캐시: {CacheSize}개 항목");
            }
            catch (Exception ex)
            {
                _logManager.LogError("CacheManager 초기화 실패", ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            try
            {
                _logManager.SetContext("CacheManager");
                _logManager.LogInfo("CacheManager 종료 중...");

                _saveTimer?.Dispose();
                _cleanupTimer?.Dispose();
                
                SaveToDisk();
                
                lock (_lockObject)
                {
                    _memoryCache.Clear();
                    _performanceMetrics.Clear();
                }
                
                _isInitialized = false;
                _logManager.LogInfo("CacheManager 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("CacheManager 종료 중 오류", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void RefreshSettings()
        {
            if (!_isInitialized)
                return;

            try
            {
                var maxCacheSize = _settingsManager.GetSetting<int>("cache.memory.max_size", 10000);
                _memoryCache.MaxCapacity = maxCacheSize;
                
                _logManager.LogDebug($"캐시 설정 갱신됨 - 최대 크기: {maxCacheSize}");
            }
            catch (Exception ex)
            {
                _logManager.LogError("캐시 설정 갱신 실패", ex);
            }
        }

        public bool TryGetTranslation(string key, out string translation)
        {
            translation = null;
            
            if (!_isInitialized || string.IsNullOrEmpty(key))
            {
                Interlocked.Increment(ref _cacheMissCount);
                return false;
            }

            lock (_lockObject)
            {
                if (_memoryCache.TryGetValue(key, out translation))
                {
                    Interlocked.Increment(ref _cacheHitCount);
                    UpdatePerformanceMetric("cache_hits", _cacheHitCount);
                    return true;
                }
            }

            Interlocked.Increment(ref _cacheMissCount);
            UpdatePerformanceMetric("cache_misses", _cacheMissCount);
            return false;
        }

        public void CacheTranslation(string key, string translation)
        {
            if (!_isInitialized || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(translation))
                return;

            try
            {
                lock (_lockObject)
                {
                    _memoryCache.Set(key, translation);
                }
                
                UpdatePerformanceMetric("cache_sets", _memoryCache.Count);
                _logManager.LogDebug($"번역 캐시됨: {key.Substring(0, Math.Min(key.Length, 50))}...");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 캐싱 실패: {key}", ex);
            }
        }

        public void InvalidateCache(string key = null)
        {
            if (!_isInitialized)
                return;

            try
            {
                lock (_lockObject)
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        _memoryCache.Clear();
                        _logManager.LogInfo("전체 캐시가 무효화되었습니다");
                    }
                    else
                    {
                        _memoryCache.Remove(key);
                        _logManager.LogDebug($"캐시 무효화됨: {key}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError("캐시 무효화 실패", ex);
            }
        }

        public void ClearCache()
        {
            InvalidateCache();
            
            try
            {
                if (File.Exists(_diskCacheFilePath))
                {
                    File.Delete(_diskCacheFilePath);
                }
                
                _cacheHitCount = 0;
                _cacheMissCount = 0;
                
                _logManager.LogInfo("캐시가 완전히 삭제되었습니다");
            }
            catch (Exception ex)
            {
                _logManager.LogError("캐시 삭제 실패", ex);
            }
        }

        public void LoadFromDisk()
        {
            if (!File.Exists(_diskCacheFilePath))
            {
                _logManager.LogDebug("디스크 캐시 파일이 존재하지 않습니다");
                return;
            }

            try
            {
                var cacheData = _fileIOManager.ReadTextFile(_diskCacheFilePath);
                var diskCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(cacheData);
                
                if (diskCache != null)
                {
                    lock (_lockObject)
                    {
                        foreach (var kvp in diskCache.Take(_memoryCache.MaxCapacity))
                        {
                            _memoryCache.Set(kvp.Key, kvp.Value);
                        }
                    }
                    
                    _logManager.LogInfo($"디스크에서 {diskCache.Count}개의 캐시 항목을 로드했습니다");
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError("디스크에서 캐시 로드 실패", ex);
            }
        }

        public void SaveToDisk()
        {
            if (!_isInitialized)
                return;

            try
            {
                Dictionary<string, string> cacheData;
                
                lock (_lockObject)
                {
                    cacheData = _memoryCache.GetAll();
                }
                
                var json = JsonConvert.SerializeObject(cacheData, Formatting.None);
                _fileIOManager.WriteTextFile(_diskCacheFilePath, json);
                
                _logManager.LogDebug($"{cacheData.Count}개의 캐시 항목을 디스크에 저장했습니다");
            }
            catch (Exception ex)
            {
                _logManager.LogError("디스크에 캐시 저장 실패", ex);
            }
        }

        public void StartPerformanceMonitoring()
        {
            _isPerformanceMonitoringEnabled = true;
            _performanceMonitoringStartTime = DateTime.Now;
            
            lock (_lockObject)
            {
                _performanceMetrics["monitoring_start_time"] = _performanceMonitoringStartTime;
                _performanceMetrics["monitoring_enabled"] = true;
            }
            
            _logManager.LogInfo("캐시 성능 모니터링이 시작되었습니다");
        }

        public void StopPerformanceMonitoring()
        {
            _isPerformanceMonitoringEnabled = false;
            
            lock (_lockObject)
            {
                _performanceMetrics["monitoring_enabled"] = false;
                _performanceMetrics["monitoring_end_time"] = DateTime.Now;
            }
            
            _logManager.LogInfo("캐시 성능 모니터링이 중지되었습니다");
        }

        public Dictionary<string, object> GetPerformanceMetrics()
        {
            lock (_lockObject)
            {
                var metrics = new Dictionary<string, object>(_performanceMetrics)
                {
                    ["cache_hit_count"] = _cacheHitCount,
                    ["cache_miss_count"] = _cacheMissCount,
                    ["cache_hit_ratio"] = CacheHitRatio,
                    ["cache_size"] = CacheSize,
                    ["max_cache_capacity"] = _memoryCache.MaxCapacity,
                    ["memory_usage_percentage"] = (float)CacheSize / _memoryCache.MaxCapacity * 100
                };
                
                if (_isPerformanceMonitoringEnabled)
                {
                    metrics["monitoring_duration"] = DateTime.Now - _performanceMonitoringStartTime;
                }
                
                return metrics;
            }
        }

        private void AutoSave(object state)
        {
            try
            {
                SaveToDisk();
            }
            catch (Exception ex)
            {
                _logManager.LogError("자동 저장 실패", ex);
            }
        }

        private void AutoCleanup(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    var currentSize = _memoryCache.Count;
                    var maxSize = _memoryCache.MaxCapacity;
                    var threshold = (int)(maxSize * 0.8);
                    
                    if (currentSize > threshold)
                    {
                        var itemsToRemove = currentSize - threshold;
                        _memoryCache.RemoveOldestItems(itemsToRemove);
                        
                        _logManager.LogDebug($"캐시 정리 완료: {itemsToRemove}개 항목 제거");
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError("자동 정리 실패", ex);
            }
        }

        private void UpdatePerformanceMetric(string key, object value)
        {
            if (!_isPerformanceMonitoringEnabled)
                return;

            lock (_lockObject)
            {
                _performanceMetrics[key] = value;
                _performanceMetrics["last_update_time"] = DateTime.Now;
            }
        }
    }
}