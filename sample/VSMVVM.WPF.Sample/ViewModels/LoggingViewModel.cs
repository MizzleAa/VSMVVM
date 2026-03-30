using System;
using System.Collections.ObjectModel;
using VSMVVM.Core.MVVM;
using VSMVVM.Core.Attributes;

#nullable enable
namespace VSMVVM.WPF.Sample.ViewModels
{
    /// <summary>
    /// 로그 엔트리 모델. UI ListView 바인딩용.
    /// </summary>
    public class LogEntry
    {
        public string Timestamp { get; set; } = "";
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public string Exception { get; set; } = "";
    }

    /// <summary>
    /// ILoggerService 데모 ViewModel. 각 로그 레벨에서 메시지를 기록하고 실시간으로 표시합니다.
    /// </summary>
    public partial class LoggingViewModel : ViewModelBase
    {
        private const int MaxLogEntries = 100;
        private readonly ILoggerService _logger;

        [Property]
        private string _messageInput = "Hello VSMVVM!";

        [Property]
        private string _selectedLevel = "Info";

        [Property]
        private string _logOutput = "";

        [Property]
        private int _logCount;

        /// <summary>
        /// UI에 표시할 로그 엔트리 목록.
        /// </summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        /// <summary>
        /// 사용 가능한 로그 레벨 목록.
        /// </summary>
        public string[] LogLevels { get; } = { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };

        public LoggingViewModel(ILoggerService logger)
        {
            _logger = logger;
        }

        [RelayCommand]
        private void SendLog()
        {
            if (string.IsNullOrWhiteSpace(MessageInput)) return;

            var message = MessageInput;
            string? exceptionText = null;

            switch (SelectedLevel)
            {
                case "Trace":
                    _logger.Trace(message);
                    break;
                case "Debug":
                    _logger.Debug(message);
                    break;
                case "Info":
                    _logger.Info(message);
                    break;
                case "Warn":
                    _logger.Warn(message);
                    break;
                case "Error":
                    _logger.Error(message);
                    break;
                case "Fatal":
                    var ex = new InvalidOperationException("Sample fatal exception");
                    _logger.Fatal(message, ex);
                    exceptionText = ex.Message;
                    break;
            }

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                Level = SelectedLevel,
                Message = message,
                Exception = exceptionText ?? ""
            };

            LogEntries.Insert(0, entry);
            TrimEntries();
            LogCount = LogEntries.Count;
            LogOutput += $"[{entry.Timestamp}] [{entry.Level,-5}] {entry.Message}";
            if (!string.IsNullOrEmpty(exceptionText))
            {
                LogOutput += $" | Exception: {exceptionText}";
            }
            LogOutput += "\n";
        }

        [RelayCommand]
        private void ClearLogs()
        {
            LogEntries.Clear();
            LogOutput = "";
            LogCount = 0;
        }

        [RelayCommand]
        private void SendBatchLogs()
        {
            _logger.Trace("Batch: Trace level message");
            _logger.Debug("Batch: Debug level message");
            _logger.Info("Batch: Info level message");
            _logger.Warn("Batch: Warn level message");
            _logger.Error("Batch: Error level message");
            _logger.Fatal("Batch: Fatal level message");

            var levels = new[] { "Trace", "Debug", "Info", "Warn", "Error", "Fatal" };
            foreach (var level in levels)
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Level = level,
                    Message = $"Batch: {level} level message"
                };
                LogEntries.Insert(0, entry);
                LogOutput += $"[{entry.Timestamp}] [{entry.Level,-5}] {entry.Message}\n";
            }
            TrimEntries();
            LogCount = LogEntries.Count;
        }

        private void TrimEntries()
        {
            while (LogEntries.Count > MaxLogEntries)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1);
            }
        }
    }
}
