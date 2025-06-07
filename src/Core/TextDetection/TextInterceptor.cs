using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using GoLani.SPTModTranslator.Core.TranslationManager;
using GoLani.SPTModTranslator.Core.CacheManager;
using GoLani.SPTModTranslator.Utils.Logging;

namespace GoLani.SPTModTranslator.Core.TextDetection
{
    public class TextInterceptor : ITextInterceptor
    {
        private readonly ITranslationManager _translationManager;
        private readonly ICacheManager _cacheManager;
        private readonly ILogManager _logManager;
        
        private readonly Dictionary<string, ITextSource> _textSources;
        private readonly List<ITextFilter> _textFilters;
        private readonly TextInterceptionStatistics _statistics;
        private readonly object _lockObject = new object();
        
        private bool _isInitialized;
        private bool _isInterceptionEnabled;

        public event Action<TextInterceptionEventArgs> OnTextIntercepted;
        public event Action<TextInterceptionEventArgs> OnTextFiltered;
        public event Action<Exception> OnInterceptionError;

        public bool IsInterceptionEnabled => _isInterceptionEnabled;

        public TextInterceptor(
            ITranslationManager translationManager, 
            ICacheManager cacheManager, 
            ILogManager logManager)
        {
            _translationManager = translationManager ?? throw new ArgumentNullException(nameof(translationManager));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            
            _textSources = new Dictionary<string, ITextSource>();
            _textFilters = new List<ITextFilter>();
            _statistics = new TextInterceptionStatistics
            {
                StatisticsStartTime = DateTime.Now
            };
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                _logManager.SetContext("TextInterceptor");
                _logManager.LogInfo("TextInterceptor 초기화 시작...");

                InitializeBuiltInFilters();
                InitializeBuiltInTextSources();

                _isInterceptionEnabled = true;
                _isInitialized = true;

                _logManager.LogInfo("TextInterceptor 초기화 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("TextInterceptor 초기화 실패", ex);
                OnInterceptionError?.Invoke(ex);
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
                _logManager.SetContext("TextInterceptor");
                _logManager.LogInfo("TextInterceptor 종료 중...");

                _isInterceptionEnabled = false;
                
                lock (_lockObject)
                {
                    _textSources.Clear();
                    _textFilters.Clear();
                }

                _isInitialized = false;
                _logManager.LogInfo("TextInterceptor 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("TextInterceptor 종료 중 오류", ex);
                OnInterceptionError?.Invoke(ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public string InterceptText(string originalText, TextInterceptionContext context = null)
        {
            if (!_isInitialized || !_isInterceptionEnabled || string.IsNullOrEmpty(originalText))
                return originalText;

            var stopwatch = Stopwatch.StartNew();
            string translatedText = originalText;
            bool wasTranslated = false;
            string translationSource = "none";

            try
            {
                context = context ?? new TextInterceptionContext();
                
                lock (_lockObject)
                {
                    _statistics.TotalInterceptions++;
                    _statistics.LastInterceptionTime = DateTime.Now;
                    
                    UpdateComponentStats(context.ComponentType);
                    UpdateSourceModuleStats(context.SourceModule);
                }

                if (!ShouldInterceptText(originalText, context))
                {
                    lock (_lockObject)
                    {
                        _statistics.FilteredTexts++;
                    }
                    
                    OnTextFiltered?.Invoke(new TextInterceptionEventArgs
                    {
                        OriginalText = originalText,
                        TranslatedText = originalText,
                        Context = context,
                        ProcessingTime = stopwatch.Elapsed,
                        WasTranslated = false,
                        TranslationSource = "filtered"
                    });
                    
                    return originalText;
                }

                if (_cacheManager.TryGetTranslation(originalText, out string cachedTranslation))
                {
                    translatedText = cachedTranslation;
                    wasTranslated = true;
                    translationSource = "cache";
                }
                else
                {
                    var translation = _translationManager.GetTranslation(originalText);
                    if (!string.IsNullOrEmpty(translation) && translation != originalText)
                    {
                        translatedText = translation;
                        wasTranslated = true;
                        translationSource = "translation_manager";
                        
                        _cacheManager.CacheTranslation(originalText, translation);
                    }
                    else
                    {
                        var customTranslation = TryGetCustomTranslation(originalText, context);
                        if (!string.IsNullOrEmpty(customTranslation))
                        {
                            translatedText = customTranslation;
                            wasTranslated = true;
                            translationSource = "custom_source";
                            
                            _cacheManager.CacheTranslation(originalText, customTranslation);
                        }
                    }
                }

                translatedText = ApplyTextFilters(translatedText, context);

                if (wasTranslated)
                {
                    lock (_lockObject)
                    {
                        _statistics.SuccessfulTranslations++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"텍스트 인터셉션 중 오류: {originalText}", ex);
                
                lock (_lockObject)
                {
                    _statistics.Errors++;
                }
                
                OnInterceptionError?.Invoke(ex);
                return originalText;
            }
            finally
            {
                stopwatch.Stop();
                
                lock (_lockObject)
                {
                    UpdateAverageProcessingTime(stopwatch.Elapsed);
                }

                OnTextIntercepted?.Invoke(new TextInterceptionEventArgs
                {
                    OriginalText = originalText,
                    TranslatedText = translatedText,
                    Context = context,
                    ProcessingTime = stopwatch.Elapsed,
                    WasTranslated = wasTranslated,
                    TranslationSource = translationSource
                });
            }

            return translatedText;
        }

        public bool ShouldInterceptText(string text, TextInterceptionContext context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (text.Length > 1000)
                return false;

            if (IsNumericOrSymbolic(text))
                return false;

            if (IsUrl(text))
                return false;

            if (IsPath(text))
                return false;

            foreach (var filter in _textFilters.OrderBy(f => f.Priority))
            {
                if (filter.ShouldFilter(text, context))
                    return false;
            }

            return true;
        }

        public void RegisterTextSource(string sourceId, ITextSource textSource)
        {
            if (string.IsNullOrEmpty(sourceId) || textSource == null)
                return;

            lock (_lockObject)
            {
                _textSources[sourceId] = textSource;
            }
            
            _logManager.LogDebug($"텍스트 소스 등록됨: {sourceId}");
        }

        public void UnregisterTextSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId))
                return;

            lock (_lockObject)
            {
                _textSources.Remove(sourceId);
            }
            
            _logManager.LogDebug($"텍스트 소스 등록 해제됨: {sourceId}");
        }

        public void AddTextFilter(ITextFilter filter)
        {
            if (filter == null)
                return;

            lock (_lockObject)
            {
                if (!_textFilters.Any(f => f.FilterId == filter.FilterId))
                {
                    _textFilters.Add(filter);
                    _textFilters.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                }
            }
            
            _logManager.LogDebug($"텍스트 필터 추가됨: {filter.FilterId}");
        }

        public void RemoveTextFilter(ITextFilter filter)
        {
            if (filter == null)
                return;

            lock (_lockObject)
            {
                _textFilters.RemoveAll(f => f.FilterId == filter.FilterId);
            }
            
            _logManager.LogDebug($"텍스트 필터 제거됨: {filter.FilterId}");
        }

        public void EnableInterception()
        {
            _isInterceptionEnabled = true;
            _logManager.LogInfo("텍스트 인터셉션 활성화됨");
        }

        public void DisableInterception()
        {
            _isInterceptionEnabled = false;
            _logManager.LogInfo("텍스트 인터셉션 비활성화됨");
        }

        public TextInterceptionStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new TextInterceptionStatistics
                {
                    TotalInterceptions = _statistics.TotalInterceptions,
                    SuccessfulTranslations = _statistics.SuccessfulTranslations,
                    FilteredTexts = _statistics.FilteredTexts,
                    Errors = _statistics.Errors,
                    AverageProcessingTime = _statistics.AverageProcessingTime,
                    ComponentTypeStats = new Dictionary<string, int>(_statistics.ComponentTypeStats),
                    SourceModuleStats = new Dictionary<string, int>(_statistics.SourceModuleStats),
                    StatisticsStartTime = _statistics.StatisticsStartTime,
                    LastInterceptionTime = _statistics.LastInterceptionTime
                };
            }
        }

