using System;
using System.Reflection;
using System.Runtime.InteropServices;
using VSMVVM.Core.MVVM;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// 빌드 메타데이터 조회 서비스 구현체.
    /// 진입 어셈블리에서 메타데이터를 추출합니다.
    /// </summary>
    public sealed class BuildInfoService : IBuildInfoService
    {
        #region Fields

        private readonly Assembly _entryAssembly;

        #endregion

        #region Constructor

        /// <summary>
        /// 기본 생성자. 진입 어셈블리에서 정보를 추출합니다.
        /// </summary>
        public BuildInfoService()
        {
            _entryAssembly = Assembly.GetEntryAssembly();
        }

        #endregion

        #region IBuildInfoService

        public string AppVersion
        {
            get
            {
                if (_entryAssembly == null)
                {
                    return "Unknown";
                }

                var version = _entryAssembly.GetName().Version;
                return version?.ToString() ?? "Unknown";
            }
        }

        public string BuildTime
        {
            get
            {
                if (_entryAssembly == null)
                {
                    return "Unknown";
                }

                var location = _entryAssembly.Location;
                if (string.IsNullOrEmpty(location))
                {
                    return "Unknown";
                }

                try
                {
                    var buildDate = System.IO.File.GetLastWriteTime(location);
                    return buildDate.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        public string FrameworkVersion => RuntimeInformation.FrameworkDescription;

        public string OSArchitecture => RuntimeInformation.OSArchitecture.ToString();

        #endregion
    }
}
