using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GoLani.SPTModTranslator.Core.ModCompatibility
{
    public class ModTranslationPattern
    {
        public string ModId { get; set; }
        public string PatternName { get; set; }
        public string Description { get; set; }
        public int Priority { get; set; }
        public bool IsEnabled { get; set; }
        
        public List<TextFrameworkPattern> TextFrameworkPatterns { get; set; }
        public List<KeyPattern> KeyPatterns { get; set; }
        public List<ValuePattern> ValuePatterns { get; set; }
        public List<ContextPattern> ContextPatterns { get; set; }
        
        public Dictionary<string, string> CustomSettings { get; set; }
        public List<string> RequiredDependencies { get; set; }
        public List<string> ExcludeKeys { get; set; }
        public List<string> IncludeKeys { get; set; }
        
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public string Version { get; set; }
        
        public ModTranslationPattern()
        {
            TextFrameworkPatterns = new List<TextFrameworkPattern>();
            KeyPatterns = new List<KeyPattern>();
            ValuePatterns = new List<ValuePattern>();
            ContextPatterns = new List<ContextPattern>();
            CustomSettings = new Dictionary<string, string>();
            RequiredDependencies = new List<string>();
            ExcludeKeys = new List<string>();
            IncludeKeys = new List<string>();
            
            Priority = 100;
            IsEnabled = true;
            CreatedDate = DateTime.Now;
            LastModified = DateTime.Now;
            Version = "1.0.0";
        }
        
        public bool IsKeyExcluded(string key)
        {
            if (string.IsNullOrEmpty(key))
                return true;
                
            if (ExcludeKeys.Count > 0)
            {
                foreach (var excludePattern in ExcludeKeys)
                {
                    if (Regex.IsMatch(key, excludePattern, RegexOptions.IgnoreCase))
                        return true;
                }
            }
            
            if (IncludeKeys.Count > 0)
            {
                foreach (var includePattern in IncludeKeys)
                {
                    if (Regex.IsMatch(key, includePattern, RegexOptions.IgnoreCase))
                        return false;
                }
                return true;
            }
            
            return false;
        }
        
        public bool MatchesContext(string context)
        {
            if (ContextPatterns.Count == 0)
                return true;
                
            foreach (var pattern in ContextPatterns)
            {
                if (pattern.IsEnabled && pattern.Matches(context))
                    return true;
            }
            
            return false;
        }
        
        public string TransformKey(string originalKey)
        {
            if (string.IsNullOrEmpty(originalKey))
                return originalKey;
                
            var transformedKey = originalKey;
            
            foreach (var keyPattern in KeyPatterns)
            {
                if (keyPattern.IsEnabled)
                {
                    transformedKey = keyPattern.Transform(transformedKey);
                }
            }
            
            return transformedKey;
        }
        
        public string TransformValue(string originalValue)
        {
            if (string.IsNullOrEmpty(originalValue))
                return originalValue;
                
            var transformedValue = originalValue;
            
            foreach (var valuePattern in ValuePatterns)
            {
                if (valuePattern.IsEnabled)
                {
                    transformedValue = valuePattern.Transform(transformedValue);
                }
            }
            
            return transformedValue;
        }
    }

    public class TextFrameworkPattern
    {
        public string FrameworkName { get; set; }
        public string MethodPattern { get; set; }
        public string PropertyPattern { get; set; }
        public bool IsEnabled { get; set; }
        public int Priority { get; set; }
        
        public TextFrameworkPattern()
        {
            IsEnabled = true;
            Priority = 100;
        }
    }

    public class KeyPattern
    {
        public string Pattern { get; set; }
        public string Replacement { get; set; }
        public bool IsRegex { get; set; }
        public bool IsEnabled { get; set; }
        public RegexOptions RegexOptions { get; set; }
        
        public KeyPattern()
        {
            IsEnabled = true;
            IsRegex = false;
            RegexOptions = RegexOptions.IgnoreCase;
        }
        
        public string Transform(string input)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(Pattern))
                return input;
                
            try
            {
                if (IsRegex)
                {
                    return Regex.Replace(input, Pattern, Replacement ?? string.Empty, RegexOptions);
                }
                else
                {
                    return input.Replace(Pattern, Replacement ?? string.Empty);
                }
            }
            catch (Exception)
            {
                return input;
            }
        }
    }

    public class ValuePattern
    {
        public string Pattern { get; set; }
        public string Replacement { get; set; }
        public bool IsRegex { get; set; }
        public bool IsEnabled { get; set; }
        public RegexOptions RegexOptions { get; set; }
        
        public ValuePattern()
        {
            IsEnabled = true;
            IsRegex = false;
            RegexOptions = RegexOptions.IgnoreCase;
        }
        
        public string Transform(string input)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(Pattern))
                return input;
                
            try
            {
                if (IsRegex)
                {
                    return Regex.Replace(input, Pattern, Replacement ?? string.Empty, RegexOptions);
                }
                else
                {
                    return input.Replace(Pattern, Replacement ?? string.Empty);
                }
            }
            catch (Exception)
            {
                return input;
            }
        }
    }

    public class ContextPattern
    {
        public string Pattern { get; set; }
        public bool IsRegex { get; set; }
        public bool IsEnabled { get; set; }
        public RegexOptions RegexOptions { get; set; }
        
        public ContextPattern()
        {
            IsEnabled = true;
            IsRegex = false;
            RegexOptions = RegexOptions.IgnoreCase;
        }
        
        public bool Matches(string context)
        {
            if (string.IsNullOrEmpty(context) || string.IsNullOrEmpty(Pattern))
                return false;
                
            try
            {
                if (IsRegex)
                {
                    return Regex.IsMatch(context, Pattern, RegexOptions);
                }
                else
                {
                    return context.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}