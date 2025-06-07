using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ImGuiNET;
using GoLani.SPTModTranslator.Core.TranslationManager;
using GoLani.SPTModTranslator.Core.CacheManager;
using GoLani.SPTModTranslator.Core.TextDetection;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.UI.Settings;

namespace GoLani.SPTModTranslator.UI.ImGuiPanel
{
    public class TranslationManagementPanel : ITranslationManagementPanel
    {
        private readonly ITranslationManager _translationManager;
        private readonly ICacheManager _cacheManager;
        private readonly ITextInterceptor _textInterceptor;
        private readonly ILogManager _logManager;
        private readonly ISettingsManager _settingsManager;
        
        private readonly List<string> _missingTranslations;
        private readonly Dictionary<string, TranslationPreview> _translationPreviews;
        private readonly object _lockObject = new object();
        
        private bool _isVisible;
        private bool _isEnabled = true;
        private bool _isInitialized;
        
        private int _selectedTab;
        private string _searchFilter = "";
        private string _newTranslationKey = "";
        private string _newTranslationValue = "";
        private bool _showAdvancedSettings;
        private Vector2 _scrollPosition;

        public string PanelTitle => "SKLF 번역 관리 패널";
        public ImGuiPanelFlags Flags { get; set; } = ImGuiPanelFlags.None;
        
        public bool IsVisible 
        { 
            get => _isVisible; 
            set 
            { 
                if (_isVisible != value)
                {
                    _isVisible = value;
                    if (value) OnShow?.Invoke();
                    else OnHide?.Invoke();
                }
            } 
        }
        
        public bool IsEnabled 
        { 
            get => _isEnabled; 
            set => _isEnabled = value; 
        }

        public event Action OnShow;
        public event Action OnHide;
        public event Action<Exception> OnError;

