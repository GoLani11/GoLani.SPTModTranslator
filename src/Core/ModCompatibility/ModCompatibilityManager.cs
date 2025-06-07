using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.UI.Settings;
using GoLani.SPTModTranslator.Data.FileIO;

namespace GoLani.SPTModTranslator.Core.ModCompatibility
{
    public class ModCompatibilityManager : IModCompatibilityManager
    {
        private readonly ILogManager _logManager;
        private readonly ISettingsManager _settingsManager;
        private readonly IFileIOManager _fileIOManager;
        
        private readonly Dictionary<string, ModInfo> _detectedMods;
        private readonly Dictionary<string, ModTranslationPattern> _translationPatterns;
        private readonly Dictionary<string, ModCompatibilityResult> _compatibilityResults;
        private readonly List<string> _appliedPatches;
        private readonly object _lockObject = new object();
        
        private bool _isInitialized;
        private string _bepInExPluginsPath;
        private string _sptModsPath;
        private string _compatibilityDataPath;
        
        public bool IsInitialized => _isInitialized;
        
        public event Action<ModInfo> OnModDetected;
        public event Action<ModInfo> OnModRemoved;
        public event Action<string> OnCompatibilityIssueDetected;
        public event Action OnModAnalysisCompleted;
        
        public ModCompatibilityManager(
            ILogManager logManager,
            ISettingsManager settingsManager,
            IFileIOManager fileIOManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _fileIOManager = fileIOManager ?? throw new ArgumentNullException(nameof(fileIOManager));
            
            _detectedMods = new Dictionary<string, ModInfo>();
            _translationPatterns = new Dictionary<string, ModTranslationPattern>();
            _compatibilityResults = new Dictionary<string, ModCompatibilityResult>();
            _appliedPatches = new List<string>();
        }
        
