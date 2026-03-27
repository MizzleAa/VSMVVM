namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 빌드 메타데이터 조회 서비스 인터페이스.
    /// </summary>
    public interface IBuildInfoService
    {
        /// <summary>
        /// 앱 버전 (예: "1.0.0.0").
        /// </summary>
        string AppVersion { get; }

        /// <summary>
        /// 빌드 시간 (예: "2026-03-24 17:00:00").
        /// </summary>
        string BuildTime { get; }

        /// <summary>
        /// .NET 프레임워크 버전 (예: ".NET 8.0.0").
        /// </summary>
        string FrameworkVersion { get; }

        /// <summary>
        /// OS 아키텍처 (예: "x64").
        /// </summary>
        string OSArchitecture { get; }
    }
}
