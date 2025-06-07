using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.Core.ModCompatibility
{
    public class ModInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string InstallPath { get; set; }
        public string AssemblyPath { get; set; }
        public DateTime InstallDate { get; set; }
        public DateTime LastModified { get; set; }
        
        public ModType Type { get; set; }
        public ModCompatibilityLevel CompatibilityLevel { get; set; }
        public List<string> Dependencies { get; set; }
        public List<string> ConflictsWith { get; set; }
        public List<string> SupportedTextFrameworks { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; }
        
        public bool IsActive { get; set; }
        public bool HasTranslationSupport { get; set; }
        public bool RequiresCustomPatching { get; set; }
        
        public ModInfo()
        {
            Dependencies = new List<string>();
            ConflictsWith = new List<string>();
            SupportedTextFrameworks = new List<string>();
            CustomProperties = new Dictionary<string, object>();
            IsActive = true;
            HasTranslationSupport = false;
            RequiresCustomPatching = false;
        }
        
        public override string ToString()
        {
            return $"{Name} ({Id}) v{Version} - {CompatibilityLevel}";
        }
        
        public override bool Equals(object obj)
        {
            if (obj is ModInfo other)
            {
                return Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }
    }

    public enum ModType
    {
        Unknown = 0,
        BepInExPlugin = 1,
        SPTMod = 2,
        UnityMod = 3,
        NativeMod = 4,
        ScriptMod = 5
    }

    public enum ModCompatibilityLevel
    {
        Unknown = 0,
        FullyCompatible = 1,
        PartiallyCompatible = 2,
        RequiresPatch = 3,
        Incompatible = 4,
        Conflicting = 5
    }
}