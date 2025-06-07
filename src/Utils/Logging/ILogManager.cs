using System;
using BepInEx.Logging;

namespace GoLani.SPTModTranslator.Utils.Logging
{
    public interface ILogManager
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception);
        void LogError(string message, Exception exception);
        void LogFatal(string message);
        void LogFatal(Exception exception);
        
        bool IsDebugEnabled { get; }
        LogLevel MinimumLogLevel { get; set; }
        
        void SetContext(string context);
        void ClearContext();
    }
}