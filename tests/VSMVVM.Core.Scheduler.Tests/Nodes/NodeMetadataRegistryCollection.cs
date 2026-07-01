using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Nodes
{
    /// <summary>
    /// NodeMetadataRegistry 는 process-wide singleton. 같은 어셈블리에서 RegisterFromAssembly 를
    /// 호출하거나 UnregisterForTests 로 정리하는 테스트들이 병렬 실행되면 race condition 발생.
    /// 모든 그런 테스트를 같은 xunit collection 에 넣어 직렬화 보장.
    /// </summary>
    [CollectionDefinition(nameof(NodeMetadataRegistryCollection), DisableParallelization = true)]
    public sealed class NodeMetadataRegistryCollection
    {
    }
}
