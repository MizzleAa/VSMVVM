using System;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// View 이름과 ViewModel 타입 간의 매핑을 관리하는 인터페이스.
    /// ViewModelLocator에서 자동 바인딩에 사용됩니다.
    /// </summary>
    public interface IViewModelMapper
    {
        /// <summary>
        /// View 타입과 ViewModel 타입을 매핑합니다.
        /// </summary>
        void Register<TView, TViewModel>() where TView : class where TViewModel : class;

        /// <summary>
        /// View 타입으로 매핑된 ViewModel 타입을 조회합니다.
        /// </summary>
        Type GetViewModelType(Type viewType);

        /// <summary>
        /// 매핑 존재 여부를 확인합니다.
        /// </summary>
        bool HasMapping(Type viewType);
    }
}
