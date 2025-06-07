using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.UI.Settings;
using GoLani.SPTModTranslator.Data.JsonParser;
using GoLani.SPTModTranslator.Core.TranslationManager;

namespace GoLani.SPTModTranslator.Core.AutoUpdate
{
    public class TranslationUpdateManager
    {
        private readonly ILogManager _logManager;
        private readonly ISettingsManager _settingsManager;
        private readonly IJsonParser _jsonParser;
        private readonly ITranslationManager _translationManager;
        private readonly HttpClient _httpClient;
        
        private readonly Dictionary<string, TranslationFileInfo> _trackedFiles;
        private readonly List<TranslationUpdate> _pendingUpdates;
        private readonly Dictionary<string, TranslationQualityMetrics> _qualityMetrics;
        private readonly object _lockObject = new object();
        
        private string _translationUpdateUrl;
        private string _translationDataPath;
        private DateTime _lastUpdateCheck;
        private bool _isInitialized;
        
        public event Action<TranslationUpdateInfo> OnTranslationUpdateAvailable;
        public event Action<TranslationUpdateProgress> OnTranslationUpdateProgress;
        public event Action<TranslationUpdateResult> OnTranslationUpdateCompleted;
        public event Action<string> OnTranslationUpdateError;
        public event Action<TranslationQualityReport> OnQualityAnalysisCompleted;
        
        public TranslationUpdateManager(
            ILogManager logManager,
            ISettingsManager settingsManager,
            IJsonParser jsonParser,
            ITranslationManager translationManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _jsonParser = jsonParser ?? throw new ArgumentNullException(nameof(jsonParser));
            _translationManager = translationManager ?? throw new ArgumentNullException(nameof(translationManager));
            
            _httpClient = new HttpClient();
            _trackedFiles = new Dictionary<string, TranslationFileInfo>();
            _pendingUpdates = new List<TranslationUpdate>();
            _qualityMetrics = new Dictionary<string, TranslationQualityMetrics>();
        }
        
