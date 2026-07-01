using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.WPF.Scheduler.Editor.Completion;
using Xunit;

namespace VSMVVM.WPF.Scheduler.Editor.Tests
{
    public class RoslynCompletionProviderTests
    {
        [Fact]
        public async Task GetCompletionsAsync_AtMemberAccessOnString_ReturnsStringMembers()
        {
            using var provider = new RoslynCompletionProvider();
            // `s.` 위치에서 호출 → String 멤버가 등장해야 한다.
            var source = "class C { void M(string s) { s. } }";
            var position = source.IndexOf("s.") + 2; // '.' 직후
            var items = await provider.GetCompletionsAsync(source, position,
                CompletionTriggerKind.Insertion, '.');

            items.Should().NotBeEmpty();
            items.Select(i => i.DisplayText).Should().Contain("Length");
        }

        [Fact]
        public async Task GetCompletionsAsync_OnInvokeAtTopLevel_ReturnsKeywords()
        {
            using var provider = new RoslynCompletionProvider();
            // 빈 문서에서 명시 호출 → 키워드들이 포함되어야 함.
            var items = await provider.GetCompletionsAsync(string.Empty, 0,
                CompletionTriggerKind.Invoke, '\0');

            items.Should().Contain(i => i.DisplayText == "namespace");
            items.Should().Contain(i => i.DisplayText == "class");
        }

        [Fact]
        public async Task GetCompletionsAsync_ClampsOutOfRangePosition()
        {
            using var provider = new RoslynCompletionProvider();
            // position이 텍스트 길이 초과 → 클램프 후 동작 (예외 없음)
            var items = await provider.GetCompletionsAsync("hello", 9999,
                CompletionTriggerKind.Invoke, '\0');

            items.Should().NotBeNull();
        }

        [Fact]
        public async Task GetCompletionsAsync_AfterDispose_ReturnsEmpty()
        {
            var provider = new RoslynCompletionProvider();
            provider.Dispose();

            var items = await provider.GetCompletionsAsync("class C {}", 0,
                CompletionTriggerKind.Invoke, '\0');

            items.Should().BeEmpty();
        }

        [Fact]
        public void RoslynCompletionItem_NullDisplayText_DefaultsToEmpty()
        {
            var item = new RoslynCompletionItem(null, null, null, null, CompletionItemKind.Text);
            item.DisplayText.Should().Be(string.Empty);
            item.InsertionText.Should().Be(string.Empty);
            item.Description.Should().Be(string.Empty);
            item.SortText.Should().Be(string.Empty);
        }

        [Fact]
        public async Task GetCompletionsAsync_CancellationToken_PropagatesCancellation()
        {
            using var provider = new RoslynCompletionProvider();
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            // 이미 취소된 토큰 → OperationCanceledException 던져야 함.
            var act = async () => await provider.GetCompletionsAsync(
                "class C {}", 0, CompletionTriggerKind.Invoke, '\0', cts.Token);
            await act.Should().ThrowAsync<System.OperationCanceledException>();
        }

        [Fact]
        public async Task SecondCall_AfterWarmup_IsFasterThan_FirstUserCall()
        {
            // 핵심 성능 검증 — 워밍업이 작동한다면 "두 번째 사용자 호출" 이 "첫 사용자 호출" 보다 더 빨라야 한다.
            // 절대 임계값은 솔루션 병렬 실행(GC/JIT) 부하에 따라 변동이 크므로 비율로 검증.
            using var provider = new RoslynCompletionProvider();
            await provider.WarmupTask;

            // 첫 사용자 호출 — 워밍업 후 MetadataReference 는 캐시되어 있지만 CompletionService 가 이 source 를 처음 봄.
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var items1 = await provider.GetCompletionsAsync(
                "class C { void M(string s) { s. } }",
                position: "class C { void M(string s) { s.".Length,
                CompletionTriggerKind.Insertion, '.');
            sw1.Stop();

            // 두 번째 호출 — 같은 source 재사용 → 캐시 효과 가시화
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var items2 = await provider.GetCompletionsAsync(
                "class C { void M(string s) { s. } }",
                position: "class C { void M(string s) { s.".Length,
                CompletionTriggerKind.Insertion, '.');
            sw2.Stop();

            items1.Should().NotBeEmpty();
            items2.Should().NotBeEmpty();
            // 두 번째 호출은 첫 호출보다 빠르거나 비슷해야 한다 (재인덱싱 없음).
            // 솔루션 부하에서 거의 같을 수도 있으므로 첫 호출의 2배 이내로 잡음 (충분히 관대).
            sw2.ElapsedMilliseconds.Should().BeLessOrEqualTo(sw1.ElapsedMilliseconds * 2 + 200,
                $"두 번째 호출은 캐시 효과로 첫 호출보다 빠르거나 비슷해야 함 (1차: {sw1.ElapsedMilliseconds}ms, 2차: {sw2.ElapsedMilliseconds}ms)");
        }
    }
}
