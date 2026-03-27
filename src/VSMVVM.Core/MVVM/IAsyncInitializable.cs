using System.Threading.Tasks;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// View가 Region에 들어올 때 비동기 초기화를 수행하기 위한 인터페이스.
    /// </summary>
    public interface IAsyncInitializable
    {
        Task InitializeAsync();
    }
}
