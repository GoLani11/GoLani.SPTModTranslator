using System;
using System.Collections.Generic;

namespace GoLani.SPTModTranslator.UI.ImGuiPanel
{
    public interface IImGuiPanel
    {
        void Initialize();
        void Shutdown();
        
        void Render();
        void Update();
        
        bool IsVisible { get; set; }
        bool IsEnabled { get; set; }
        
        void Show();
        void Hide();
        void Toggle();
        
        string PanelTitle { get; }
        ImGuiPanelFlags Flags { get; set; }
        
        event Action OnShow;
        event Action OnHide;
        event Action<Exception> OnError;
        
        Dictionary<string, object> GetPanelStatistics();
    }

    [Flags]
    public enum ImGuiPanelFlags
    {
        None = 0,
        NoTitleBar = 1,
        NoResize = 2,
        NoMove = 4,
        NoScrollbar = 8,
        NoScrollWithMouse = 16,
        NoCollapse = 32,
        AlwaysAutoResize = 64,
        NoBackground = 128,
        NoSavedSettings = 256,
        NoMouseInputs = 512,
        MenuBar = 1024,
        HorizontalScrollbar = 2048,
        NoFocusOnAppearing = 4096,
        NoBringToFrontOnFocus = 8192,
        AlwaysVerticalScrollbar = 16384,
        AlwaysHorizontalScrollbar = 32768,
        AlwaysUseWindowPadding = 65536
    }

    public interface ITranslationManagementPanel : IImGuiPanel
    {
        void ShowTranslationPreview(string originalText, string translatedText);
        void AddMissingTranslation(string key);
        void ShowStatistics();
        void ManageCache();
        void ShowSettings();
        
        List<string> GetMissingTranslations();
        void ClearMissingTranslations();
    }
}