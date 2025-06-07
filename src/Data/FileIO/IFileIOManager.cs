using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoLani.SPTModTranslator.Data.JsonParser;

namespace GoLani.SPTModTranslator.Data.FileIO
{
    public interface IFileIOManager
    {
        void Initialize();
        void Shutdown();
        
        List<string> DiscoverTranslationFiles(string directory, bool recursive = true);
        void WatchDirectory(string directory, Action<string> onFileChanged = null);
        void StopWatching(string directory);
        
        TranslationData LoadTranslationFile(string filePath);
        Task<TranslationData> LoadTranslationFileAsync(string filePath);
        
        void SaveTranslationFile(TranslationData data, string filePath);
        Task SaveTranslationFileAsync(TranslationData data, string filePath);
        
        bool BackupFile(string filePath, string backupDirectory = null);
        bool RestoreFromBackup(string originalPath, string backupPath);
        
        List<string> GetAvailableLanguages(string translationsDirectory);
        Dictionary<string, DateTime> GetFileModificationTimes(List<string> filePaths);
        
        bool IsFileAccessible(string filePath);
        long GetFileSize(string filePath);
        bool ValidateFileIntegrity(string filePath);
        
        void SetTranslationsDirectory(string directory);
        string GetTranslationsDirectory();
        
        event Action<string> OnFileChanged;
        event Action<string> OnFileCreated;
        event Action<string> OnFileDeleted;
        event Action<string, Exception> OnFileError;
    }
}