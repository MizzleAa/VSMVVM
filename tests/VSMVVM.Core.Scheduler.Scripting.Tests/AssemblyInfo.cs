using Xunit;

// Roslyn 컴파일 + CustomNodeFactory.RegisterFromAssembly 가 프로세스 전역 NodeMetadataRegistry 를 변이.
// 어셈블리 전체 비-병렬화로 typeId race 차단.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
