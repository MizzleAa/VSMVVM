using System.Runtime.Loader;

namespace VSMVVM.Core.Scheduler.Scripting
{
    /// <summary>
    /// 사용자 코드 어셈블리 격리를 위한 collectible AssemblyLoadContext.
    /// Unload() 호출 시 해당 컨텍스트에 로드된 어셈블리가 GC 회수 가능 상태가 됩니다.
    /// </summary>
    internal sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext(string name)
            : base(name, isCollectible: true)
        {
        }
    }
}