        public TranslationManagementPanel(
            ITranslationManager translationManager,
            ICacheManager cacheManager,
            ITextInterceptor textInterceptor,
            ILogManager logManager,
            ISettingsManager settingsManager)
        {
            _translationManager = translationManager ?? throw new ArgumentNullException(nameof(translationManager));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _textInterceptor = textInterceptor ?? throw new ArgumentNullException(nameof(textInterceptor));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            
            _missingTranslations = new List<string>();
            _translationPreviews = new Dictionary<string, TranslationPreview>();
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                _logManager.SetContext("TranslationManagementPanel");
                _logManager.LogInfo("TranslationManagementPanel 초기화 시작...");

                _translationManager.OnTranslationMissing += AddMissingTranslation;
                _textInterceptor.OnTextIntercepted += OnTextIntercepted;
                
                _isInitialized = true;
                _logManager.LogInfo("TranslationManagementPanel 초기화 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("TranslationManagementPanel 초기화 실패", ex);
                OnError?.Invoke(ex);
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
                _logManager.SetContext("TranslationManagementPanel");
                _logManager.LogInfo("TranslationManagementPanel 종료 중...");

                _translationManager.OnTranslationMissing -= AddMissingTranslation;
                _textInterceptor.OnTextIntercepted -= OnTextIntercepted;
                
                lock (_lockObject)
                {
                    _missingTranslations.Clear();
                    _translationPreviews.Clear();
                }
                
                _isInitialized = false;
                _logManager.LogInfo("TranslationManagementPanel 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("TranslationManagementPanel 종료 중 오류", ex);
                OnError?.Invoke(ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void Render()
        {
            if (!_isVisible || !_isEnabled || !_isInitialized)
                return;

            try
            {
                ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);
                
                if (ImGui.Begin(PanelTitle, ref _isVisible, (ImGuiWindowFlags)Flags))
                {
                    RenderMainTabs();
                }
                ImGui.End();
            }
            catch (Exception ex)
            {
                _logManager.LogError("패널 렌더링 중 오류", ex);
                OnError?.Invoke(ex);
            }
        }

        public void Update()
        {
            if (!_isEnabled || !_isInitialized)
                return;

            try
            {
                HandleKeyboardShortcuts();
                UpdateTranslationPreviews();
            }
            catch (Exception ex)
            {
                _logManager.LogError("패널 업데이트 중 오류", ex);
                OnError?.Invoke(ex);
            }
        }

        public void Show()
        {
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void Toggle()
        {
            IsVisible = !IsVisible;
        }

        public void ShowTranslationPreview(string originalText, string translatedText)
        {
            if (string.IsNullOrEmpty(originalText))
                return;

            lock (_lockObject)
            {
                _translationPreviews[originalText] = new TranslationPreview
                {
                    OriginalText = originalText,
                    TranslatedText = translatedText,
                    Timestamp = DateTime.Now
                };
            }
        }

        public void AddMissingTranslation(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            lock (_lockObject)
            {
                if (!_missingTranslations.Contains(key))
                {
                    _missingTranslations.Add(key);
                    
                    var maxMissing = _settingsManager.GetSetting<int>("ui.max_missing_translations", 1000);
                    if (_missingTranslations.Count > maxMissing)
                    {
                        _missingTranslations.RemoveRange(0, _missingTranslations.Count - maxMissing);
                    }
                }
            }
        }

        public void ShowStatistics()
        {
            _selectedTab = 2;
            Show();
        }

        public void ManageCache()
        {
            _selectedTab = 3;
            Show();
        }

        public void ShowSettings()
        {
            _selectedTab = 4;
            Show();
        }

        public List<string> GetMissingTranslations()
        {
            lock (_lockObject)
            {
                return new List<string>(_missingTranslations);
            }
        }

        public void ClearMissingTranslations()
        {
            lock (_lockObject)
            {
                _missingTranslations.Clear();
            }
        }

        public Dictionary<string, object> GetPanelStatistics()
        {
            lock (_lockObject)
            {
                return new Dictionary<string, object>
                {
                    ["is_visible"] = _isVisible,
                    ["is_enabled"] = _isEnabled,
                    ["missing_translations_count"] = _missingTranslations.Count,
                    ["translation_previews_count"] = _translationPreviews.Count,
                    ["selected_tab"] = _selectedTab,
                    ["last_update"] = DateTime.Now
                };
            }
        }

        private void RenderMainTabs()
        {
            if (ImGui.BeginTabBar("MainTabs"))
            {
                if (ImGui.BeginTabItem("실시간 번역"))
                {
                    _selectedTab = 0;
                    RenderTranslationPreviewTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("누락된 번역"))
                {
                    _selectedTab = 1;
                    RenderMissingTranslationsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("통계"))
                {
                    _selectedTab = 2;
                    RenderStatisticsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("캐시 관리"))
                {
                    _selectedTab = 3;
                    RenderCacheManagementTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("설정"))
                {
                    _selectedTab = 4;
                    RenderSettingsTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void RenderTranslationPreviewTab()
        {
            ImGui.Text("실시간 번역 미리보기");
            ImGui.Separator();

            ImGui.InputText("검색", ref _searchFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button("지우기"))
            {
                _searchFilter = "";
            }

            ImGui.BeginChild("TranslationPreview", new Vector2(0, -30), true);
            
            lock (_lockObject)
            {
                var filteredPreviews = _translationPreviews.Values
                    .Where(p => string.IsNullOrEmpty(_searchFilter) || 
                               p.OriginalText.Contains(_searchFilter) || 
                               p.TranslatedText.Contains(_searchFilter))
                    .OrderByDescending(p => p.Timestamp)
                    .Take(100);

                foreach (var preview in filteredPreviews)
                {
                    ImGui.Text($"[{preview.Timestamp:HH:mm:ss}]");
                    ImGui.Text($"원본: {preview.OriginalText}");
                    ImGui.Text($"번역: {preview.TranslatedText}");
                    ImGui.Separator();
                }
            }
            
            ImGui.EndChild();

            if (ImGui.Button("미리보기 지우기"))
            {
                lock (_lockObject)
                {
                    _translationPreviews.Clear();
                }
            }
        }

        private void RenderMissingTranslationsTab()
        {
            ImGui.Text("누락된 번역 목록");
            ImGui.Separator();

            lock (_lockObject)
            {
                ImGui.Text($"총 {_missingTranslations.Count}개의 누락된 번역");
            }

            ImGui.Separator();
            ImGui.Text("새 번역 추가:");
            ImGui.InputText("키", ref _newTranslationKey, 256);
            ImGui.InputText("값", ref _newTranslationValue, 256);
            
            if (ImGui.Button("추가") && !string.IsNullOrEmpty(_newTranslationKey) && !string.IsNullOrEmpty(_newTranslationValue))
            {
                _translationManager.AddDynamicTranslation(_newTranslationKey, _newTranslationValue);
                _newTranslationKey = "";
                _newTranslationValue = "";
            }

            ImGui.Separator();
            ImGui.BeginChild("MissingTranslations", new Vector2(0, -30), true);
            
            lock (_lockObject)
            {
                foreach (var missing in _missingTranslations)
                {
                    ImGui.Text(missing);
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"제거##{missing}"))
                    {
                        _missingTranslations.Remove(missing);
                        break;
                    }
                }
            }
            
            ImGui.EndChild();

            if (ImGui.Button("모든 누락 번역 지우기"))
            {
                ClearMissingTranslations();
            }
        }

        private void RenderStatisticsTab()
        {
            ImGui.Text("번역 시스템 통계");
            ImGui.Separator();

            var translationStats = _translationManager.GetStatistics();
            var cacheStats = _cacheManager.GetPerformanceMetrics();
            var interceptorStats = _textInterceptor.GetStatistics();

            ImGui.Text("번역 매니저:");
            foreach (var kvp in translationStats)
            {
                ImGui.Text($"  {kvp.Key}: {kvp.Value}");
            }

            ImGui.Separator();
            ImGui.Text("캐시 매니저:");
            foreach (var kvp in cacheStats)
            {
                ImGui.Text($"  {kvp.Key}: {kvp.Value}");
            }

            ImGui.Separator();
            ImGui.Text("텍스트 인터셉터:");
            ImGui.Text($"  총 인터셉션: {interceptorStats.TotalInterceptions}");
            ImGui.Text($"  성공한 번역: {interceptorStats.SuccessfulTranslations}");
            ImGui.Text($"  필터링된 텍스트: {interceptorStats.FilteredTexts}");
            ImGui.Text($"  오류: {interceptorStats.Errors}");
            ImGui.Text($"  평균 처리 시간: {interceptorStats.AverageProcessingTime.TotalMilliseconds:F2}ms");
        }

        private void RenderCacheManagementTab()
        {
            ImGui.Text("캐시 관리");
            ImGui.Separator();

            ImGui.Text($"캐시 크기: {_cacheManager.CacheSize}");
            ImGui.Text($"캐시 히트율: {_cacheManager.CacheHitRatio:P2}");
            ImGui.Text($"캐시 히트: {_cacheManager.CacheHitCount}");
            ImGui.Text($"캐시 미스: {_cacheManager.CacheMissCount}");

            ImGui.Separator();

            if (ImGui.Button("캐시 지우기"))
            {
                _cacheManager.ClearCache();
            }
            ImGui.SameLine();
            if (ImGui.Button("디스크에 저장"))
            {
                _cacheManager.SaveToDisk();
            }
            ImGui.SameLine();
            if (ImGui.Button("디스크에서 로드"))
            {
                _cacheManager.LoadFromDisk();
            }
        }

        private void RenderSettingsTab()
        {
            ImGui.Text("SKLF 설정");
            ImGui.Separator();

            var isInterceptionEnabled = _textInterceptor.IsInterceptionEnabled;
            if (ImGui.Checkbox("텍스트 인터셉션 활성화", ref isInterceptionEnabled))
            {
                if (isInterceptionEnabled)
                    _textInterceptor.EnableInterception();
                else
                    _textInterceptor.DisableInterception();
            }

            ImGui.Separator();
            ImGui.Checkbox("고급 설정 표시", ref _showAdvancedSettings);

            if (_showAdvancedSettings)
            {
                ImGui.Text("현재 언어: " + _translationManager.CurrentLanguage);
                
                var availableLanguages = _translationManager.GetAvailableLanguages();
                if (availableLanguages.Count > 0)
                {
                    if (ImGui.BeginCombo("언어 선택", _translationManager.CurrentLanguage))
                    {
                        foreach (var language in availableLanguages)
                        {
                            if (ImGui.Selectable(language, language == _translationManager.CurrentLanguage))
                            {
                                _translationManager.SetLanguage(language);
                            }
                        }
                        ImGui.EndCombo();
                    }
                }

                if (ImGui.Button("번역 파일 재로드"))
                {
                    _translationManager.ReloadTranslations();
                }
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.T))
            {
                Toggle();
            }
        }

        private void UpdateTranslationPreviews()
        {
            lock (_lockObject)
            {
                var maxPreviews = _settingsManager.GetSetting<int>("ui.max_translation_previews", 100);
                if (_translationPreviews.Count > maxPreviews)
                {
                    var oldestKeys = _translationPreviews
                        .OrderBy(kvp => kvp.Value.Timestamp)
                        .Take(_translationPreviews.Count - maxPreviews)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in oldestKeys)
                    {
                        _translationPreviews.Remove(key);
                    }
                }
            }
        }

        private void OnTextIntercepted(TextInterceptionEventArgs args)
        {
            if (args.WasTranslated)
            {
                ShowTranslationPreview(args.OriginalText, args.TranslatedText);
            }
        }
    }

    public class TranslationPreview
    {
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public DateTime Timestamp { get; set; }
    }
}