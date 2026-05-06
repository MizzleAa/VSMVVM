using System;
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
        /// 로케일 변경을 구독합니다. 반환된 IDisposable.Dispose() 호출 시 구독 해제됩니다.
        /// 콜백은 strong-ref 로 보관되므로 호출자가 IDisposable 을 보관하지 않으면 메모리 누수 가능 — Subscriptions 컬렉션 사용 권장.
        /// </summary>
        IDisposable Subscribe(Action<string> callback);
    }
}