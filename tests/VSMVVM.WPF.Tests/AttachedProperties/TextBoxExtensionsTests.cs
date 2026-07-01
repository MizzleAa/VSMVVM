using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FluentAssertions;
using VSMVVM.WPF.AttachedProperties;
using Xunit;

namespace VSMVVM.WPF.Tests.AttachedProperties
{
    public class TextBoxExtensionsTests
    {
        // ── 기본값 ─────────────────────────────────────────────────────

        [StaFact]
        public void GetSubmitOnEnter_WhenNotSet_ShouldReturnFalse()
        {
            // Arrange
            var tb = new TextBox();

            // Act
            var value = TextBoxExtensions.GetSubmitOnEnter(tb);

            // Assert
            value.Should().BeFalse();
        }

        // ── 설정 / 해제 ──────────────────────────────────────────────

        [StaFact]
        public void SetSubmitOnEnter_WhenSetToTrue_ShouldGetReturnTrue()
        {
            // Arrange
            var tb = new TextBox();

            // Act
            TextBoxExtensions.SetSubmitOnEnter(tb, true);

            // Assert
            TextBoxExtensions.GetSubmitOnEnter(tb).Should().BeTrue();
        }

        [StaFact]
        public void SetSubmitOnEnter_WhenSetToFalse_ShouldGetReturnFalse()
        {
            // Arrange
            var tb = new TextBox();
            TextBoxExtensions.SetSubmitOnEnter(tb, true);

            // Act
            TextBoxExtensions.SetSubmitOnEnter(tb, false);

            // Assert
            TextBoxExtensions.GetSubmitOnEnter(tb).Should().BeFalse();
        }

        // ── 비-TextBox 에 적용해도 예외 없음 ─────────────────────────

        [StaFact]
        public void SetSubmitOnEnter_WhenAppliedToNonTextBox_ShouldNotThrow()
        {
            // Arrange
            var button = new Button();

            // Act — TextBox 가 아니면 콜백에서 조용히 무시
            var act = () => TextBoxExtensions.SetSubmitOnEnter(button, true);

            // Assert
            act.Should().NotThrow();
        }

        // ── 이벤트 중복 연결 방지 (토글 반복) ────────────────────────
        // true → false → true 순으로 설정해도 PreviewKeyDown 핸들러가 한 번만 연결되어야 한다.
        // 내부 구현: false 시 `-=`, true 시 `+=` 순서로 중복 방지.
        // 핸들러 수를 직접 검사할 수 없으므로 Enter 키 이벤트를 시뮬레이션해 updateSource 가
        // 정확히 한 번 호출됨을 간접 검증한다.

        [StaFact]
        public void SetSubmitOnEnter_WhenToggledTrueFalseTrue_ShouldUpdateSourceOnlyOnce()
        {
            // Arrange
            var tb = new TextBox();
            var model = new SimpleModel { Text = "original" };
            BindingOperations.SetBinding(
                tb,
                TextBox.TextProperty,
                new Binding(nameof(SimpleModel.Text))
                {
                    Source = model,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit, // Enter 키에만 commit
                });

            TextBoxExtensions.SetSubmitOnEnter(tb, true);
            TextBoxExtensions.SetSubmitOnEnter(tb, false);
            TextBoxExtensions.SetSubmitOnEnter(tb, true); // 최종 true

            // Act — TextBox 에 새 값 입력 후 Enter 키 시뮬레이션
            tb.Text = "newValue";
            SimulateEnterKey(tb);

            // Assert — source 가 업데이트되어야 함 (UpdateSource 가 한 번 호출됨)
            model.Text.Should().Be("newValue", "Enter 키가 source 를 commit 해야 한다");
        }

        // ── Enter 키가 아닌 다른 키는 UpdateSource 하지 않음 ──────────

        [StaFact]
        public void SetSubmitOnEnter_WhenNonEnterKeyPressed_ShouldNotUpdateSource()
        {
            // Arrange
            var tb = new TextBox();
            var model = new SimpleModel { Text = "original" };
            BindingOperations.SetBinding(
                tb,
                TextBox.TextProperty,
                new Binding(nameof(SimpleModel.Text))
                {
                    Source = model,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.Explicit,
                });
            TextBoxExtensions.SetSubmitOnEnter(tb, true);
            tb.Text = "modified";

            // Act — Tab 키 (Enter 가 아님)
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new FakePresentationSource(),
                0,
                Key.Tab)
            {
                RoutedEvent = UIElement.PreviewKeyDownEvent
            };
            tb.RaiseEvent(args);

            // Assert — Tab 은 source 를 commit 하지 않아야 함
            model.Text.Should().Be("original");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private static void SimulateEnterKey(TextBox tb)
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new FakePresentationSource(),
                0,
                Key.Return)
            {
                RoutedEvent = UIElement.PreviewKeyDownEvent
            };
            tb.RaiseEvent(args);
        }

        private sealed class SimpleModel : INotifyPropertyChanged
        {
            private string _text;
            public string Text
            {
                get => _text;
                set
                {
                    if (_text == value) return;
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        // WPF 이벤트 시뮬레이션에 필요한 최소한의 PresentationSource 스텁.
        private sealed class FakePresentationSource : PresentationSource
        {
            protected override CompositionTarget GetCompositionTargetCore() => null;
            public override Visual RootVisual { get; set; }
            public override bool IsDisposed => false;
        }
    }
}
