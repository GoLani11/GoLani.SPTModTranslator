using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GoLani.SPTModTranslator.Utils.Logging;
using GoLani.SPTModTranslator.UI.Settings;
using GoLani.SPTModTranslator.Data.FileIO;

namespace GoLani.SPTModTranslator.Core.AutoUpdate
{
    public class AutoUpdateManager : IAutoUpdateManager
    {
        private readonly ILogManager _logManager;
        private readonly ISettingsManager _settingsManager;
        private readonly IFileIOManager _fileIOManager;
        private readonly HttpClient _httpClient;
        
        private readonly List<UpdateInfo> _availableUpdates;
        private readonly List<UpdateHistory> _updateHistory;
        private readonly Timer _updateTimer;
        private readonly object _lockObject = new object();
        
        private bool _isInitialized;
        private bool _isUpdateInProgress;
        private CancellationTokenSource _updateCancellationTokenSource;
        private UpdateConfiguration _configuration;
        private string _updateDataPath;
        private string _backupPath;
        
        public bool IsInitialized => _isInitialized;
        public bool IsUpdateInProgress => _isUpdateInProgress;
        public UpdateConfiguration Configuration 
        { 
            get => _configuration; 
            set 
            { 
                _configuration = value ?? throw new ArgumentNullException(nameof(value));
                SaveConfiguration();
            } 
        }
        
        public event Action<UpdateInfo> OnUpdateAvailable;
        public event Action<UpdateProgress> OnUpdateProgress;
        public event Action<UpdateResult> OnUpdateCompleted;
        public event Action<string> OnUpdateError;
        public event Action OnUpdateCancelled;
        
        public AutoUpdateManager(
            ILogManager logManager,
            ISettingsManager settingsManager,
            IFileIOManager fileIOManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _fileIOManager = fileIOManager ?? throw new ArgumentNullException(nameof(fileIOManager));
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SKLF-AutoUpdate/1.0");
            
            _availableUpdates = new List<UpdateInfo>();
            _updateHistory = new List<UpdateHistory>();
            _configuration = new UpdateConfiguration();
        }
        
