using Newtonsoft.Json;

namespace GoLaniSPTModTranslator.Models
{
    // 패치 정의 모델
    public class PatchDefinition
    {
        [JsonProperty("Enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("TargetAssembly")]
        public string TargetAssembly { get; set; }

        [JsonProperty("TargetType")]
        public string TargetType { get; set; }

        [JsonProperty("TargetMethod")]
        public string TargetMethod { get; set; }

        [JsonProperty("PatchType")]
        public string PatchType { get; set; }

        [JsonProperty("ParameterIndex")]
        public int? ParameterIndex { get; set; }

        [JsonProperty("TranslationModID")]
        public string TranslationModID { get; set; }
    }
} 