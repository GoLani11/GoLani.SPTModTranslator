using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GoLani.SPTModTranslator.Utils.Logging;

namespace GoLani.SPTModTranslator.Data.JsonParser
{
    public class JsonParser : IJsonParser
    {
        private readonly ILogManager _logManager;
        private JsonSerializerSettings _serializerSettings;

        public event Action<string> OnFileLoaded;
        public event Action<string> OnFileSaved;
        public event Action<string, Exception> OnError;

        public JsonParser(ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            InitializeDefaultSettings();
        }

        public TranslationData LoadFromFile(string filePath)
        {
            try
            {
                _logManager.SetContext("JsonParser");
                _logManager.LogDebug($"JSON 파일 로딩 시작: {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"번역 파일을 찾을 수 없습니다: {filePath}");
                }

                var jsonContent = File.ReadAllText(filePath);
                var data = LoadFromString(jsonContent);
                data.SourceFilePath = filePath;
                data.LoadedAt = DateTime.Now;

                OnFileLoaded?.Invoke(filePath);
                _logManager.LogInfo($"JSON 파일 로딩 완료: {filePath}");
                
                return data;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"JSON 파일 로딩 실패: {filePath}", ex);
                OnError?.Invoke(filePath, ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public async Task<TranslationData> LoadFromFileAsync(string filePath)
        {
            try
            {
                _logManager.SetContext("JsonParser");
                _logManager.LogDebug($"JSON 파일 비동기 로딩 시작: {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"번역 파일을 찾을 수 없습니다: {filePath}");
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var data = LoadFromString(jsonContent);
                data.SourceFilePath = filePath;
                data.LoadedAt = DateTime.Now;

                OnFileLoaded?.Invoke(filePath);
                _logManager.LogInfo($"JSON 파일 비동기 로딩 완료: {filePath}");
                
                return data;
            }
            catch (Exception ex)
            {
                _logManager.LogError($"JSON 파일 비동기 로딩 실패: {filePath}", ex);
                OnError?.Invoke(filePath, ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public void SaveToFile(TranslationData data, string filePath)
        {
            try
            {
                _logManager.SetContext("JsonParser");
                _logManager.LogDebug($"JSON 파일 저장 시작: {filePath}");

                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                data.Metadata.UpdatedAt = DateTime.Now;
                var jsonContent = SaveToString(data);
                File.WriteAllText(filePath, jsonContent);

                OnFileSaved?.Invoke(filePath);
                _logManager.LogInfo($"JSON 파일 저장 완료: {filePath}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"JSON 파일 저장 실패: {filePath}", ex);
                OnError?.Invoke(filePath, ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public async Task SaveToFileAsync(TranslationData data, string filePath)
        {
            try
            {
                _logManager.SetContext("JsonParser");
                _logManager.LogDebug($"JSON 파일 비동기 저장 시작: {filePath}");

                var directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                data.Metadata.UpdatedAt = DateTime.Now;
                var jsonContent = SaveToString(data);
                await File.WriteAllTextAsync(filePath, jsonContent);

                OnFileSaved?.Invoke(filePath);
                _logManager.LogInfo($"JSON 파일 비동기 저장 완료: {filePath}");
            }
            catch (Exception ex)
            {
                _logManager.LogError($"JSON 파일 비동기 저장 실패: {filePath}", ex);
                OnError?.Invoke(filePath, ex);
                throw;
            }
            finally
            {
                _logManager.ClearContext();
            }
        }

        public TranslationData LoadFromString(string jsonContent)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonContent))
                {
                    throw new ArgumentException("JSON 내용이 비어있습니다.");
                }

                return JsonConvert.DeserializeObject<TranslationData>(jsonContent, _serializerSettings);
            }
            catch (Exception ex)
            {
                _logManager.LogError("JSON 문자열 파싱 실패", ex);
                throw new JsonException("JSON 파싱 중 오류가 발생했습니다.", ex);
            }
        }

        public string SaveToString(TranslationData data)
        {
            try
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                return JsonConvert.SerializeObject(data, _serializerSettings);
            }
            catch (Exception ex)
            {
                _logManager.LogError("JSON 문자열 직렬화 실패", ex);
                throw new JsonException("JSON 직렬화 중 오류가 발생했습니다.", ex);
            }
        }

        public bool ValidateTranslationFile(string filePath, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                if (!File.Exists(filePath))
                {
                    errors.Add($"파일이 존재하지 않습니다: {filePath}");
                    return false;
                }

                var jsonContent = File.ReadAllText(filePath);
                var data = LoadFromString(jsonContent);
                return ValidateTranslationData(data, out errors);
            }
            catch (Exception ex)
            {
                errors.Add($"파일 검증 중 오류: {ex.Message}");
                return false;
            }
        }

        public bool ValidateTranslationData(TranslationData data, out List<string> errors)
        {
            errors = new List<string>();

            if (data == null)
            {
                errors.Add("번역 데이터가 null입니다.");
                return false;
            }

            if (string.IsNullOrEmpty(data.Version))
            {
                errors.Add("버전 정보가 누락되었습니다.");
            }

            if (string.IsNullOrEmpty(data.Language))
            {
                errors.Add("언어 정보가 누락되었습니다.");
            }

            if (data.Translations == null || data.Translations.Count == 0)
            {
                errors.Add("번역 데이터가 비어있습니다.");
            }

            if (data.Metadata == null)
            {
                errors.Add("메타데이터가 누락되었습니다.");
            }

            return errors.Count == 0;
        }

        public TranslationData MergeTranslationData(TranslationData primary, TranslationData secondary)
        {
            if (primary == null && secondary == null)
                return new TranslationData();

            if (primary == null)
                return secondary;

            if (secondary == null)
                return primary;

            var merged = new TranslationData
            {
                Version = primary.Version,
                Language = primary.Language,
                Metadata = primary.Metadata,
                Translations = new Dictionary<string, object>(primary.Translations)
            };

            MergeDictionaries(merged.Translations, secondary.Translations);
            merged.Metadata.UpdatedAt = DateTime.Now;

            return merged;
        }

        public List<string> GetMissingKeys(TranslationData source, TranslationData target)
        {
            var missingKeys = new List<string>();

            if (source?.Translations == null || target?.Translations == null)
                return missingKeys;

            var sourceKeys = source.GetAllKeys();
            var targetKeys = new HashSet<string>(target.GetAllKeys());

            foreach (var key in sourceKeys)
            {
                if (!targetKeys.Contains(key))
                {
                    missingKeys.Add(key);
                }
            }

            return missingKeys;
        }

        public void SetCustomSerializerSettings(JsonSerializerSettings settings)
        {
            _serializerSettings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        private void InitializeDefaultSettings()
        {
            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Local,
                DefaultValueHandling = DefaultValueHandling.Include,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
        }

        private void MergeDictionaries(Dictionary<string, object> target, Dictionary<string, object> source)
        {
            foreach (var kvp in source)
            {
                if (kvp.Value is Dictionary<string, object> sourceDict)
                {
                    if (target.ContainsKey(kvp.Key) && target[kvp.Key] is Dictionary<string, object> targetDict)
                    {
                        MergeDictionaries(targetDict, sourceDict);
                    }
                    else
                    {
                        target[kvp.Key] = new Dictionary<string, object>(sourceDict);
                    }
                }
                else
                {
                    target[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}