using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoLani.SPTModTranslator.Core.AutoUpdate
{
    public interface IAutoUpdateManager
    {
        bool IsInitialized { get; }
        bool IsUpdateInProgress { get; }
        UpdateConfiguration Configuration { get; set; }
        
        event Action<UpdateInfo> OnUpdateAvailable;
        event Action<UpdateProgress> OnUpdateProgress;
        event Action<UpdateResult> OnUpdateCompleted;
        event Action<string> OnUpdateError;
        event Action OnUpdateCancelled;
        
        void Initialize();
        void Shutdown();
        
        Task<List<UpdateInfo>> CheckForUpdatesAsync();
        Task<UpdateResult> DownloadAndApplyUpdatesAsync();
        Task<UpdateResult> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo);
        
        void CancelUpdate();
        void PauseUpdate();
        void ResumeUpdate();
        
        List<UpdateInfo> GetAvailableUpdates();
        List<UpdateHistory> GetUpdateHistory();
        UpdateInfo GetLatestUpdateInfo();
        
        Task<bool> RollbackToVersionAsync(string version);
        Task<bool> CreateBackupAsync();
        Task<bool> RestoreBackupAsync(string backupId);
        
        void SetAutoUpdateEnabled(bool enabled);
        void SetUpdateChannel(UpdateChannel channel);
        void SetUpdateInterval(TimeSpan interval);
        
        bool IsVersionNewer(string currentVersion, string newVersion);
        bool IsUpdateRequired(UpdateInfo updateInfo);
        bool IsBackupAvailable(string backupId);
        
        Dictionary<string, object> GetUpdateStatistics();
        void ValidateIntegrity();
        void CleanupOldBackups(int maxBackupsToKeep = 5);
    }
}