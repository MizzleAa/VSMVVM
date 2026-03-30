using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// Region 매니저 인터페이스. 네비게이션 + Back/Forward 히스토리 지원.
    /// </summary>
    public interface IRegionManager
    {
        #region Registration

        /// <summary>
        /// Region 컨트롤을 등록합니다.
        /// </summary>
        void Register(string regionName, IRegion region);

        /// <summary>
        /// Region 초기 View를 매핑합니다.
        /// </summary>
        void Mapping<TView>(string regionName) where TView : class;

        /// <summary>
        /// Region 초기 View를 매핑합니다 (Type 기반).
        /// </summary>
        void Mapping(string regionName, Type viewType);

        /// <summary>
        /// Region 등록을 해제합니다.
        /// </summary>
        void Cleanup(string regionName);

        #endregion

        #region Navigation

        /// <summary>
        /// 제네릭 타입으로 네비게이션합니다.
        /// </summary>
        void Navigate<TView>(string regionName) where TView : class;

        /// <summary>
        /// 제네릭 타입 + 컨텍스트로 네비게이션합니다.
        /// </summary>
        void Navigate<TView>(string regionName, NavigationContext context) where TView : class;

        /// <summary>
        /// View 이름 문자열로 네비게이션합니다.
        /// </summary>
        void Navigate(string regionName, string viewName);

        /// <summary>
        /// View 이름 + 컨텍스트로 네비게이션합니다.
        /// </summary>
        void Navigate(string regionName, string viewName, NavigationContext context);

        /// <summary>
        /// Type으로 네비게이션합니다.
        /// </summary>
        void Navigate(string regionName, Type viewType, NavigationContext navigationContext = null);

        /// <summary>
        /// Region 콘텐츠를 숨깁니다.
        /// </summary>
        void Hide(string regionName);

        #endregion

        #region Navigation History

        /// <summary>
        /// 이전 View로 돌아갑니다.
        /// </summary>
        void GoBack(string regionName);

        /// <summary>
        /// 앞으로 이동합니다.
        /// </summary>
        void GoForward(string regionName);

        /// <summary>
        /// 뒤로 갈 수 있는지 확인합니다.
        /// </summary>
        bool CanGoBack(string regionName);

        /// <summary>
        /// 앞으로 갈 수 있는지 확인합니다.
        /// </summary>
        bool CanGoForward(string regionName);

        /// <summary>
        /// 현재 Region에 표시 중인 View의 Type 이름을 반환합니다.
        /// </summary>
        string GetCurrentViewTypeName(string regionName);

        /// <summary>
        /// 현재 Region에 표시 중인 View의 표시 이름을 반환합니다.
        /// "View" 접미사 제거 후 PascalCase를 공백으로 분리합니다.
        /// 예: "DefaultDesignView" → "Default Design"
        /// </summary>
        string GetCurrentViewDisplayName(string regionName);

        #endregion
    }
}
