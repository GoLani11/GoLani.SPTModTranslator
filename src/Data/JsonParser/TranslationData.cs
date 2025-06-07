using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GoLani.SPTModTranslator.Data.JsonParser
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TranslationData
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("language")]
        public string Language { get; set; } = "ko_KR";

        [JsonProperty("metadata")]
        public TranslationMetadata Metadata { get; set; } = new TranslationMetadata();

        [JsonProperty("translations")]
        public Dictionary<string, object> Translations { get; set; } = new Dictionary<string, object>();

        [JsonIgnore]
        public DateTime LoadedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string SourceFilePath { get; set; }

        public string GetTranslation(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            return GetNestedValue(Translations, key.Split('.'));
        }

        public void SetTranslation(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            SetNestedValue(Translations, key.Split('.'), value);
        }

        public bool HasTranslation(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return GetNestedValue(Translations, key.Split('.')) != null;
        }

        public List<string> GetAllKeys()
        {
            var keys = new List<string>();
            CollectKeys(Translations, "", keys);
            return keys;
        }

        private string GetNestedValue(Dictionary<string, object> dict, string[] keyParts)
        {
            if (keyParts.Length == 0)
                return null;

            if (!dict.ContainsKey(keyParts[0]))
                return null;

            if (keyParts.Length == 1)
            {
                return dict[keyParts[0]]?.ToString();
            }

            if (dict[keyParts[0]] is Dictionary<string, object> nestedDict)
            {
                var remainingKeys = new string[keyParts.Length - 1];
                Array.Copy(keyParts, 1, remainingKeys, 0, remainingKeys.Length);
                return GetNestedValue(nestedDict, remainingKeys);
            }

            return null;
        }

        private void SetNestedValue(Dictionary<string, object> dict, string[] keyParts, string value)
        {
            if (keyParts.Length == 0)
                return;

            if (keyParts.Length == 1)
            {
                dict[keyParts[0]] = value;
                return;
            }

            if (!dict.ContainsKey(keyParts[0]) || !(dict[keyParts[0]] is Dictionary<string, object>))
            {
                dict[keyParts[0]] = new Dictionary<string, object>();
            }

            var nestedDict = (Dictionary<string, object>)dict[keyParts[0]];
            var remainingKeys = new string[keyParts.Length - 1];
            Array.Copy(keyParts, 1, remainingKeys, 0, remainingKeys.Length);
            SetNestedValue(nestedDict, remainingKeys, value);
        }

        private void CollectKeys(Dictionary<string, object> dict, string prefix, List<string> keys)
        {
            foreach (var kvp in dict)
            {
                var currentKey = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (kvp.Value is Dictionary<string, object> nestedDict)
                {
                    CollectKeys(nestedDict, currentKey, keys);
                }
                else
                {
                    keys.Add(currentKey);
                }
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TranslationMetadata
    {
        [JsonProperty("author")]
        public string Author { get; set; } = "SKLF";

        [JsonProperty("description")]
        public string Description { get; set; } = "SPT Korean Localization";

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [JsonProperty("mod_name")]
        public string ModName { get; set; } = "";

        [JsonProperty("mod_version")]
        public string ModVersion { get; set; } = "";

        [JsonProperty("translation_count")]
        public int TranslationCount { get; set; } = 0;

        [JsonProperty("completion_percentage")]
        public float CompletionPercentage { get; set; } = 0.0f;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }
}