        public void ResetStatistics()
        {
            lock (_lockObject)
            {
                _statistics.TotalInterceptions = 0;
                _statistics.SuccessfulTranslations = 0;
                _statistics.FilteredTexts = 0;
                _statistics.Errors = 0;
                _statistics.AverageProcessingTime = TimeSpan.Zero;
                _statistics.ComponentTypeStats.Clear();
                _statistics.SourceModuleStats.Clear();
                _statistics.StatisticsStartTime = DateTime.Now;
                _statistics.LastInterceptionTime = DateTime.MinValue;
            }
            
            _logManager.LogInfo("텍스트 인터셉션 통계가 재설정되었습니다");
        }

        private void InitializeBuiltInFilters()
        {
            AddTextFilter(new WhitespaceFilter());
            AddTextFilter(new SpecialCharacterFilter());
            AddTextFilter(new MinLengthFilter(2));
        }

        private void InitializeBuiltInTextSources()
        {
        }

        private string TryGetCustomTranslation(string originalText, TextInterceptionContext context)
        {
            var sources = _textSources.Values.OrderByDescending(s => s.Priority);
            
            foreach (var source in sources)
            {
                if (source.CanProvideText(context))
                {
                    var customText = source.GetText(context);
                    if (!string.IsNullOrEmpty(customText))
                        return customText;
                }
            }
            
            return null;
        }

