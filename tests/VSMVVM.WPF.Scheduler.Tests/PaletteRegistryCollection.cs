using Xunit;

// NodeMetadataRegistry 는 프로세스 전역 싱글톤. 어셈블리 전체의 컬렉션 병렬 실행을 끄고
// 테스트가 typeId 를 register/unregister 하는 동안 다른 테스트가 GetCategories 를 호출해 오염되는 것을 막는다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VSMVVM.WPF.Scheduler.Tests
{
    /// <summary>
    /// 팔레트/Registry 를 건드리는 테스트 클래스가 공유할 collection (직렬 실행). 본 assembly 는 이미
    /// <c>DisableTestParallelization=true</c> 로 모든 테스트가 직렬이라 collection 자체는 보조 정렬 용도.
    /// </summary>
    [CollectionDefinition(nameof(PaletteRegistryCollection), DisableParallelization = true)]
    public class PaletteRegistryCollection { }
}