        public void Initialize()
        {
            if (_isInitialized)
                return;
                
            try
            {
                _logManager.SetContext("TranslationUpdateManager");
                _logManager.LogInfo("번역 업데이트 관리자 초기화 시작...");
                
                _translationUpdateUrl = _settingsManager.GetSetting<string>("translation_update.server_url", 
                    "https://sklf-translations.example.com");
                _translationDataPath = _settingsManager.GetSetting<string>("translation.directory", "assets/translations");
                
                LoadTrackedFiles();
                ScanExistingTranslationFiles();
                
                _isInitialized = true;
                _logManager.LogInfo("번역 업데이트 관리자 초기화 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("번역 업데이트 관리자 초기화 실패", ex);
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
                _logManager.SetContext("TranslationUpdateManager");
                _logManager.LogInfo("번역 업데이트 관리자 종료 중...");
                
                SaveTrackedFiles();
                _httpClient?.Dispose();
                
                lock (_lockObject)
                {
                    _trackedFiles.Clear();
                    _pendingUpdates.Clear();
                    _qualityMetrics.Clear();
                }
                
                _isInitialized = false;
                _logManager.LogInfo("번역 업데이트 관리자 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("번역 업데이트 관리자 종료 중 오류", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }
        
        public async Task<List<TranslationUpdateInfo>> CheckForTranslationUpdatesAsync()
        {
            if (!_isInitialized)
                return new List<TranslationUpdateInfo>();
                
            try
            {
                _logManager.LogInfo("번역 업데이트 확인 시작...");
                
                var updates = new List<TranslationUpdateInfo>();
                var manifestUrl = $"{_translationUpdateUrl}/translation-manifest.json";
                
                var manifestJson = await _httpClient.GetStringAsync(manifestUrl);
                var manifest = JsonSerializer.Deserialize<TranslationManifest>(manifestJson);
                
                foreach (var fileInfo in manifest.TranslationFiles)
                {
                    var updateInfo = CheckFileForUpdates(fileInfo);
                    if (updateInfo != null)
                    {
                        updates.Add(updateInfo);
                        OnTranslationUpdateAvailable?.Invoke(updateInfo);
                    }
                }
                
                _lastUpdateCheck = DateTime.Now;
                _logManager.LogInfo($"번역 업데이트 확인 완료 - {updates.Count}개 업데이트 발견");
                
                return updates;
            }
            catch (Exception ex)
            {
                _logManager.LogError("번역 업데이트 확인 실패", ex);
                OnTranslationUpdateError?.Invoke($"번역 업데이트 확인 실패: {ex.Message}");
                return new List<TranslationUpdateInfo>();
            }
        }
        
        public async Task<TranslationUpdateResult> ApplyTranslationUpdateAsync(TranslationUpdateInfo updateInfo)
        {
            var result = new TranslationUpdateResult
            {
                UpdateId = updateInfo.Id,
                FileName = updateInfo.FileName,
                StartTime = DateTime.Now
            };
            
            try
            {
                _logManager.LogInfo($"번역 업데이트 적용 시작: {updateInfo.FileName}");
                
                var progress = new TranslationUpdateProgress
                {
                    UpdateId = updateInfo.Id,
                    FileName = updateInfo.FileName,
                    Stage = TranslationUpdateStage.Downloading,
                    ProgressPercent = 0
                };
                OnTranslationUpdateProgress?.Invoke(progress);
                
                // 1. 번역 파일 다운로드
                var downloadedContent = await DownloadTranslationFileAsync(updateInfo);
                progress.Stage = TranslationUpdateStage.Validating;
                progress.ProgressPercent = 30;
                OnTranslationUpdateProgress?.Invoke(progress);
                
                // 2. 번역 품질 검증
                var qualityReport = await ValidateTranslationQualityAsync(downloadedContent, updateInfo);
                if (!qualityReport.IsValid)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"번역 품질 검증 실패: {string.Join(", ", qualityReport.Issues)}";
                    return result;
                }
                
                progress.Stage = TranslationUpdateStage.Merging;
                progress.ProgressPercent = 60;
                OnTranslationUpdateProgress?.Invoke(progress);
                
                // 3. 기존 번역과 병합
                var mergedContent = await MergeTranslationContentAsync(updateInfo, downloadedContent);
                
                progress.Stage = TranslationUpdateStage.Applying;
                progress.ProgressPercent = 80;
                OnTranslationUpdateProgress?.Invoke(progress);
                
                // 4. 번역 파일 적용
                await ApplyTranslationContentAsync(updateInfo, mergedContent, result);
                
                progress.Stage = TranslationUpdateStage.Completed;
                progress.ProgressPercent = 100;
                OnTranslationUpdateProgress?.Invoke(progress);
                
                result.IsSuccess = true;
                result.QualityScore = qualityReport.OverallScore;
                result.NewTranslationsCount = qualityReport.NewTranslationsCount;
                result.UpdatedTranslationsCount = qualityReport.UpdatedTranslationsCount;
                
                UpdateFileTracking(updateInfo);
                
                _logManager.LogInfo($"번역 업데이트 적용 완료: {updateInfo.FileName}");
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                _logManager.LogError($"번역 업데이트 적용 실패: {updateInfo.FileName}", ex);
                OnTranslationUpdateError?.Invoke($"번역 업데이트 적용 실패: {ex.Message}");
            }
            finally
            {
                result.EndTime = DateTime.Now;
                OnTranslationUpdateCompleted?.Invoke(result);
            }
            
            return result;
        }
        
        public async Task<TranslationQualityReport> AnalyzeTranslationQualityAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new TranslationQualityReport
                    {
                        IsValid = false,
                        Issues = new List<string> { "파일이 존재하지 않습니다." }
                    };
                }
                
                var content = await File.ReadAllTextAsync(filePath);
                var translationData = _jsonParser.ParseTranslationContent(content);
                
                return await AnalyzeTranslationDataQuality(translationData);
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 품질 분석 실패: {filePath}", ex);
                return new TranslationQualityReport
                {
                    IsValid = false,
                    Issues = new List<string> { $"분석 중 오류 발생: {ex.Message}" }
                };
            }
        }
        
        public async Task<bool> ValidateTranslationIntegrityAsync()
        {
            try
            {
                _logManager.LogInfo("번역 무결성 검증 시작...");
                
                var allValid = true;
                var translationFiles = Directory.GetFiles(_translationDataPath, "*.json", SearchOption.AllDirectories);
                
                foreach (var file in translationFiles)
                {
                    var qualityReport = await AnalyzeTranslationQualityAsync(file);
                    
                    if (!qualityReport.IsValid)
                    {
                        allValid = false;
                        _logManager.LogWarning($"번역 무결성 오류 발견: {file} - {string.Join(", ", qualityReport.Issues)}");
                    }
                }
                
                _logManager.LogInfo($"번역 무결성 검증 완료 - 결과: {(allValid ? "정상" : "오류 발견")}");
                return allValid;
            }
            catch (Exception ex)
            {
                _logManager.LogError("번역 무결성 검증 실패", ex);
                return false;
            }
        }
        
