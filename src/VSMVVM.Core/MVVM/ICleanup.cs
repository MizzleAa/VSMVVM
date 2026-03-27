namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// View가 Region에서 빠질 때 리소스를 정리하기 위한 인터페이스.
    /// </summary>
    public interface ICleanup
    {
        void Cleanup();
    }
}
