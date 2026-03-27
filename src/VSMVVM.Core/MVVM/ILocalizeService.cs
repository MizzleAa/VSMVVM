using System.Resources;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 다국어 로컬라이제이션 서비스 인터페이스.
    /// </summary>
    public interface ILocalizeService
    {
        /// <summary>
        /// 현재 로케일 문자열 (예: "ko", "en").
        /// </summary>
        string CurrentLocale { get; }

        /// <summary>
        /// ResourceManager를 설정합니다.
        /// </summary>
        void SetResourceManager(ResourceManager resourceManager);

        /// <summary>
        /// 로케일을 변경합니다.
        /// </summary>
        void ChangeLocale(string locale);

        /// <summary>
        /// 리소스 키에 해당하는 문자열을 반환합니다.
        /// </summary>
        string GetString(string key);

        /// <summary>
        /// 로케일 변경 시 발생하는 이벤트.
        /// </summary>
        event System.Action<string> LocaleChanged;
    }
}
