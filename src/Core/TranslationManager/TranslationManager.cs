using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GoLani.SPTModTranslator.Data.JsonParser;
using GoLani.SPTModTranslator.Data.FileIO;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.UI.Settings;

namespace GoLani.SPTModTranslator.Core.TranslationManager
{
    public class TranslationManager : ITranslationManager
    {
        private readonly IJsonParser _jsonParser;
        private readonly IFileIOManager _fileIOManager;
        private readonly ILogManager _logManager;
        private readonly ISettingsManager _settingsManager;
        
        private readonly Dictionary<string, TranslationData> _translationFiles;
        private readonly Dictionary<string, string> _dynamicTranslations;
        private readonly List<string> _fallbackChain;
        private readonly object _lockObject = new object();
        
        private string _currentLanguage;
        private bool _isInitialized;
        private int _totalTranslations;
        private int _hitCount;
        private int _missCount;

        public string CurrentLanguage => _currentLanguage;

        public event Action<string> OnTranslationMissing;
        public event Action<string> OnLanguageChanged;
        public event Action OnTranslationsReloaded;

        public TranslationManager(
            IJsonParser jsonParser,
            IFileIOManager fileIOManager,
            ILogManager logManager,
            ISettingsManager settingsManager)
        {
            _jsonParser = jsonParser ?? throw new ArgumentNullException(nameof(jsonParser));
            _fileIOManager = fileIOManager ?? throw new ArgumentNullException(nameof(fileIOManager));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            
            _translationFiles = new Dictionary<string, TranslationData>();
            _dynamicTranslations = new Dictionary<string, string>();
            _fallbackChain = new List<string>();
            
            _currentLanguage = _settingsManager.GetSetting<string>("translation.default_language", "ko_KR");
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                _logManager.SetContext("TranslationManager");
                _logManager.LogInfo("TranslationManager 초기화 시작...");

                SetupDefaultFallbackChain();
                LoadTranslationFiles();
                
                _isInitialized = true;
                _logManager.LogInfo($"TranslationManager 초기화 완료 - {_totalTranslations}개 번역 로드됨");
            }
            catch (Exception ex)
            {
                _logManager.LogError("TranslationManager 초기화 실패", ex);
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
                _logManager.SetContext("TranslationManager");
                _logManager.LogInfo("TranslationManager 종료 중...");

                lock (_lockObject)
                {
                    _translationFiles.Clear();
                    _dynamicTranslations.Clear();
                    _fallbackChain.Clear();
                }
                
                _isInitialized = false;
                _logManager.LogInfo("TranslationManager 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("TranslationManager 종료 중 오류", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void ReloadTranslations()
        {
            if (!_isInitialized)
                return;

            try
            {
                _logManager.LogInfo("번역 파일 재로드 시작...");

                lock (_lockObject)
                {
                    _translationFiles.Clear();
                    _totalTranslations = 0;
                }

                LoadTranslationFiles();
                
                OnTranslationsReloaded?.Invoke();
                _logManager.LogInfo($"번역 파일 재로드 완료 - {_totalTranslations}개 번역 로드됨");
            }
            catch (Exception ex)
            {
                _logManager.LogError("번역 파일 재로드 실패", ex);
            }
        }

        public string GetTranslation(string key, string fallback = null)
        {
            if (!_isInitialized || string.IsNullOrEmpty(key))
                return fallback ?? key;

            try
            {
                lock (_lockObject)
                {
                    if (_dynamicTranslations.TryGetValue(key, out string dynamicTranslation))
                    {
                        _hitCount++;
                        return dynamicTranslation;
                    }

                    foreach (var language in _fallbackChain)
                    {
                        var translation = SearchTranslationInLanguage(key, language);
                        if (!string.IsNullOrEmpty(translation))
                        {
                            _hitCount++;
                            return translation;
                        }
                    }
                }

                _missCount++;
                OnTranslationMissing?.Invoke(key);
                
                _logManager.LogDebug($"번역 누락: {key}");
                return fallback ?? key;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 검색 중 오류: {key}", ex);
                return fallback ?? key;
            }
        }

        public string GetTranslation(string key, params object[] formatArgs)
        {
            var translation = GetTranslation(key);
            
            if (formatArgs != null && formatArgs.Length > 0)
            {
                try
                {
                    return string.Format(translation, formatArgs);
                }
                catch (FormatException ex)
                {
                    _logManager.LogError($"번역 포맷팅 실패: {key}", ex);
                    return translation;
                }
            }
            
            return translation;
        }

        public bool HasTranslation(string key)
        {
            if (!_isInitialized || string.IsNullOrEmpty(key))
                return false;

            lock (_lockObject)
            {
                if (_dynamicTranslations.ContainsKey(key))
                    return true;

                foreach (var language in _fallbackChain)
                {
                    if (HasTranslationInLanguage(key, language))
                        return true;
                }
            }

            return false;
        }

        public void RegisterTranslationFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logManager.LogWarning($"번역 파일이 존재하지 않습니다: {filePath}");
                return;
            }

            try
            {
                var translationData = _jsonParser.ParseTranslationFile(filePath);
                
                lock (_lockObject)
                {
                    _translationFiles[filePath] = translationData;
                    _totalTranslations += translationData.GetAllKeys().Count;
                }
                
                _logManager.LogInfo($"번역 파일 등록됨: {filePath}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 파일 등록 실패: {filePath}", ex);
            }
        }

