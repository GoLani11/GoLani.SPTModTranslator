using System;
using BepInEx.Logging;

namespace GoLani.SPTModTranslator.Utils.Logging
{
    public class LogManager : ILogManager
    {
        private readonly ManualLogSource _logger;
        private string _currentContext;

        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
        public bool IsDebugEnabled => MinimumLogLevel <= LogLevel.Debug;

        public LogManager(ManualLogSource logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LogDebug(string message)
        {
            if (MinimumLogLevel <= LogLevel.Debug)
            {
                _logger.LogDebug(FormatMessage(message));
            }
        }

        public void LogInfo(string message)
        {
            if (MinimumLogLevel <= LogLevel.Info)
            {
                _logger.LogInfo(FormatMessage(message));
            }
        }

        public void LogWarning(string message)
        {
            if (MinimumLogLevel <= LogLevel.Warning)
            {
                _logger.LogWarning(FormatMessage(message));
            }
        }

        public void LogError(string message)
        {
            _logger.LogError(FormatMessage(message));
        }

        public void LogError(Exception exception)
        {
            _logger.LogError(FormatMessage($"예외 발생: {exception}"));
        }

        public void LogError(string message, Exception exception)
        {
            _logger.LogError(FormatMessage($"{message}: {exception}"));
        }

        public void LogFatal(string message)
        {
            _logger.LogFatal(FormatMessage(message));
        }

        public void LogFatal(Exception exception)
        {
            _logger.LogFatal(FormatMessage($"치명적 오류: {exception}"));
        }

        public void SetContext(string context)
        {
            _currentContext = context;
        }

        public void ClearContext()
        {
            _currentContext = null;
        }

        private string FormatMessage(string message)
        {
            if (string.IsNullOrEmpty(_currentContext))
            {
                return message;
            }
            
            return $"[{_currentContext}] {message}";
        }
    }
}