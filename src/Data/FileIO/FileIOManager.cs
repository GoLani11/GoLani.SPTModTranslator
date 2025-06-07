using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoLani.SPTModTranslator.Data.JsonParser;
using GoLani.SPTModTranslator.Utils.Logging;

namespace GoLani.SPTModTranslator.Data.FileIO
{
    public class FileIOManager : IFileIOManager
    {
        private readonly IJsonParser _jsonParser;
        private readonly ILogManager _logManager;
        private readonly Dictionary<string, FileSystemWatcher> _watchers;
        private string _translationsDirectory;

        public event Action<string> OnFileChanged;
        public event Action<string> OnFileCreated;
        public event Action<string> OnFileDeleted;
        public event Action<string, Exception> OnFileError;

        public FileIOManager(IJsonParser jsonParser, ILogManager logManager)
        {
            _jsonParser = jsonParser ?? throw new ArgumentNullException(nameof(jsonParser));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _watchers = new Dictionary<string, FileSystemWatcher>();
        }

        public void Initialize()
        {
            try
            {
                _logManager.SetContext("FileIOManager");
                _logManager.LogInfo("파일 I/O 매니저 초기화 중...");

                SetDefaultTranslationsDirectory();
                EnsureDirectoryExists(_translationsDirectory);

                _logManager.LogInfo($"번역 파일 디렉토리: {_translationsDirectory}");
                _logManager.LogInfo("파일 I/O 매니저 초기화 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("파일 I/O 매니저 초기화 실패", ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void Shutdown()
        {
            try
            {
                _logManager.SetContext("FileIOManager");
                _logManager.LogInfo("파일 I/O 매니저 종료 중...");

                foreach (var watcher in _watchers.Values)
                {
                    watcher?.Dispose();
                }
                _watchers.Clear();

                _logManager.LogInfo("파일 I/O 매니저 종료 완료");
            }
            catch (Exception ex)
            {
                _logManager.LogError("파일 I/O 매니저 종료 중 오류", ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public List<string> DiscoverTranslationFiles(string directory, bool recursive = true)
        {
            try
            {
                _logManager.SetContext("FileIOManager");
                _logManager.LogDebug($"번역 파일 검색 시작: {directory} (재귀: {recursive})");

                if (!Directory.Exists(directory))
                {
                    _logManager.LogWarning($"디렉토리가 존재하지 않습니다: {directory}");
                    return new List<string>();
                }

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(directory, "*.json", searchOption)
                    .Where(f => IsTranslationFile(f))
                    .ToList();

                _logManager.LogInfo($"번역 파일 {files.Count}개 발견: {directory}");
                return files;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"번역 파일 검색 실패: {directory}", ex);
                OnFileError?.Invoke(directory, ex);
                return new List<string>();
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void WatchDirectory(string directory, Action<string> onFileChanged = null)
        {
            try
            {
                _logManager.SetContext("FileIOManager");
                _logManager.LogDebug($"디렉토리 감시 시작: {directory}");

                if (!Directory.Exists(directory))
                {
                    _logManager.LogWarning($"감시할 디렉토리가 존재하지 않습니다: {directory}");
                    return;
                }

                if (_watchers.ContainsKey(directory))
                {
                    _logManager.LogDebug($"이미 감시 중인 디렉토리: {directory}");
                    return;
                }

                var watcher = new FileSystemWatcher(directory, "*.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (sender, e) => HandleFileEvent(e, onFileChanged, OnFileChanged);
                watcher.Created += (sender, e) => HandleFileEvent(e, onFileChanged, OnFileCreated);
                watcher.Deleted += (sender, e) => HandleFileEvent(e, null, OnFileDeleted);
                watcher.Error += (sender, e) => OnFileError?.Invoke(directory, e.GetException());

                _watchers[directory] = watcher;
                _logManager.LogInfo($"디렉토리 감시 시작됨: {directory}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"디렉토리 감시 설정 실패: {directory}", ex);
                OnFileError?.Invoke(directory, ex);
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void StopWatching(string directory)
        {
            if (_watchers.ContainsKey(directory))
            {
                _watchers[directory]?.Dispose();
                _watchers.Remove(directory);
                _logManager.LogInfo($"디렉토리 감시 중지: {directory}");
            }
        }

        public TranslationData LoadTranslationFile(string filePath)
        {
            try
            {
                if (!IsFileAccessible(filePath))
                {
                    throw new FileNotFoundException($"번역 파일에 접근할 수 없습니다: {filePath}");
                }

                return _jsonParser.LoadFromFile(filePath);
            }
            catch (Exception ex)
            {
                OnFileError?.Invoke(filePath, ex);
                throw;
            }
        }

        public async Task<TranslationData> LoadTranslationFileAsync(string filePath)
        {
            try
            {
                if (!IsFileAccessible(filePath))
                {
                    throw new FileNotFoundException($"번역 파일에 접근할 수 없습니다: {filePath}");
                }

                return await _jsonParser.LoadFromFileAsync(filePath);
            }
            catch (Exception ex)
            {
                OnFileError?.Invoke(filePath, ex);
                throw;
            }
        }

        public void SaveTranslationFile(TranslationData data, string filePath)
        {
            try
            {
                EnsureDirectoryExists(Path.GetDirectoryName(filePath));
                _jsonParser.SaveToFile(data, filePath);
            }
            catch (Exception ex)
            {
                OnFileError?.Invoke(filePath, ex);
                throw;
            }
        }

        public async Task SaveTranslationFileAsync(TranslationData data, string filePath)
        {
            try
            {
                EnsureDirectoryExists(Path.GetDirectoryName(filePath));
                await _jsonParser.SaveToFileAsync(data, filePath);
            }
            catch (Exception ex)
            {
                OnFileError?.Invoke(filePath, ex);
                throw;
            }
        }

        public bool BackupFile(string filePath, string backupDirectory = null)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                backupDirectory = backupDirectory ?? Path.Combine(Path.GetDirectoryName(filePath), "backups");
                EnsureDirectoryExists(backupDirectory);

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{fileName}_{timestamp}{extension}";
                var backupPath = Path.Combine(backupDirectory, backupFileName);

                File.Copy(filePath, backupPath);
                _logManager.LogDebug($"파일 백업 완료: {filePath} -> {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"파일 백업 실패: {filePath}", ex);
                OnFileError?.Invoke(filePath, ex);
                return false;
            }
        }

        public bool RestoreFromBackup(string originalPath, string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                    return false;

                File.Copy(backupPath, originalPath, true);
                _logManager.LogInfo($"파일 복원 완료: {backupPath} -> {originalPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"파일 복원 실패: {backupPath} -> {originalPath}", ex);
                OnFileError?.Invoke(originalPath, ex);
                return false;
            }
        }

        public List<string> GetAvailableLanguages(string translationsDirectory)
        {
            try
            {
                var languages = new HashSet<string>();
                var files = DiscoverTranslationFiles(translationsDirectory);

                foreach (var file in files)
                {
                    try
                    {
                        var data = LoadTranslationFile(file);
                        if (!string.IsNullOrEmpty(data.Language))
                        {
                            languages.Add(data.Language);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logManager.LogWarning($"언어 정보 읽기 실패: {file}", ex);
                    }
                }

                return languages.ToList();
            }
            catch (Exception ex)
            {
                _logManager.LogError($"사용 가능한 언어 목록 가져오기 실패: {translationsDirectory}", ex);
                return new List<string>();
            }
        }

        public Dictionary<string, DateTime> GetFileModificationTimes(List<string> filePaths)
        {
            var result = new Dictionary<string, DateTime>();

            foreach (var filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        result[filePath] = File.GetLastWriteTime(filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logManager.LogWarning($"파일 수정 시간 가져오기 실패: {filePath}", ex);
                }
            }

            return result;
        }

        public bool IsFileAccessible(string filePath)
        {
            try
            {
                return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public long GetFileSize(string filePath)
        {
            try
            {
                return new FileInfo(filePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        public bool ValidateFileIntegrity(string filePath)
        {
            try
            {
                if (!IsFileAccessible(filePath))
                    return false;

                var errors = new List<string>();
                return _jsonParser.ValidateTranslationFile(filePath, out errors);
            }
            catch
            {
                return false;
            }
        }

        public void SetTranslationsDirectory(string directory)
        {
            _translationsDirectory = directory;
            EnsureDirectoryExists(_translationsDirectory);
        }

        public string GetTranslationsDirectory()
        {
            return _translationsDirectory;
        }

        private void SetDefaultTranslationsDirectory()
        {
            _translationsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "translations");
        }

        private void EnsureDirectoryExists(string directory)
        {
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private bool IsTranslationFile(string filePath)
        {
            try
            {
                return Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase) &&
                       ValidateFileIntegrity(filePath);
            }
            catch
            {
                return false;
            }
        }

        private void HandleFileEvent(FileSystemEventArgs e, Action<string> customHandler, Action<string> defaultHandler)
        {
            try
            {
                if (IsTranslationFile(e.FullPath))
                {
                    customHandler?.Invoke(e.FullPath);
                    defaultHandler?.Invoke(e.FullPath);
                    _logManager.LogDebug($"파일 이벤트 처리: {e.ChangeType} - {e.FullPath}");
                }
            }
            catch (Exception ex)
            {
                _logManager.LogError($"파일 이벤트 처리 실패: {e.FullPath}", ex);
                OnFileError?.Invoke(e.FullPath, ex);
            }
        }
    }
}