using System;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Sample.Services
{
    /// <summary>
    /// Sample ILoggerService 구현. 콘솔 출력 기반의 간단한 데모용 로거.
    /// 프로덕션에서는 NLog, Serilog 등으로 교체합니다.
    /// </summary>
    public class SampleLoggerService : ILoggerService
    {
        private string _configPath;

        public void Configure(string configFilePath)
        {
            _configPath = configFilePath;
            System.Diagnostics.Debug.WriteLine($"[Logger] Configured: {configFilePath}");
        }

        public void Trace(string message)
            => WriteLog("TRC", message);

        public void Debug(string message)
            => WriteLog("DBG", message);

        public void Info(string message)
            => WriteLog("INF", message);

        public void Warn(string message)
            => WriteLog("WRN", message);

        public void Error(string message)
            => WriteLog("ERR", message);

        public void Error(string message, Exception exception)
            => WriteLog("ERR", $"{message} | {exception?.GetType().Name}: {exception?.Message}");

        public void Fatal(string message)
            => WriteLog("FTL", message);

        public void Fatal(string message, Exception exception)
            => WriteLog("FTL", $"{message} | {exception?.GetType().Name}: {exception?.Message}");

        private void WriteLog(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            System.Diagnostics.Debug.WriteLine($"[{timestamp}] [{level}] {message}");
        }
    }
}
