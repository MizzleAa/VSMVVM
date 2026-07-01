using Xunit;

// WPF BAML/PackagePart 리소스 로딩이 멀티 스레드에서 race condition 을 일으켜 CI 에서 비결정적 실패.
// 어셈블리 전체 테스트를 직렬 실행 강제로 회피.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