        public void Initialize()
        {
            if (_isInitialized)
                return;
                
            try
            {
                _logManager.SetContext("ModCompatibilityManager");
                _logManager.LogInfo("모드 호환성 관리자 초기화 시작...");
                
                InitializePaths();
                LoadTranslationPatterns();
                ScanForMods();
                AnalyzeModCompatibility();
                
                _isInitialized = true;
                _logManager.LogInfo($"모드 호환성 관리자 초기화 완료 - {_detectedMods.Count}개 모드 감지됨");
            }
            catch (Exception ex)
            {
                _logManager.LogError("모드 호환성 관리자 초기화 실패", ex);
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
                _logManager.SetContext("ModCompatibilityManager");
                _logManager.LogInfo("모드 호환성 관리자 종료 중...");
                
                RemoveAllCompatibilityPatches();
                SaveCompatibilityData();
                
                lock (_lockObject)
                {
                    _detectedMods.Clear();
                    _translationPatterns.Clear();
                    _compatibilityResults.Clear();
                    _appliedPatches.Clear();
                }
                
                _isInitialized = false;
                _logManager.LogInfo("모드 호환성 관리자 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("모드 호환성 관리자 종료 중 오류", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }
        
        public void ScanForMods()
        {
            if (!_isInitialized)
                return;
                
            try
            {
                _logManager.LogInfo("모드 스캔 시작...");
                
                var newMods = new List<ModInfo>();
                
                ScanBepInExPlugins(newMods);
                ScanSPTMods(newMods);
                
                lock (_lockObject)
                {
                    var removedMods = _detectedMods.Values
                        .Where(mod => !newMods.Any(newMod => newMod.Id == mod.Id))
                        .ToList();
                        
                    foreach (var removedMod in removedMods)
                    {
                        _detectedMods.Remove(removedMod.Id);
                        OnModRemoved?.Invoke(removedMod);
                        _logManager.LogInfo($"모드 제거됨: {removedMod.Name}");
                    }
                    
                    foreach (var newMod in newMods)
                    {
                        if (!_detectedMods.ContainsKey(newMod.Id))
                        {
                            _detectedMods[newMod.Id] = newMod;
                            OnModDetected?.Invoke(newMod);
                            _logManager.LogInfo($"새 모드 감지됨: {newMod.Name}");
                        }
                        else
                        {
                            _detectedMods[newMod.Id] = newMod;
                        }
                    }
                }
                
                _logManager.LogInfo($"모드 스캔 완료 - {newMods.Count}개 모드 감지됨");
            }
            catch (Exception ex)
            {
                _logManager.LogError("모드 스캔 중 오류 발생", ex);
            }
        }
        
        public void AnalyzeModCompatibility()
        {
            if (!_isInitialized)
                return;
                
            try
            {
                _logManager.LogInfo("모드 호환성 분석 시작...");
                
                var startTime = DateTime.Now;
                
                lock (_lockObject)
                {
                    _compatibilityResults.Clear();
                    
                    foreach (var mod in _detectedMods.Values)
                    {
                        var result = AnalyzeSingleMod(mod);
                        _compatibilityResults[mod.Id] = result;
                        
                        if (result.HasCriticalIssues)
                        {
                            OnCompatibilityIssueDetected?.Invoke($"모드 {mod.Name}에서 심각한 호환성 문제 발견");
                        }
                    }
                }
                
                var duration = DateTime.Now - startTime;
                OnModAnalysisCompleted?.Invoke();
                
                _logManager.LogInfo($"모드 호환성 분석 완료 - 소요시간: {duration.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                _logManager.LogError("모드 호환성 분석 중 오류 발생", ex);
            }
        }
        
        public void RefreshModList()
        {
            ScanForMods();
            AnalyzeModCompatibility();
        }
        
        public List<ModInfo> GetDetectedMods()
        {
            lock (_lockObject)
            {
                return _detectedMods.Values.ToList();
            }
        }
        
        public List<ModInfo> GetCompatibleMods()
        {
            lock (_lockObject)
            {
                return _detectedMods.Values
                    .Where(mod => _compatibilityResults.TryGetValue(mod.Id, out var result) &&
                                  (result.CompatibilityLevel == ModCompatibilityLevel.FullyCompatible ||
                                   result.CompatibilityLevel == ModCompatibilityLevel.PartiallyCompatible))
                    .ToList();
            }
        }
        
        public List<ModInfo> GetIncompatibleMods()
        {
            lock (_lockObject)
            {
                return _detectedMods.Values
                    .Where(mod => _compatibilityResults.TryGetValue(mod.Id, out var result) &&
                                  (result.CompatibilityLevel == ModCompatibilityLevel.Incompatible ||
                                   result.CompatibilityLevel == ModCompatibilityLevel.Conflicting))
                    .ToList();
            }
        }
        
        public List<string> GetCompatibilityIssues()
        {
            lock (_lockObject)
            {
                var issues = new List<string>();
                
                foreach (var result in _compatibilityResults.Values)
                {
                    issues.AddRange(result.Issues);
                }
                
                return issues.Distinct().ToList();
            }
        }
        
        public ModCompatibilityResult CheckModCompatibility(ModInfo modInfo)
        {
            if (modInfo == null)
                return null;
                
            lock (_lockObject)
            {
                if (_compatibilityResults.TryGetValue(modInfo.Id, out var existingResult))
                {
                    return existingResult;
                }
                
                var result = AnalyzeSingleMod(modInfo);
                _compatibilityResults[modInfo.Id] = result;
                return result;
            }
        }
        
        public bool IsModSupported(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;
                
            lock (_lockObject)
            {
                return _compatibilityResults.TryGetValue(modId, out var result) &&
                       result.IsSupported &&
                       result.CompatibilityLevel != ModCompatibilityLevel.Incompatible;
            }
        }
        
        public bool IsModConflicting(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;
                
            lock (_lockObject)
            {
                return _compatibilityResults.TryGetValue(modId, out var result) &&
                       result.HasConflicts;
            }
        }
        
        public void RegisterModTranslationPattern(string modId, ModTranslationPattern pattern)
        {
            if (string.IsNullOrEmpty(modId) || pattern == null)
                return;
                
            lock (_lockObject)
            {
                _translationPatterns[modId] = pattern;
            }
            
            _logManager.LogInfo($"모드 번역 패턴 등록됨: {modId}");
        }
        
        public void UnregisterModTranslationPattern(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return;
                
            lock (_lockObject)
            {
                _translationPatterns.Remove(modId);
            }
            
            _logManager.LogInfo($"모드 번역 패턴 등록 해제됨: {modId}");
        }
        
        public ModTranslationPattern GetModTranslationPattern(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return null;
                
            lock (_lockObject)
            {
                return _translationPatterns.TryGetValue(modId, out var pattern) ? pattern : null;
            }
        }
        
        public void ApplyCompatibilityPatch(string modId)
        {
            if (string.IsNullOrEmpty(modId) || _appliedPatches.Contains(modId))
                return;
                
            try
            {
                var result = CheckModCompatibility(_detectedMods.TryGetValue(modId, out var mod) ? mod : null);
                
                if (result?.RequiresPatch == true)
                {
                    foreach (var patchName in result.RequiredPatches)
                    {
                        ApplySpecificPatch(modId, patchName);
                    }
                    
                    _appliedPatches.Add(modId);
                    _logManager.LogInfo($"모드 호환성 패치 적용됨: {modId}");
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"모드 호환성 패치 적용 실패: {modId}", ex);
            }
        }
        
        public void RemoveCompatibilityPatch(string modId)
        {
            if (string.IsNullOrEmpty(modId) || !_appliedPatches.Contains(modId))
                return;
                
            try
            {
                RemoveSpecificPatches(modId);
                _appliedPatches.Remove(modId);
                _logManager.LogInfo($"모드 호환성 패치 제거됨: {modId}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"모드 호환성 패치 제거 실패: {modId}", ex);
            }
        }
        
        public bool HasCompatibilityPatch(string modId)
        {
            return !string.IsNullOrEmpty(modId) && _appliedPatches.Contains(modId);
        }
        
        public Dictionary<string, object> GetCompatibilityStatistics()
        {
            lock (_lockObject)
            {
                var totalMods = _detectedMods.Count;
                var compatibleMods = GetCompatibleMods().Count;
                var incompatibleMods = GetIncompatibleMods().Count;
                var modsRequiringPatches = _compatibilityResults.Values
                    .Count(r => r.RequiresPatch);
                var appliedPatchesCount = _appliedPatches.Count;
                
                return new Dictionary<string, object>
                {
                    ["total_mods"] = totalMods,
                    ["compatible_mods"] = compatibleMods,
                    ["incompatible_mods"] = incompatibleMods,
                    ["mods_requiring_patches"] = modsRequiringPatches,
                    ["applied_patches"] = appliedPatchesCount,
                    ["translation_patterns"] = _translationPatterns.Count,
                    ["compatibility_ratio"] = totalMods == 0 ? 0 : (float)compatibleMods / totalMods,
                    ["last_scan"] = DateTime.Now
                };
            }
        }
        
        public void GenerateCompatibilityReport()
        {
            try
            {
                var reportPath = Path.Combine(_compatibilityDataPath, $"compatibility_report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                var statistics = GetCompatibilityStatistics();
                
                var report = new
                {
                    GeneratedAt = DateTime.Now,
                    Statistics = statistics,
                    DetectedMods = GetDetectedMods(),
                    CompatibilityResults = _compatibilityResults,
                    AppliedPatches = _appliedPatches
                };
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var jsonContent = JsonSerializer.Serialize(report, jsonOptions);
                File.WriteAllText(reportPath, jsonContent);
                
                _logManager.LogInfo($"호환성 보고서 생성됨: {reportPath}");
            }
            catch (Exception ex)
            {
                _logManager.LogError("호환성 보고서 생성 실패", ex);
            }
        }
        
        private void InitializePaths()
        {
            _bepInExPluginsPath = _settingsManager.GetSetting<string>("mod_compatibility.bepinex_plugins_path", "BepInEx/plugins");
            _sptModsPath = _settingsManager.GetSetting<string>("mod_compatibility.spt_mods_path", "user/mods");
            _compatibilityDataPath = _settingsManager.GetSetting<string>("mod_compatibility.data_path", "assets/compatibility");
            
            if (!Directory.Exists(_compatibilityDataPath))
            {
                Directory.CreateDirectory(_compatibilityDataPath);
            }
        }
        
        private void LoadTranslationPatterns()
        {
            var patternsPath = Path.Combine(_compatibilityDataPath, "translation_patterns.json");
            
            if (File.Exists(patternsPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(patternsPath);
                    var patterns = JsonSerializer.Deserialize<Dictionary<string, ModTranslationPattern>>(jsonContent);
                    
                    if (patterns != null)
                    {
                        lock (_lockObject)
                        {
                            foreach (var kvp in patterns)
                            {
                                _translationPatterns[kvp.Key] = kvp.Value;
                            }
                        }
                        
                        _logManager.LogInfo($"{patterns.Count}개 번역 패턴 로드됨");
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogError("번역 패턴 로드 실패", ex);
                }
            }
        }
        
        private void ScanBepInExPlugins(List<ModInfo> mods)
        {
            if (!Directory.Exists(_bepInExPluginsPath))
                return;
                
            var pluginFiles = Directory.GetFiles(_bepInExPluginsPath, "*.dll", SearchOption.AllDirectories);
            
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var modInfo = AnalyzeBepInExPlugin(pluginFile);
                    if (modInfo != null)
                    {
                        mods.Add(modInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogWarning($"BepInEx 플러그인 분석 실패: {pluginFile}", ex);
                }
            }
        }
        
        private void ScanSPTMods(List<ModInfo> mods)
        {
            if (!Directory.Exists(_sptModsPath))
                return;
                
            var modDirectories = Directory.GetDirectories(_sptModsPath);
            
            foreach (var modDirectory in modDirectories)
            {
                try
                {
                    var modInfo = AnalyzeSPTMod(modDirectory);
                    if (modInfo != null)
                    {
                        mods.Add(modInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogWarning($"SPT 모드 분석 실패: {modDirectory}", ex);
                }
            }
        }
        
        private ModInfo AnalyzeBepInExPlugin(string pluginPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(pluginPath);
                var bepInExPluginAttribute = assembly.GetCustomAttribute<BepInEx.BepInPluginAttribute>();
                
                if (bepInExPluginAttribute != null)
                {
                    return new ModInfo
                    {
                        Id = bepInExPluginAttribute.GUID,
                        Name = bepInExPluginAttribute.Name,
                        Version = bepInExPluginAttribute.Version.ToString(),
                        Type = ModType.BepInExPlugin,
                        AssemblyPath = pluginPath,
                        InstallPath = Path.GetDirectoryName(pluginPath),
                        LastModified = File.GetLastWriteTime(pluginPath),
                        CompatibilityLevel = ModCompatibilityLevel.Unknown
                    };
                }
            }
            catch (Exception ex)
            {
                _logManager.LogDebug($"BepInEx 플러그인 분석 중 오류: {pluginPath}", ex);
            }
            
            return null;
        }
        
        private ModInfo AnalyzeSPTMod(string modDirectory)
        {
            var packageJsonPath = Path.Combine(modDirectory, "package.json");
            
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(packageJsonPath);
                    var packageInfo = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                    
                    return new ModInfo
                    {
                        Id = packageInfo.GetProperty("name").GetString(),
                        Name = packageInfo.GetProperty("displayName").GetString(),
                        Version = packageInfo.GetProperty("version").GetString(),
                        Author = packageInfo.TryGetProperty("author", out var author) ? author.GetString() : "Unknown",
                        Description = packageInfo.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                        Type = ModType.SPTMod,
                        InstallPath = modDirectory,
                        LastModified = Directory.GetLastWriteTime(modDirectory),
                        CompatibilityLevel = ModCompatibilityLevel.Unknown
                    };
                }
                catch (Exception ex)
                {
                    _logManager.LogDebug($"SPT 모드 분석 중 오류: {modDirectory}", ex);
                }
            }
            
            return null;
        }
        
        private ModCompatibilityResult AnalyzeSingleMod(ModInfo modInfo)
        {
            var result = new ModCompatibilityResult
            {
                ModId = modInfo.Id,
                CompatibilityLevel = ModCompatibilityLevel.Unknown,
                IsSupported = false
            };
            
            var analysisStartTime = DateTime.Now;
            
            try
            {
                CheckBasicCompatibility(modInfo, result);
                CheckTextFrameworkCompatibility(modInfo, result);
                CheckDependencies(modInfo, result);
                CheckConflicts(modInfo, result);
                DetermineCompatibilityLevel(result);
                
                result.AnalysisDuration = DateTime.Now - analysisStartTime;
                
                _logManager.LogDebug($"모드 호환성 분석 완료: {modInfo.Name} - {result.CompatibilityLevel}");
            }
            catch (Exception ex)
            {
                result.AddIssue($"분석 중 오류 발생: {ex.Message}");
                result.CompatibilityLevel = ModCompatibilityLevel.Unknown;
                _logManager.LogError($"모드 호환성 분석 실패: {modInfo.Name}", ex);
            }
            
            return result;
        }
        
        private void CheckBasicCompatibility(ModInfo modInfo, ModCompatibilityResult result)
        {
            if (modInfo.Type == ModType.BepInExPlugin || modInfo.Type == ModType.SPTMod)
            {
                result.IsSupported = true;
            }
            else
            {
                result.AddIssue("지원되지 않는 모드 타입입니다.");
            }
            
            if (!File.Exists(modInfo.AssemblyPath) && modInfo.Type == ModType.BepInExPlugin)
            {
                result.AddIssue("어셈블리 파일을 찾을 수 없습니다.");
            }
            
            if (!Directory.Exists(modInfo.InstallPath))
            {
                result.AddIssue("설치 경로를 찾을 수 없습니다.");
            }
        }
        
        private void CheckTextFrameworkCompatibility(ModInfo modInfo, ModCompatibilityResult result)
        {
            var knownCompatibleFrameworks = new[]
            {
                "UnityEngine.UI.Text",
                "TMPro.TextMeshProUGUI",
                "UnityEngine.GUIText",
                "UnityEngine.TextMesh"
            };
            
            if (_translationPatterns.TryGetValue(modInfo.Id, out var pattern))
            {
                foreach (var framework in pattern.TextFrameworkPatterns)
                {
                    if (knownCompatibleFrameworks.Contains(framework.FrameworkName))
                    {
                        modInfo.SupportedTextFrameworks.Add(framework.FrameworkName);
                    }
                }
                
                if (modInfo.SupportedTextFrameworks.Count > 0)
                {
                    modInfo.HasTranslationSupport = true;
                    result.AddRecommendation("번역 패턴이 정의되어 있어 번역이 지원됩니다.");
                }
            }
            else
            {
                result.AddWarning("번역 패턴이 정의되지 않았습니다. 수동 분석이 필요할 수 있습니다.");
                result.AddRequiredPatch("AutoDetectionPatch");
            }
        }
        
        private void CheckDependencies(ModInfo modInfo, ModCompatibilityResult result)
        {
            foreach (var dependency in modInfo.Dependencies)
            {
                if (!_detectedMods.ContainsKey(dependency))
                {
                    result.AddIssue($"필수 의존성이 누락됨: {dependency}");
                }
            }
        }
        
        private void CheckConflicts(ModInfo modInfo, ModCompatibilityResult result)
        {
            foreach (var conflictingModId in modInfo.ConflictsWith)
            {
                if (_detectedMods.ContainsKey(conflictingModId))
                {
                    result.AddConflictingMod(conflictingModId);
                    result.AddIssue($"충돌하는 모드가 설치됨: {conflictingModId}");
                }
            }
        }
        
        private void DetermineCompatibilityLevel(ModCompatibilityResult result)
        {
            if (result.HasConflicts)
            {
                result.CompatibilityLevel = ModCompatibilityLevel.Conflicting;
            }
            else if (result.Issues.Count > 0)
            {
                result.CompatibilityLevel = ModCompatibilityLevel.Incompatible;
            }
            else if (result.RequiresPatch)
            {
                result.CompatibilityLevel = ModCompatibilityLevel.RequiresPatch;
            }
            else if (result.Warnings.Count > 0)
            {
                result.CompatibilityLevel = ModCompatibilityLevel.PartiallyCompatible;
            }
            else
            {
                result.CompatibilityLevel = ModCompatibilityLevel.FullyCompatible;
            }
        }
        
        private void ApplySpecificPatch(string modId, string patchName)
        {
            _logManager.LogInfo($"패치 적용 중: {modId} - {patchName}");
        }
        
        private void RemoveSpecificPatches(string modId)
        {
            _logManager.LogInfo($"패치 제거 중: {modId}");
        }
        
        private void RemoveAllCompatibilityPatches()
        {
            foreach (var modId in _appliedPatches.ToList())
            {
                RemoveCompatibilityPatch(modId);
            }
        }
        
        private void SaveCompatibilityData()
        {
            try
            {
                var dataPath = Path.Combine(_compatibilityDataPath, "compatibility_cache.json");
                var data = new
                {
                    SavedAt = DateTime.Now,
                    CompatibilityResults = _compatibilityResults,
                    AppliedPatches = _appliedPatches
                };
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var jsonContent = JsonSerializer.Serialize(data, jsonOptions);
                File.WriteAllText(dataPath, jsonContent);
                
                _logManager.LogInfo("호환성 데이터 저장 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("호환성 데이터 저장 실패", ex);
            }
        }
    }
}