        public void TrackTranslationFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;
                
            try
            {
                var fileInfo = new TranslationFileInfo
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    LastModified = File.GetLastWriteTime(filePath),
                    FileSize = new FileInfo(filePath).Length,
                    Checksum = CalculateFileChecksum(filePath)
                };
                
                lock (_lockObject)
                {
                    _trackedFiles[filePath] = fileInfo;
                }
                
                _logManager.LogDebug($"번역 파일 추적 시작: {filePath}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 파일 추적 실패: {filePath}", ex);
            }
        }
        
        public void UntrackTranslationFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;
                
            lock (_lockObject)
            {
                _trackedFiles.Remove(filePath);
            }
            
            _logManager.LogDebug($"번역 파일 추적 중단: {filePath}");
        }
        
        public Dictionary<string, object> GetTranslationUpdateStatistics()
        {
            lock (_lockObject)
            {
                var totalFiles = _trackedFiles.Count;
                var outdatedFiles = _trackedFiles.Values.Count(f => f.IsOutdated);
                var totalTranslations = _qualityMetrics.Values.Sum(m => m.TotalTranslations);
                var averageQuality = _qualityMetrics.Values.Any() ? 
                    _qualityMetrics.Values.Average(m => m.QualityScore) : 0;
                
                return new Dictionary<string, object>
                {
                    ["tracked_files"] = totalFiles,
                    ["outdated_files"] = outdatedFiles,
                    ["pending_updates"] = _pendingUpdates.Count,
                    ["total_translations"] = totalTranslations,
                    ["average_quality"] = averageQuality,
                    ["last_update_check"] = _lastUpdateCheck,
                    ["is_initialized"] = _isInitialized
                };
            }
        }
        
        private TranslationUpdateInfo CheckFileForUpdates(TranslationFileManifest fileManifest)
        {
            var localFilePath = Path.Combine(_translationDataPath, fileManifest.RelativePath);
            
            if (!_trackedFiles.TryGetValue(localFilePath, out var trackedFile))
            {
                // 새 파일
                return new TranslationUpdateInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = fileManifest.FileName,
                    FilePath = localFilePath,
                    Version = fileManifest.Version,
                    DownloadUrl = fileManifest.DownloadUrl,
                    Checksum = fileManifest.Checksum,
                    FileSize = fileManifest.FileSize,
                    UpdateType = TranslationUpdateType.New,
                    ReleaseDate = fileManifest.LastModified,
                    Description = $"새 번역 파일: {fileManifest.FileName}"
                };
            }
            
            if (fileManifest.Checksum != trackedFile.Checksum ||
                fileManifest.LastModified > trackedFile.LastModified)
            {
                // 업데이트된 파일
                return new TranslationUpdateInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = fileManifest.FileName,
                    FilePath = localFilePath,
                    Version = fileManifest.Version,
                    DownloadUrl = fileManifest.DownloadUrl,
                    Checksum = fileManifest.Checksum,
                    FileSize = fileManifest.FileSize,
                    UpdateType = TranslationUpdateType.Updated,
                    ReleaseDate = fileManifest.LastModified,
                    Description = $"번역 파일 업데이트: {fileManifest.FileName}"
                };
            }
            
            return null; // 업데이트 불필요
        }
        
        private async Task<string> DownloadTranslationFileAsync(TranslationUpdateInfo updateInfo)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(updateInfo.DownloadUrl);
                
                // 체크섬 검증
                var actualChecksum = CalculateContentChecksum(response);
                if (actualChecksum != updateInfo.Checksum)
                {
                    throw new InvalidOperationException("다운로드된 파일의 체크섬이 일치하지 않습니다.");
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 파일 다운로드 실패: {updateInfo.FileName}", ex);
                throw;
            }
        }
        
        private async Task<TranslationQualityReport> ValidateTranslationQualityAsync(string content, TranslationUpdateInfo updateInfo)
        {
            try
            {
                var translationData = _jsonParser.ParseTranslationContent(content);
                var report = await AnalyzeTranslationDataQuality(translationData);
                
                report.FileName = updateInfo.FileName;
                report.Version = updateInfo.Version;
                
                OnQualityAnalysisCompleted?.Invoke(report);
                
                return report;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 품질 검증 실패: {updateInfo.FileName}", ex);
                return new TranslationQualityReport
                {
                    IsValid = false,
                    Issues = new List<string> { $"품질 검증 중 오류: {ex.Message}" }
                };
            }
        }
        
        private async Task<TranslationQualityReport> AnalyzeTranslationDataQuality(TranslationData translationData)
        {
            var report = new TranslationQualityReport
            {
                AnalysisDate = DateTime.Now,
                Issues = new List<string>(),
                Warnings = new List<string>()
            };
            
            try
            {
                var allKeys = translationData.GetAllKeys();
                var totalTranslations = allKeys.Count;
                var validTranslations = 0;
                var emptyTranslations = 0;
                var duplicateKeys = new HashSet<string>();
                var checkedKeys = new HashSet<string>();
                
                foreach (var key in allKeys)
                {
                    var translation = translationData.GetTranslation(key);
                    
                    if (checkedKeys.Contains(key))
                    {
                        duplicateKeys.Add(key);
                        continue;
                    }
                    checkedKeys.Add(key);
                    
                    if (string.IsNullOrWhiteSpace(translation))
                    {
                        emptyTranslations++;
                        report.Warnings.Add($"빈 번역: {key}");
                    }
                    else
                    {
                        validTranslations++;
                        
                        // 번역 품질 검사
                        if (await ValidateTranslationContentAsync(key, translation))
                        {
                            // 품질 검사 통과
                        }
                        else
                        {
                            report.Warnings.Add($"품질 검사 실패: {key}");
                        }
                    }
                }
                
                // 중복 키 처리
                foreach (var duplicateKey in duplicateKeys)
                {
                    report.Issues.Add($"중복 키 발견: {duplicateKey}");
                }
                
                // 전체 품질 점수 계산
                var qualityScore = totalTranslations == 0 ? 0 : 
                    (float)validTranslations / totalTranslations * 100;
                
                report.TotalTranslations = totalTranslations;
                report.ValidTranslations = validTranslations;
                report.EmptyTranslations = emptyTranslations;
                report.DuplicateKeys = duplicateKeys.Count;
                report.OverallScore = qualityScore;
                report.IsValid = report.Issues.Count == 0 && qualityScore >= 80; // 80% 이상 유효해야 함
                
                // 품질 등급 결정
                report.QualityGrade = qualityScore switch
                {
                    >= 95 => "A+",
                    >= 90 => "A",
                    >= 85 => "B+",
                    >= 80 => "B",
                    >= 70 => "C",
                    >= 60 => "D",
                    _ => "F"
                };
            }
            catch (Exception ex)
            {
                report.IsValid = false;
                report.Issues.Add($"품질 분석 중 오류: {ex.Message}");
                _logManager.LogError("번역 데이터 품질 분석 실패", ex);
            }
            
            return report;
        }
        
        private async Task<bool> ValidateTranslationContentAsync(string key, string translation)
        {
            // 기본적인 번역 내용 검증 로직
            await Task.Delay(1); // 비동기 작업 시뮬레이션
            
            // 1. 포맷 문자열 일치 검사
            var keyFormatCount = CountFormatStrings(key);
            var translationFormatCount = CountFormatStrings(translation);
            
            if (keyFormatCount != translationFormatCount)
            {
                return false;
            }
            
            // 2. HTML 태그 일치 검사
            if (ContainsHtmlTags(key) != ContainsHtmlTags(translation))
            {
                return false;
            }
            
            // 3. 최소 길이 검사
            if (translation.Length < 1)
            {
                return false;
            }
            
            return true;
        }
        
        private int CountFormatStrings(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
                
            var count = 0;
            count += System.Text.RegularExpressions.Regex.Matches(text, @"\{[0-9]+\}").Count;
            count += System.Text.RegularExpressions.Regex.Matches(text, @"%[ds]").Count;
            
            return count;
        }
        
        private bool ContainsHtmlTags(string text)
        {
            return !string.IsNullOrEmpty(text) && 
                   System.Text.RegularExpressions.Regex.IsMatch(text, @"<[^>]+>");
        }
        
        private async Task<string> MergeTranslationContentAsync(TranslationUpdateInfo updateInfo, string newContent)
        {
            try
            {
                var newTranslationData = _jsonParser.ParseTranslationContent(newContent);
                
                if (!File.Exists(updateInfo.FilePath))
                {
                    // 새 파일인 경우 그대로 반환
                    return newContent;
                }
                
                var existingContent = await File.ReadAllTextAsync(updateInfo.FilePath);
                var existingTranslationData = _jsonParser.ParseTranslationContent(existingContent);
                
                // 기존 번역과 새 번역 병합
                var mergedData = MergeTranslationData(existingTranslationData, newTranslationData);
                
                return _jsonParser.SerializeTranslationData(mergedData);
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 병합 실패: {updateInfo.FileName}", ex);
                throw;
            }
        }
        
        private TranslationData MergeTranslationData(TranslationData existing, TranslationData update)
        {
            var merged = new TranslationData
            {
                Version = update.Version,
                Language = update.Language ?? existing.Language,
                LastModified = DateTime.Now
            };
            
            // 기존 번역 복사
            var existingKeys = existing.GetAllKeys();
            foreach (var key in existingKeys)
            {
                merged.SetTranslation(key, existing.GetTranslation(key));
            }
            
            // 새 번역 추가/업데이트
            var updateKeys = update.GetAllKeys();
            foreach (var key in updateKeys)
            {
                var newTranslation = update.GetTranslation(key);
                if (!string.IsNullOrWhiteSpace(newTranslation))
                {
                    merged.SetTranslation(key, newTranslation);
                }
            }
            
            return merged;
        }
        
        private async Task ApplyTranslationContentAsync(TranslationUpdateInfo updateInfo, string content, TranslationUpdateResult result)
        {
            try
            {
                // 백업 생성
                if (File.Exists(updateInfo.FilePath))
                {
                    var backupPath = $"{updateInfo.FilePath}.backup_{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(updateInfo.FilePath, backupPath);
                    result.BackupFilePath = backupPath;
                }
                
                // 디렉토리 생성 (필요한 경우)
                var directory = Path.GetDirectoryName(updateInfo.FilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 번역 파일 저장
                await File.WriteAllTextAsync(updateInfo.FilePath, content);
                
                // 번역 매니저에 파일 재등록
                _translationManager.UnregisterTranslationFile(updateInfo.FilePath);
                _translationManager.RegisterTranslationFile(updateInfo.FilePath);
                
                result.AppliedFilePath = updateInfo.FilePath;
                
                _logManager.LogInfo($"번역 파일 적용 완료: {updateInfo.FilePath}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 파일 적용 실패: {updateInfo.FilePath}", ex);
                throw;
            }
        }
        
        private void UpdateFileTracking(TranslationUpdateInfo updateInfo)
        {
            try
            {
                var fileInfo = new TranslationFileInfo
                {
                    FilePath = updateInfo.FilePath,
                    FileName = updateInfo.FileName,
                    LastModified = File.GetLastWriteTime(updateInfo.FilePath),
                    FileSize = new FileInfo(updateInfo.FilePath).Length,
                    Checksum = updateInfo.Checksum,
                    Version = updateInfo.Version,
                    IsOutdated = false
                };
                
                lock (_lockObject)
                {
                    _trackedFiles[updateInfo.FilePath] = fileInfo;
                }
                
                SaveTrackedFiles();
            }
            catch (Exception ex)
            {
                _logManager.LogError($"파일 추적 정보 업데이트 실패: {updateInfo.FilePath}", ex);
            }
        }
        
        private void LoadTrackedFiles()
        {
            var trackingFilePath = Path.Combine(_translationDataPath, "file_tracking.json");
            
            if (File.Exists(trackingFilePath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(trackingFilePath);
                    var trackedFiles = JsonSerializer.Deserialize<Dictionary<string, TranslationFileInfo>>(jsonContent);
                    
                    if (trackedFiles != null)
                    {
                        lock (_lockObject)
                        {
                            foreach (var kvp in trackedFiles)
                            {
                                _trackedFiles[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    
                    _logManager.LogInfo($"{_trackedFiles.Count}개 번역 파일 추적 정보 로드됨");
                }
                catch (Exception ex)
                {
                    _logManager.LogError("번역 파일 추적 정보 로드 실패", ex);
                }
            }
        }
        
        private void SaveTrackedFiles()
        {
            try
            {
                var trackingFilePath = Path.Combine(_translationDataPath, "file_tracking.json");
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                
                lock (_lockObject)
                {
                    var jsonContent = JsonSerializer.Serialize(_trackedFiles, jsonOptions);
                    File.WriteAllText(trackingFilePath, jsonContent);
                }
                
                _logManager.LogDebug("번역 파일 추적 정보 저장 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("번역 파일 추적 정보 저장 실패", ex);
            }
        }
        
        private void ScanExistingTranslationFiles()
        {
            try
            {
                if (!Directory.Exists(_translationDataPath))
                    return;
                    
                var translationFiles = Directory.GetFiles(_translationDataPath, "*.json", SearchOption.AllDirectories);
                
                foreach (var file in translationFiles)
                {
                    TrackTranslationFile(file);
                }
                
                _logManager.LogInfo($"{translationFiles.Length}개 기존 번역 파일 스캔 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("기존 번역 파일 스캔 실패", ex);
            }
        }
        
        private string CalculateFileChecksum(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private string CalculateContentChecksum(string content)
        {
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    // 지원 클래스들
    public class TranslationUpdateInfo
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Checksum { get; set; }
        public long FileSize { get; set; }
        public TranslationUpdateType UpdateType { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string Description { get; set; }
        public List<string> Contributors { get; set; }
        public int Priority { get; set; }
        
        public TranslationUpdateInfo()
        {
            Contributors = new List<string>();
            Priority = 100;
        }
    }

    public class TranslationUpdateProgress
    {
        public string UpdateId { get; set; }
        public string FileName { get; set; }
        public TranslationUpdateStage Stage { get; set; }
        public int ProgressPercent { get; set; }
        public string CurrentOperation { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }

    public class TranslationUpdateResult
    {
        public string UpdateId { get; set; }
        public string FileName { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public string AppliedFilePath { get; set; }
        public string BackupFilePath { get; set; }
        public float QualityScore { get; set; }
        public int NewTranslationsCount { get; set; }
        public int UpdatedTranslationsCount { get; set; }
    }

    public class TranslationFileInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime LastModified { get; set; }
        public long FileSize { get; set; }
        public string Checksum { get; set; }
        public string Version { get; set; }
        public bool IsOutdated { get; set; }
    }

    public class TranslationQualityReport
    {
        public string FileName { get; set; }
        public string Version { get; set; }
        public DateTime AnalysisDate { get; set; }
        public bool IsValid { get; set; }
        public float OverallScore { get; set; }
        public string QualityGrade { get; set; }
        public int TotalTranslations { get; set; }
        public int ValidTranslations { get; set; }
        public int EmptyTranslations { get; set; }
        public int DuplicateKeys { get; set; }
        public int NewTranslationsCount { get; set; }
        public int UpdatedTranslationsCount { get; set; }
        public List<string> Issues { get; set; }
        public List<string> Warnings { get; set; }
        
        public TranslationQualityReport()
        {
            Issues = new List<string>();
            Warnings = new List<string>();
        }
    }

    public class TranslationQualityMetrics
    {
        public int TotalTranslations { get; set; }
        public float QualityScore { get; set; }
        public DateTime LastAnalyzed { get; set; }
    }

    public class TranslationManifest
    {
        public string Version { get; set; }
        public DateTime LastModified { get; set; }
        public List<TranslationFileManifest> TranslationFiles { get; set; }
        
        public TranslationManifest()
        {
            TranslationFiles = new List<TranslationFileManifest>();
        }
    }

    public class TranslationFileManifest
    {
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
        public string Checksum { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public string Language { get; set; }
        public List<string> Contributors { get; set; }
        
        public TranslationFileManifest()
        {
            Contributors = new List<string>();
        }
    }

    public class TranslationUpdate
    {
        public string Id { get; set; }
        public string FilePath { get; set; }
        public TranslationUpdateType Type { get; set; }
        public DateTime RequestedAt { get; set; }
        public bool IsProcessed { get; set; }
    }

    public enum TranslationUpdateType
    {
        New = 0,
        Updated = 1,
        Deleted = 2,
        Merged = 3
    }

    public enum TranslationUpdateStage
    {
        Initializing = 0,
        Downloading = 1,
        Validating = 2,
        Merging = 3,
        Applying = 4,
        Completed = 5,
        Failed = 6
    }
}