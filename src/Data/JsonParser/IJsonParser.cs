using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GoLani.SPTModTranslator.Data.JsonParser
{
    public interface IJsonParser
    {
        TranslationData LoadFromFile(string filePath);
        Task<TranslationData> LoadFromFileAsync(string filePath);
        
        void SaveToFile(TranslationData data, string filePath);
        Task SaveToFileAsync(TranslationData data, string filePath);
        
        TranslationData LoadFromString(string jsonContent);
        string SaveToString(TranslationData data);
        
        bool ValidateTranslationFile(string filePath, out List<string> errors);
        bool ValidateTranslationData(TranslationData data, out List<string> errors);
        
        TranslationData MergeTranslationData(TranslationData primary, TranslationData secondary);
        List<string> GetMissingKeys(TranslationData source, TranslationData target);
        
        void SetCustomSerializerSettings(Newtonsoft.Json.JsonSerializerSettings settings);
        
        event Action<string> OnFileLoaded;
        event Action<string> OnFileSaved;
        event Action<string, Exception> OnError;
    }
}