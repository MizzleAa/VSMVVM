namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Region 네비게이션 시 ViewModel에 네비게이션 이벤트를 통지받기 위한 인터페이스.
    /// </summary>
    public interface INavigateAware
    {
        /// <summary>
        /// 네비게이션 가능 여부를 반환합니다.
        /// </summary>
        bool CanNavigate(NavigationContext context);

        /// <summary>
        /// 이 View로 네비게이션될 때 호출됩니다.
        /// </summary>
        void OnNavigatedTo(NavigationContext context);

        /// <summary>
        /// 이 View에서 나갈 때 호출됩니다.
        /// </summary>
        void OnNavigatedFrom(NavigationContext context);
    }
}