        private string ApplyTextFilters(string text, TextInterceptionContext context)
        {
            string result = text;
            
            foreach (var filter in _textFilters.OrderBy(f => f.Priority))
            {
                result = filter.ApplyFilter(result, context);
            }
            
            return result;
        }

        private static bool IsNumericOrSymbolic(string text)
        {
            return Regex.IsMatch(text, @"^[\d\s\.\,\-\+\%\$\€\£\¥\₩]+$");
        }

        private static bool IsUrl(string text)
        {
            return Uri.TryCreate(text, UriKind.Absolute, out _);
        }

        private static bool IsPath(string text)
        {
            return text.Contains('/') || text.Contains('\\') || text.Contains("://");
        }

        private void UpdateComponentStats(string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
                return;

            if (_statistics.ComponentTypeStats.ContainsKey(componentType))
                _statistics.ComponentTypeStats[componentType]++;
            else
                _statistics.ComponentTypeStats[componentType] = 1;
        }

        private void UpdateSourceModuleStats(string sourceModule)
        {
            if (string.IsNullOrEmpty(sourceModule))
                return;

            if (_statistics.SourceModuleStats.ContainsKey(sourceModule))
                _statistics.SourceModuleStats[sourceModule]++;
            else
                _statistics.SourceModuleStats[sourceModule] = 1;
        }

        private void UpdateAverageProcessingTime(TimeSpan processingTime)
        {
            if (_statistics.TotalInterceptions == 1)
            {
                _statistics.AverageProcessingTime = processingTime;
            }
            else
            {
                var totalTicks = _statistics.AverageProcessingTime.Ticks * (_statistics.TotalInterceptions - 1) + processingTime.Ticks;
                _statistics.AverageProcessingTime = new TimeSpan(totalTicks / _statistics.TotalInterceptions);
            }
        }
    }

    public class WhitespaceFilter : ITextFilter
    {
        public string FilterId => "whitespace_filter";
        public int Priority => 1;

        public bool ShouldFilter(string text, TextInterceptionContext context)
        {
            return string.IsNullOrWhiteSpace(text) || text.Trim().Length == 0;
        }

        public string ApplyFilter(string text, TextInterceptionContext context)
        {
            return text;
        }
    }

    public class SpecialCharacterFilter : ITextFilter
    {
        public string FilterId => "special_character_filter";
        public int Priority => 2;

        public bool ShouldFilter(string text, TextInterceptionContext context)
        {
            return Regex.IsMatch(text, @"^[\W\d_]+$");
        }

        public string ApplyFilter(string text, TextInterceptionContext context)
        {
            return text;
        }
    }

    public class MinLengthFilter : ITextFilter
    {
        private readonly int _minLength;

        public MinLengthFilter(int minLength)
        {
            _minLength = minLength;
        }

        public string FilterId => $"min_length_filter_{_minLength}";
        public int Priority => 3;

        public bool ShouldFilter(string text, TextInterceptionContext context)
        {
            return text.Length < _minLength;
        }

        public string ApplyFilter(string text, TextInterceptionContext context)
        {
            return text;
        }
    }
}