namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 서비스 컨테이너에서 인스턴스 생성 직후 초기화 콜백을 받기 위한 인터페이스.
    /// </summary>
    public interface IServiceInitializable
    {
        void OnServiceInitialized();
    }
}
