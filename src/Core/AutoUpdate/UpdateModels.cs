using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.Core.AutoUpdate
{
    public class UpdateInfo
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DownloadUrl { get; set; }
        public string ChecksumMD5 { get; set; }
        public string ChecksumSHA256 { get; set; }
        
        public long FileSize { get; set; }
        public DateTime ReleaseDate { get; set; }
        public UpdatePriority Priority { get; set; }
        public UpdateType Type { get; set; }
        public UpdateChannel Channel { get; set; }
        
        public List<string> TargetLanguages { get; set; }
        public List<string> SupportedMods { get; set; }
        public List<string> Dependencies { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        
        public bool IsSecurityUpdate { get; set; }
        public bool IsCritical { get; set; }
        public bool RequiresRestart { get; set; }
        
        public UpdateInfo()
        {
            TargetLanguages = new List<string>();
            SupportedMods = new List<string>();
            Dependencies = new List<string>();
            Metadata = new Dictionary<string, object>();
        }
        
        public override string ToString()
        {
            return $"{Name} v{Version} ({Type}, {Priority})";
        }
    }

    public class UpdateProgress
    {
        public string UpdateId { get; set; }
        public UpdateStage Stage { get; set; }
        public string CurrentOperation { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public int PercentComplete { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public double DownloadSpeed { get; set; }
        
        public DateTime StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public bool IsCompleted => CompletionTime.HasValue;
        
        public string GetFormattedProgress()
        {
            return $"{CurrentOperation}: {PercentComplete}% ({FormatBytes(BytesDownloaded)}/{FormatBytes(TotalBytes)})";
        }
        
        public string GetFormattedSpeed()
        {
            return $"{FormatBytes((long)DownloadSpeed)}/s";
        }
        
        private string FormatBytes(long bytes)
        {
            var units = new[] { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unitIndex = 0;
            
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            
            return $"{size:F1} {units[unitIndex]}";
        }
    }

    public class UpdateResult
    {
        public string UpdateId { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
        public TimeSpan Duration => CompletionTime - StartTime;
        
        public List<string> AppliedFiles { get; set; }
        public List<string> BackedUpFiles { get; set; }
        public List<string> FailedFiles { get; set; }
        public string BackupId { get; set; }
        
        public Dictionary<string, object> ResultData { get; set; }
        
        public UpdateResult()
        {
            AppliedFiles = new List<string>();
            BackedUpFiles = new List<string>();
            FailedFiles = new List<string>();
            ResultData = new Dictionary<string, object>();
        }
        
        public void AddAppliedFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && !AppliedFiles.Contains(filePath))
            {
                AppliedFiles.Add(filePath);
            }
        }
        
        public void AddBackedUpFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && !BackedUpFiles.Contains(filePath))
            {
                BackedUpFiles.Add(filePath);
            }
        }
        
        public void AddFailedFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && !FailedFiles.Contains(filePath))
            {
                FailedFiles.Add(filePath);
            }
        }
        
        public string GetSummary()
        {
            if (IsSuccess)
            {
                return $"업데이트 성공: {AppliedFiles.Count}개 파일 적용됨 (소요시간: {Duration.TotalSeconds:F1}초)";
            }
            else
            {
                return $"업데이트 실패: {ErrorMessage} (실패한 파일: {FailedFiles.Count}개)";
            }
        }
    }

    public class UpdateHistory
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public DateTime AppliedDate { get; set; }
        public bool IsSuccess { get; set; }
        public string BackupId { get; set; }
        public UpdateType Type { get; set; }
        public int FilesModified { get; set; }
        public string Notes { get; set; }
        
        public override string ToString()
        {
            var status = IsSuccess ? "성공" : "실패";
            return $"{Name} v{Version} - {status} ({AppliedDate:yyyy-MM-dd HH:mm})";
        }
    }

    public class UpdateConfiguration
    {
        public bool AutoUpdateEnabled { get; set; }
        public UpdateChannel Channel { get; set; }
        public TimeSpan CheckInterval { get; set; }
        public string UpdateServerUrl { get; set; }
        public string BackupDirectory { get; set; }
        public int MaxBackupsToKeep { get; set; }
        public bool VerifyChecksums { get; set; }
        public bool CreateBackupBeforeUpdate { get; set; }
        public bool AllowPreReleases { get; set; }
        public List<string> IncludedLanguages { get; set; }
        public List<string> ExcludedMods { get; set; }
        
        public UpdateConfiguration()
        {
            AutoUpdateEnabled = true;
            Channel = UpdateChannel.Stable;
            CheckInterval = TimeSpan.FromHours(24);
            MaxBackupsToKeep = 5;
            VerifyChecksums = true;
            CreateBackupBeforeUpdate = true;
            AllowPreReleases = false;
            IncludedLanguages = new List<string> { "ko_KR" };
            ExcludedMods = new List<string>();
        }
    }

    public enum UpdatePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3,
        Security = 4
    }

    public enum UpdateType
    {
        Translation = 0,
        Framework = 1,
        Configuration = 2,
        Plugin = 3,
        Hotfix = 4,
        Security = 5
    }

    public enum UpdateChannel
    {
        Stable = 0,
        Beta = 1,
        Alpha = 2,
        Development = 3
    }

    public enum UpdateStage
    {
        Initializing = 0,
        CheckingForUpdates = 1,
        DownloadingManifest = 2,
        ValidatingManifest = 3,
        DownloadingFiles = 4,
        ValidatingFiles = 5,
        CreatingBackup = 6,
        ApplyingUpdate = 7,
        VerifyingUpdate = 8,
        CleaningUp = 9,
        Completed = 10,
        Failed = 11,
        Cancelled = 12
    }
}