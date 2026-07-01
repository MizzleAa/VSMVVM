using Xunit;

// NodeMetadataRegistry / 정적 필드 (ParameterNode) 등 프로세스 전역 상태를 만지는 테스트가 다수.
// 어셈블리 전체 비-병렬화로 typeId/정적필드 race 차단. 개별 테스트는 여전히 [Collection] 으로 그룹핑 가능.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