        public void Initialize()
        {
            if (_isInitialized)
                return;
                
            try
            {
                _logManager.SetContext("AutoUpdateManager");
                _logManager.LogInfo("자동 업데이트 관리자 초기화 시작...");
                
                InitializePaths();
                LoadConfiguration();
                LoadUpdateHistory();
                SetupUpdateTimer();
                
                _isInitialized = true;
                _logManager.LogInfo("자동 업데이트 관리자 초기화 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("자동 업데이트 관리자 초기화 실패", ex);
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
                _logManager.SetContext("AutoUpdateManager");
                _logManager.LogInfo("자동 업데이트 관리자 종료 중...");
                
                CancelUpdate();
                _updateTimer?.Dispose();
                SaveConfiguration();
                SaveUpdateHistory();
                _httpClient?.Dispose();
                
                _isInitialized = false;
                _logManager.LogInfo("자동 업데이트 관리자 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("자동 업데이트 관리자 종료 중 오류", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }
        
        public async Task<List<UpdateInfo>> CheckForUpdatesAsync()
        {
            if (!_isInitialized)
                return new List<UpdateInfo>();
                
            try
            {
                _logManager.LogInfo("업데이트 확인 시작...");
                
                var manifestUrl = $"{_configuration.UpdateServerUrl}/manifest.json";
                var manifestJson = await _httpClient.GetStringAsync(manifestUrl);
                var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson);
                
                lock (_lockObject)
                {
                    _availableUpdates.Clear();
                    
                    foreach (var updateInfo in manifest.Updates)
                    {
                        if (IsUpdateApplicable(updateInfo))
                        {
                            _availableUpdates.Add(updateInfo);
                            OnUpdateAvailable?.Invoke(updateInfo);
                            _logManager.LogInfo($"새 업데이트 발견: {updateInfo.Name} v{updateInfo.Version}");
                        }
                    }
                }
                
                _logManager.LogInfo($"업데이트 확인 완료 - {_availableUpdates.Count}개 업데이트 사용 가능");
                return GetAvailableUpdates();
            }
            catch (Exception ex)
            {
                _logManager.LogError("업데이트 확인 실패", ex);
                OnUpdateError?.Invoke($"업데이트 확인 실패: {ex.Message}");
                return new List<UpdateInfo>();
            }
        }
        
        public async Task<UpdateResult> DownloadAndApplyUpdatesAsync()
        {
            var availableUpdates = GetAvailableUpdates();
            
            if (availableUpdates.Count == 0)
            {
                return new UpdateResult
                {
                    IsSuccess = true,
                    ErrorMessage = "적용할 업데이트가 없습니다."
                };
            }
            
            var overallResult = new UpdateResult
            {
                UpdateId = Guid.NewGuid().ToString(),
                StartTime = DateTime.Now
            };
            
            foreach (var updateInfo in availableUpdates.OrderBy(u => u.Priority))
            {
                var result = await DownloadAndApplyUpdateAsync(updateInfo);
                
                if (!result.IsSuccess)
                {
                    overallResult.IsSuccess = false;
                    overallResult.ErrorMessage = result.ErrorMessage;
                    overallResult.FailedFiles.AddRange(result.FailedFiles);
                    break;
                }
                
                overallResult.AppliedFiles.AddRange(result.AppliedFiles);
                overallResult.BackedUpFiles.AddRange(result.BackedUpFiles);
            }
            
            overallResult.CompletionTime = DateTime.Now;
            OnUpdateCompleted?.Invoke(overallResult);
            
            return overallResult;
        }
        
        public async Task<UpdateResult> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo)
        {
            if (_isUpdateInProgress)
            {
                return new UpdateResult
                {
                    IsSuccess = false,
                    ErrorMessage = "다른 업데이트가 진행 중입니다."
                };
            }
            
            _isUpdateInProgress = true;
            _updateCancellationTokenSource = new CancellationTokenSource();
            
            var result = new UpdateResult
            {
                UpdateId = updateInfo.Id,
                StartTime = DateTime.Now
            };
            
            try
            {
                _logManager.LogInfo($"업데이트 시작: {updateInfo.Name} v{updateInfo.Version}");
                
                await ExecuteUpdateStagesAsync(updateInfo, result);
                
                if (!_updateCancellationTokenSource.Token.IsCancellationRequested)
                {
                    result.IsSuccess = true;
                    RecordUpdateHistory(updateInfo, result);
                    _logManager.LogInfo($"업데이트 완료: {updateInfo.Name}");
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "업데이트가 취소되었습니다.";
                    OnUpdateCancelled?.Invoke();
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                _logManager.LogError($"업데이트 실패: {updateInfo.Name}", ex);
                OnUpdateError?.Invoke($"업데이트 실패: {ex.Message}");
            }
            finally
            {
                result.CompletionTime = DateTime.Now;
                _isUpdateInProgress = false;
                _updateCancellationTokenSource?.Dispose();
                _updateCancellationTokenSource = null;
                OnUpdateCompleted?.Invoke(result);
            }
            
            return result;
        }
        
        public void CancelUpdate()
        {
            if (_isUpdateInProgress && _updateCancellationTokenSource != null)
            {
                _updateCancellationTokenSource.Cancel();
                _logManager.LogInfo("업데이트 취소 요청됨");
            }
        }
        
        public void PauseUpdate()
        {
            _logManager.LogInfo("업데이트 일시정지 (현재 미구현)");
        }
        
        public void ResumeUpdate()
        {
            _logManager.LogInfo("업데이트 재개 (현재 미구현)");
        }
        
        public List<UpdateInfo> GetAvailableUpdates()
        {
            lock (_lockObject)
            {
                return _availableUpdates.ToList();
            }
        }
        
        public List<UpdateHistory> GetUpdateHistory()
        {
            return _updateHistory.ToList();
        }
        
        public UpdateInfo GetLatestUpdateInfo()
        {
            lock (_lockObject)
            {
                return _availableUpdates
                    .OrderByDescending(u => u.ReleaseDate)
                    .FirstOrDefault();
            }
        }
        
        public async Task<bool> RollbackToVersionAsync(string version)
        {
            try
            {
                var historyEntry = _updateHistory
                    .Where(h => h.Version == version && h.IsSuccess && !string.IsNullOrEmpty(h.BackupId))
                    .OrderByDescending(h => h.AppliedDate)
                    .FirstOrDefault();
                    
                if (historyEntry == null)
                {
                    _logManager.LogWarning($"롤백할 버전을 찾을 수 없습니다: {version}");
                    return false;
                }
                
                return await RestoreBackupAsync(historyEntry.BackupId);
            }
            catch (Exception ex)
            {
                _logManager.LogError($"버전 롤백 실패: {version}", ex);
                return false;
            }
        }
        
        public async Task<bool> CreateBackupAsync()
        {
            try
            {
                var backupId = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                var backupDirectory = Path.Combine(_backupPath, backupId);
                
                Directory.CreateDirectory(backupDirectory);
                
                var translationsPath = _settingsManager.GetSetting<string>("translation.directory", "assets/translations");
                
                if (Directory.Exists(translationsPath))
                {
                    await CopyDirectoryAsync(translationsPath, Path.Combine(backupDirectory, "translations"));
                }
                
                var backupInfo = new
                {
                    BackupId = backupId,
                    CreatedAt = DateTime.Now,
                    Description = "수동 백업"
                };
                
                var backupInfoPath = Path.Combine(backupDirectory, "backup_info.json");
                var backupInfoJson = JsonSerializer.Serialize(backupInfo, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(backupInfoPath, backupInfoJson);
                
                _logManager.LogInfo($"백업 생성 완료: {backupId}");
                return true;
            }
            catch (Exception ex)
            {
                _logManager.LogError("백업 생성 실패", ex);
                return false;
            }
        }
        
        public async Task<bool> RestoreBackupAsync(string backupId)
        {
            try
            {
                var backupDirectory = Path.Combine(_backupPath, backupId);
                
                if (!Directory.Exists(backupDirectory))
                {
                    _logManager.LogWarning($"백업을 찾을 수 없습니다: {backupId}");
                    return false;
                }
                
                var translationsPath = _settingsManager.GetSetting<string>("translation.directory", "assets/translations");
                var backupTranslationsPath = Path.Combine(backupDirectory, "translations");
                
                if (Directory.Exists(backupTranslationsPath))
                {
                    if (Directory.Exists(translationsPath))
                    {
                        Directory.Delete(translationsPath, true);
                    }
                    
                    await CopyDirectoryAsync(backupTranslationsPath, translationsPath);
                }
                
                _logManager.LogInfo($"백업 복원 완료: {backupId}");
                return true;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"백업 복원 실패: {backupId}", ex);
                return false;
            }
        }
        
        public void SetAutoUpdateEnabled(bool enabled)
        {
            _configuration.AutoUpdateEnabled = enabled;
            SaveConfiguration();
            _logManager.LogInfo($"자동 업데이트 {(enabled ? "활성화" : "비활성화")}됨");
        }
        
        public void SetUpdateChannel(UpdateChannel channel)
        {
            _configuration.Channel = channel;
            SaveConfiguration();
            _logManager.LogInfo($"업데이트 채널 변경됨: {channel}");
        }
        
        public void SetUpdateInterval(TimeSpan interval)
        {
            _configuration.CheckInterval = interval;
            SaveConfiguration();
            SetupUpdateTimer();
            _logManager.LogInfo($"업데이트 확인 간격 변경됨: {interval}");
        }
        
        public bool IsVersionNewer(string currentVersion, string newVersion)
        {
            try
            {
                var current = new Version(currentVersion);
                var newest = new Version(newVersion);
                return newest > current;
            }
            catch
            {
                return false;
            }
        }
        
        public bool IsUpdateRequired(UpdateInfo updateInfo)
        {
            return updateInfo.IsCritical || updateInfo.IsSecurityUpdate || 
                   updateInfo.Priority >= UpdatePriority.High;
        }
        
        public bool IsBackupAvailable(string backupId)
        {
            var backupDirectory = Path.Combine(_backupPath, backupId);
            return Directory.Exists(backupDirectory);
        }
        
        public Dictionary<string, object> GetUpdateStatistics()
        {
            lock (_lockObject)
            {
                var successfulUpdates = _updateHistory.Count(h => h.IsSuccess);
                var failedUpdates = _updateHistory.Count(h => !h.IsSuccess);
                var lastUpdate = _updateHistory.OrderByDescending(h => h.AppliedDate).FirstOrDefault();
                
                return new Dictionary<string, object>
                {
                    ["available_updates"] = _availableUpdates.Count,
                    ["update_history_count"] = _updateHistory.Count,
                    ["successful_updates"] = successfulUpdates,
                    ["failed_updates"] = failedUpdates,
                    ["success_rate"] = _updateHistory.Count == 0 ? 0 : (float)successfulUpdates / _updateHistory.Count,
                    ["last_update"] = lastUpdate?.AppliedDate,
                    ["last_check"] = DateTime.Now,
                    ["auto_update_enabled"] = _configuration.AutoUpdateEnabled,
                    ["update_channel"] = _configuration.Channel.ToString()
                };
            }
        }
        
        public void ValidateIntegrity()
        {
            try
            {
                _logManager.LogInfo("무결성 검증 시작...");
                
                var translationsPath = _settingsManager.GetSetting<string>("translation.directory", "assets/translations");
                
                if (!Directory.Exists(translationsPath))
                {
                    _logManager.LogWarning("번역 디렉토리가 존재하지 않습니다.");
                    return;
                }
                
                var jsonFiles = Directory.GetFiles(translationsPath, "*.json", SearchOption.AllDirectories);
                var corruptedFiles = new List<string>();
                
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        JsonSerializer.Deserialize<JsonElement>(content);
                    }
                    catch
                    {
                        corruptedFiles.Add(file);
                    }
                }
                
                if (corruptedFiles.Count > 0)
                {
                    _logManager.LogWarning($"{corruptedFiles.Count}개의 손상된 파일이 발견되었습니다.");
                    OnUpdateError?.Invoke($"손상된 파일 감지: {string.Join(", ", corruptedFiles.Take(3))}");
                }
                else
                {
                    _logManager.LogInfo("무결성 검증 완료 - 모든 파일이 정상입니다.");
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError("무결성 검증 실패", ex);
            }
        }
        
        public void CleanupOldBackups(int maxBackupsToKeep = 5)
        {
            try
            {
                if (!Directory.Exists(_backupPath))
                    return;
                    
                var backupDirectories = Directory.GetDirectories(_backupPath)
                    .Select(dir => new { Path = dir, Created = Directory.GetCreationTime(dir) })
                    .OrderByDescending(x => x.Created)
                    .ToList();
                    
                var backupsToDelete = backupDirectories.Skip(maxBackupsToKeep);
                
                foreach (var backup in backupsToDelete)
                {
                    Directory.Delete(backup.Path, true);
                    _logManager.LogInfo($"오래된 백업 삭제됨: {Path.GetFileName(backup.Path)}");
                }
                
                if (backupsToDelete.Any())
                {
                    _logManager.LogInfo($"{backupsToDelete.Count()}개의 오래된 백업이 정리되었습니다.");
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError("백업 정리 실패", ex);
            }
        }
        
        private void InitializePaths()
        {
            _updateDataPath = _settingsManager.GetSetting<string>("auto_update.data_path", "assets/updates");
            _backupPath = Path.Combine(_updateDataPath, "backups");
            
            Directory.CreateDirectory(_updateDataPath);
            Directory.CreateDirectory(_backupPath);
        }
        
        private void LoadConfiguration()
        {
            var configPath = Path.Combine(_updateDataPath, "update_config.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(configPath);
                    _configuration = JsonSerializer.Deserialize<UpdateConfiguration>(jsonContent) ?? new UpdateConfiguration();
                }
                catch (Exception ex)
                {
                    _logManager.LogWarning("업데이트 설정 로드 실패, 기본값 사용", ex);
                    _configuration = new UpdateConfiguration();
                }
            }
            
            _configuration.UpdateServerUrl = _settingsManager.GetSetting<string>("auto_update.server_url", 
                "https://sklf-updates.example.com");
        }
        
        private void SaveConfiguration()
        {
            try
            {
                var configPath = Path.Combine(_updateDataPath, "update_config.json");
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var jsonContent = JsonSerializer.Serialize(_configuration, jsonOptions);
                File.WriteAllText(configPath, jsonContent);
            }
            catch (Exception ex)
            {
                _logManager.LogError("업데이트 설정 저장 실패", ex);
            }
        }
        
        private void LoadUpdateHistory()
        {
            var historyPath = Path.Combine(_updateDataPath, "update_history.json");
            
            if (File.Exists(historyPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(historyPath);
                    var history = JsonSerializer.Deserialize<List<UpdateHistory>>(jsonContent);
                    
                    if (history != null)
                    {
                        _updateHistory.Clear();
                        _updateHistory.AddRange(history);
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogWarning("업데이트 기록 로드 실패", ex);
                }
            }
        }
        
        private void SaveUpdateHistory()
        {
            try
            {
                var historyPath = Path.Combine(_updateDataPath, "update_history.json");
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var jsonContent = JsonSerializer.Serialize(_updateHistory, jsonOptions);
                File.WriteAllText(historyPath, jsonContent);
            }
            catch (Exception ex)
            {
                _logManager.LogError("업데이트 기록 저장 실패", ex);
            }
        }
        
        private void SetupUpdateTimer()
        {
            _updateTimer?.Dispose();
            
            if (_configuration.AutoUpdateEnabled && _configuration.CheckInterval > TimeSpan.Zero)
            {
                _updateTimer = new Timer(async _ => await CheckForUpdatesAsync(), null, 
                    _configuration.CheckInterval, _configuration.CheckInterval);
            }
        }
        
        private bool IsUpdateApplicable(UpdateInfo updateInfo)
        {
            if (_configuration.Channel < updateInfo.Channel)
                return false;
                
            if (!_configuration.AllowPreReleases && updateInfo.Channel != UpdateChannel.Stable)
                return false;
                
            if (_configuration.IncludedLanguages.Count > 0 && 
                !updateInfo.TargetLanguages.Any(lang => _configuration.IncludedLanguages.Contains(lang)))
                return false;
                
            if (_configuration.ExcludedMods.Any(mod => updateInfo.SupportedMods.Contains(mod)))
                return false;
                
            var existingHistory = _updateHistory.FirstOrDefault(h => h.Version == updateInfo.Version);
            return existingHistory == null || !existingHistory.IsSuccess;
        }
        
        private async Task ExecuteUpdateStagesAsync(UpdateInfo updateInfo, UpdateResult result)
        {
            var progress = new UpdateProgress
            {
                UpdateId = updateInfo.Id,
                StartTime = DateTime.Now
            };
            
            // Stage 1: 매니페스트 다운로드
            progress.Stage = UpdateStage.DownloadingManifest;
            progress.CurrentOperation = "매니페스트 다운로드 중...";
            OnUpdateProgress?.Invoke(progress);
            
            // Stage 2: 파일 다운로드
            progress.Stage = UpdateStage.DownloadingFiles;
            progress.CurrentOperation = "파일 다운로드 중...";
            progress.TotalBytes = updateInfo.FileSize;
            OnUpdateProgress?.Invoke(progress);
            
            var downloadedFile = await DownloadFileWithProgressAsync(updateInfo.DownloadUrl, progress);
            
            // Stage 3: 파일 검증
            progress.Stage = UpdateStage.ValidatingFiles;
            progress.CurrentOperation = "파일 무결성 검증 중...";
            OnUpdateProgress?.Invoke(progress);
            
            if (_configuration.VerifyChecksums && !await ValidateFileAsync(downloadedFile, updateInfo))
            {
                throw new InvalidOperationException("파일 무결성 검증 실패");
            }
            
            // Stage 4: 백업 생성
            if (_configuration.CreateBackupBeforeUpdate)
            {
                progress.Stage = UpdateStage.CreatingBackup;
                progress.CurrentOperation = "백업 생성 중...";
                OnUpdateProgress?.Invoke(progress);
                
                result.BackupId = $"auto_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                await CreateBackupAsync();
            }
            
            // Stage 5: 업데이트 적용
            progress.Stage = UpdateStage.ApplyingUpdate;
            progress.CurrentOperation = "업데이트 적용 중...";
            OnUpdateProgress?.Invoke(progress);
            
            await ApplyUpdateFileAsync(downloadedFile, result);
            
            // Stage 6: 완료
            progress.Stage = UpdateStage.Completed;
            progress.CurrentOperation = "업데이트 완료";
            progress.PercentComplete = 100;
            progress.CompletionTime = DateTime.Now;
            OnUpdateProgress?.Invoke(progress);
        }
        
        private async Task<string> DownloadFileWithProgressAsync(string url, UpdateProgress progress)
        {
            var tempFileName = Path.GetTempFileName();
            
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, 
                _updateCancellationTokenSource.Token);
            response.EnsureSuccessStatusCode();
            
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempFileName, FileMode.Create, FileAccess.Write, FileShare.None);
            
            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            var lastReportTime = DateTime.Now;
            
            while (true)
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _updateCancellationTokenSource.Token);
                if (bytesRead == 0) break;
                
                await fileStream.WriteAsync(buffer, 0, bytesRead, _updateCancellationTokenSource.Token);
                totalBytesRead += bytesRead;
                
                var now = DateTime.Now;
                if ((now - lastReportTime).TotalMilliseconds >= 500)
                {
                    progress.BytesDownloaded = totalBytesRead;
                    progress.PercentComplete = (int)((double)totalBytesRead / progress.TotalBytes * 100);
                    progress.ElapsedTime = now - progress.StartTime;
                    progress.DownloadSpeed = totalBytesRead / progress.ElapsedTime.TotalSeconds;
                    
                    if (progress.DownloadSpeed > 0)
                    {
                        var remainingBytes = progress.TotalBytes - totalBytesRead;
                        progress.EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / progress.DownloadSpeed);
                    }
                    
                    OnUpdateProgress?.Invoke(progress);
                    lastReportTime = now;
                }
            }
            