        public void UnregisterTranslationFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            lock (_lockObject)
            {
                if (_translationFiles.TryGetValue(filePath, out var translationData))
                {
                    _totalTranslations -= translationData.GetAllKeys().Count;
                    _translationFiles.Remove(filePath);
                    
                    _logManager.LogInfo($"번역 파일 등록 해제됨: {filePath}");
                }
            }
        }

        public void SetLanguage(string language)
        {
            if (string.IsNullOrEmpty(language) || language == _currentLanguage)
                return;

            var previousLanguage = _currentLanguage;
            _currentLanguage = language;
            
            SetupFallbackChainForLanguage(language);
            
            OnLanguageChanged?.Invoke(language);
            _logManager.LogInfo($"언어 변경됨: {previousLanguage} -> {language}");
        }

        public void AddDynamicTranslation(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                return;

            lock (_lockObject)
            {
                _dynamicTranslations[key] = value;
            }
            
            _logManager.LogDebug($"동적 번역 추가됨: {key}");
        }

        public void RemoveDynamicTranslation(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            lock (_lockObject)
            {
                _dynamicTranslations.Remove(key);
            }
            
            _logManager.LogDebug($"동적 번역 제거됨: {key}");
        }

        public void SetFallbackChain(params string[] languages)
        {
            if (languages == null || languages.Length == 0)
                return;

            lock (_lockObject)
            {
                _fallbackChain.Clear();
                _fallbackChain.AddRange(languages.Where(lang => !string.IsNullOrEmpty(lang)));
            }
            
            _logManager.LogInfo($"폴백 체인 설정됨: {string.Join(" -> ", languages)}");
        }

        public List<string> GetAvailableLanguages()
        {
            var languages = new HashSet<string>();
            
            lock (_lockObject)
            {
                foreach (var translationData in _translationFiles.Values)
                {
                    if (!string.IsNullOrEmpty(translationData.Language))
                    {
                        languages.Add(translationData.Language);
                    }
                }
            }
            
            return languages.ToList();
        }

        public Dictionary<string, object> GetStatistics()
        {
            lock (_lockObject)
            {
                return new Dictionary<string, object>
                {
                    ["total_translations"] = _totalTranslations,
                    ["dynamic_translations"] = _dynamicTranslations.Count,
                    ["translation_files"] = _translationFiles.Count,
                    ["current_language"] = _currentLanguage,
                    ["fallback_chain"] = _fallbackChain.ToList(),
                    ["available_languages"] = GetAvailableLanguages(),
                    ["hit_count"] = _hitCount,
                    ["miss_count"] = _missCount,
                    ["hit_ratio"] = _hitCount + _missCount == 0 ? 0 : (float)_hitCount / (_hitCount + _missCount),
                    ["last_update"] = DateTime.Now
                };
            }
        }

        private void SetupDefaultFallbackChain()
        {
            var defaultChain = new[]
            {
                _currentLanguage,
                "ko_KR", 
                "en_US", 
                "original"
            };
            
            SetFallbackChain(defaultChain.Distinct().ToArray());
        }

        private void SetupFallbackChainForLanguage(string language)
        {
            var fallbackChain = new List<string> { language };
            
            if (language != "ko_KR")
                fallbackChain.Add("ko_KR");
                
            if (language != "en_US")
                fallbackChain.Add("en_US");
                
            fallbackChain.Add("original");
            
            SetFallbackChain(fallbackChain.ToArray());
        }

        private void LoadTranslationFiles()
        {
            var translationDirectory = _settingsManager.GetSetting<string>("translation.directory", "assets/translations");
            
            if (!Directory.Exists(translationDirectory))
            {
                _logManager.LogWarning($"번역 디렉토리가 존재하지 않습니다: {translationDirectory}");
                return;
            }

            var jsonFiles = Directory.GetFiles(translationDirectory, "*.json", SearchOption.AllDirectories);
            
            foreach (var filePath in jsonFiles)
            {
                RegisterTranslationFile(filePath);
            }
        }

        private string SearchTranslationInLanguage(string key, string language)
        {
            foreach (var translationData in _translationFiles.Values)
            {
                if (translationData.Language == language || 
                    (language == "original" && translationData.Language == _currentLanguage))
                {
                    var translation = translationData.GetTranslation(key);
                    if (!string.IsNullOrEmpty(translation))
                        return translation;
                }
            }
            
            return null;
        }

        private bool HasTranslationInLanguage(string key, string language)
        {
            foreach (var translationData in _translationFiles.Values)
            {
                if (translationData.Language == language || 
                    (language == "original" && translationData.Language == _currentLanguage))
                {
                    if (translationData.HasTranslation(key))
                        return true;
                }
            }
            
            return false;
        }
    }
}