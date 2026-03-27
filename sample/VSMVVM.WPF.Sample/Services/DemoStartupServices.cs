using System.Threading;

namespace VSMVVM.WPF.Sample.Services
{
    /// <summary>
    /// 스타트업 데모 서비스 인터페이스. Bootstrapper에서 순차 초기화를 시연합니다.
    /// </summary>
    public interface IStartupService
    {
        string ServiceName { get; }
        void Initialize();
    }

    /// <summary>
    /// 데모 시작 서비스 A — 설정 로드 / DB 연결 시뮬레이션.
    /// </summary>
    public class StartupServiceA : IStartupService
    {
        public string ServiceName => "Configuration Service";

        public void Initialize()
        {
            // 실제 환경: 설정 파일 로드, DB 연결 풀 초기화 등
            Thread.Sleep(600);
        }
    }

    /// <summary>
    /// 데모 시작 서비스 B — 캐시 / 플러그인 로드 시뮬레이션.
    /// </summary>
    public class StartupServiceB : IStartupService
    {
        public string ServiceName => "Plugin Service";

        public void Initialize()
        {
            // 실제 환경: 캐시 워밍업, 플러그인 탐색/로드 등
            Thread.Sleep(600);
        }
    }
}