            return tempFileName;
        }
        
        private async Task<bool> ValidateFileAsync(string filePath, UpdateInfo updateInfo)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var sha256 = SHA256.Create();
                var hash = await Task.Run(() => sha256.ComputeHash(stream));
                var hashString = Convert.ToHexString(hash);
                
                return string.Equals(hashString, updateInfo.ChecksumSHA256, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logManager.LogError("파일 검증 실패", ex);
                return false;
            }
        }
        
        private async Task ApplyUpdateFileAsync(string updateFilePath, UpdateResult result)
        {
            // 여기서는 간단한 예제로 ZIP 파일 압축 해제를 시뮬레이션합니다.
            // 실제 구현에서는 ZIP 라이브러리를 사용해야 합니다.
            var extractPath = Path.Combine(_updateDataPath, "temp_extract");
            
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
            
            Directory.CreateDirectory(extractPath);
            
            // 실제로는 System.IO.Compression.ZipFile.ExtractToDirectory 등을 사용
            // 여기서는 단순히 파일 복사로 시뮬레이션
            var translationsPath = _settingsManager.GetSetting<string>("translation.directory", "assets/translations");
            
            File.Copy(updateFilePath, Path.Combine(extractPath, "update.zip"));
            result.AddAppliedFile("update.zip");
            
            // 정리
            Directory.Delete(extractPath, true);
            File.Delete(updateFilePath);
        }
        
        private async Task CopyDirectoryAsync(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);
            
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destinationPath, fileName);
                await Task.Run(() => File.Copy(file, destFile, true));
            }
            
            foreach (var directory in Directory.GetDirectories(sourcePath))
            {
                var dirName = Path.GetFileName(directory);
                var destDir = Path.Combine(destinationPath, dirName);
                await CopyDirectoryAsync(directory, destDir);
            }
        }
        
        private void RecordUpdateHistory(UpdateInfo updateInfo, UpdateResult result)
        {
            var historyEntry = new UpdateHistory
            {
                Id = result.UpdateId,
                Version = updateInfo.Version,
                Name = updateInfo.Name,
                AppliedDate = result.CompletionTime,
                IsSuccess = result.IsSuccess,
                BackupId = result.BackupId,
                Type = updateInfo.Type,
                FilesModified = result.AppliedFiles.Count,
                Notes = result.IsSuccess ? "정상 적용" : result.ErrorMessage
            };
            
            _updateHistory.Add(historyEntry);
            
            // 너무 많은 기록이 쌓이지 않도록 제한
            while (_updateHistory.Count > 100)
            {
                var oldest = _updateHistory.OrderBy(h => h.AppliedDate).First();
                _updateHistory.Remove(oldest);
            }
            
            SaveUpdateHistory();
        }
    }

    internal class UpdateManifest
    {
        public string Version { get; set; }
        public DateTime LastModified { get; set; }
        public List<UpdateInfo> Updates { get; set; }
        
        public UpdateManifest()
        {
            Updates = new List<UpdateInfo>();
        }
    }
}