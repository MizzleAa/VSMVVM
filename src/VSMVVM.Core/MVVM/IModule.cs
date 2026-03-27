namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 모듈 인터페이스. DLL 단위의 플러그인 시스템을 지원합니다.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// 모듈 이름.
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// 모듈 버전.
        /// </summary>
        string ModuleVersion { get; }

        /// <summary>
        /// 서비스 등록 단계.
        /// </summary>
        void RegisterServices(IServiceCollection services);

        /// <summary>
        /// Region 매핑 단계.
        /// </summary>
        void RegionMapping(IRegionManager regionManager);

        /// <summary>
        /// View-ViewModel 매핑 단계.
        /// </summary>
        void ViewModelMapping(IViewModelMapper viewModelMapper);

        /// <summary>
        /// 앱 시작 단계.
        /// </summary>
        void OnStartUp(IServiceContainer container);
    }
}
