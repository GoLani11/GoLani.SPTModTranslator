using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoLani.SPTModTranslator.Core.TextDetection
{
    public interface ITextInterceptor
    {
        void Initialize();
        void Shutdown();
        
        string InterceptText(string originalText, TextInterceptionContext context = null);
        bool ShouldInterceptText(string text, TextInterceptionContext context = null);
        
        void RegisterTextSource(string sourceId, ITextSource textSource);
        void UnregisterTextSource(string sourceId);
        
        void AddTextFilter(ITextFilter filter);
        void RemoveTextFilter(ITextFilter filter);
        
        void EnableInterception();
        void DisableInterception();
        bool IsInterceptionEnabled { get; }
        
        event Action<TextInterceptionEventArgs> OnTextIntercepted;
        event Action<TextInterceptionEventArgs> OnTextFiltered;
        event Action<Exception> OnInterceptionError;
        
        TextInterceptionStatistics GetStatistics();
        void ResetStatistics();
    }

    public class TextInterceptionContext
    {
        public Component SourceComponent { get; set; }
        public string ComponentType { get; set; }
        public string TextProperty { get; set; }
        public GameObject TargetGameObject { get; set; }
        public string MethodName { get; set; }
        public object[] MethodParameters { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string SourceModule { get; set; }
        public int Priority { get; set; } = 0;
    }

    public class TextInterceptionEventArgs : EventArgs
    {
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public TextInterceptionContext Context { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool WasTranslated { get; set; }
        public string TranslationSource { get; set; }
    }

    public class TextInterceptionStatistics
    {
        public int TotalInterceptions { get; set; }
        public int SuccessfulTranslations { get; set; }
        public int FilteredTexts { get; set; }
        public int Errors { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public Dictionary<string, int> ComponentTypeStats { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> SourceModuleStats { get; set; } = new Dictionary<string, int>();
        public DateTime StatisticsStartTime { get; set; }
        public DateTime LastInterceptionTime { get; set; }
    }

    public interface ITextSource
    {
        string SourceId { get; }
        string GetText(TextInterceptionContext context);
        bool CanProvideText(TextInterceptionContext context);
        int Priority { get; }
    }

    public interface ITextFilter
    {
        string FilterId { get; }
        bool ShouldFilter(string text, TextInterceptionContext context);
        string ApplyFilter(string text, TextInterceptionContext context);
        int Priority { get; }
    }
}