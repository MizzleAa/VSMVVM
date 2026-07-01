using System;
using System.Windows;
using System.Windows.Input;
using FluentAssertions;
using VSMVVM.WPF.Behaviors.Behaviors;
using Xunit;

namespace VSMVVM.WPF.Tests.Behaviors
{
    public class EventToCommandBehaviorTests
    {
        // ── OnAttached / 이벤트 구독 ──────────────────────────────────

        [StaFact]
        public void OnAttached_WhenEventNameAndCommandSet_ShouldFireCommandOnEvent()
        {
            // Arrange
            bool executed = false;
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = nameof(TestElement.TestEvent),
                Command = new RelayStub(() => { executed = true; }),
            };

            // Act
            behavior.Attach(element);
            element.FireTestEvent();

            // Assert
            executed.Should().BeTrue("이벤트 발생 시 커맨드가 실행되어야 한다");
        }

        [StaFact]
        public void OnDetaching_WhenDetached_ShouldNotFireCommandAfterDetach()
        {
            // Arrange
            int count = 0;
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = nameof(TestElement.TestEvent),
                Command = new RelayStub(() => count++),
            };
            behavior.Attach(element);
            element.FireTestEvent(); // 구독 확인 (count=1)

            // Act
            behavior.Detach();
            element.FireTestEvent(); // 해제 후 — count 변화 없어야 함

            // Assert
            count.Should().Be(1, "Detach 후에는 이벤트가 커맨드를 실행하지 않아야 한다");
        }

        [StaFact]
        public void OnAttached_WhenCommandCannotExecute_ShouldNotExecuteCommand()
        {
            // Arrange
            bool executed = false;
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = nameof(TestElement.TestEvent),
                Command = new RelayStub(() => { executed = true; }, () => false), // CanExecute = false
            };

            // Act
            behavior.Attach(element);
            element.FireTestEvent();

            // Assert
            executed.Should().BeFalse("CanExecute=false 이면 커맨드가 실행되지 않아야 한다");
        }

        [StaFact]
        public void OnAttached_WhenCommandIsNull_ShouldNotThrow()
        {
            // Arrange
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = nameof(TestElement.TestEvent),
                Command = null,
            };

            // Act
            behavior.Attach(element);
            var act = () => element.FireTestEvent();

            // Assert
            act.Should().NotThrow("Command 가 null 이면 이벤트 발생 시 조용히 무시해야 한다");
        }

        [StaFact]
        public void OnAttached_WhenEventNameIsEmpty_ShouldNotThrow()
        {
            // Arrange
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = string.Empty,
                Command = new RelayStub(() => { }),
            };

            // Act
            var act = () => behavior.Attach(element);

            // Assert
            act.Should().NotThrow("EventName 이 비어있으면 조용히 무시해야 한다");
        }

        [StaFact]
        public void OnAttached_WhenEventNotFoundOnTarget_ShouldNotThrow()
        {
            // Arrange
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = "NonExistentEvent",
                Command = new RelayStub(() => { }),
            };

            // Act
            var act = () => behavior.Attach(element);

            // Assert
            act.Should().NotThrow("존재하지 않는 이벤트명은 조용히 무시해야 한다");
        }

        [StaFact]
        public void CommandParameter_WhenSet_ShouldBePassedToCommand()
        {
            // Arrange
            object receivedParam = null;
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = nameof(TestElement.TestEvent),
                Command = new RelayStub<object>(p => { receivedParam = p; }),
                CommandParameter = "custom-param",
            };

            // Act
            behavior.Attach(element);
            element.FireTestEvent();

            // Assert
            receivedParam.Should().Be("custom-param", "CommandParameter 가 설정되면 해당 값이 커맨드에 전달되어야 한다");
        }

        [StaFact]
        public void CommandParameter_WhenNull_ShouldPassEventArgsToCommand()
        {
            // Arrange
            object receivedParam = null;
            var element = new TestElement();
            var behavior = new EventToCommandBehavior
            {
                EventName = nameof(TestElement.TestEvent),
                Command = new RelayStub<object>(p => { receivedParam = p; }),
                // CommandParameter = null (기본)
            };

            // Act
            behavior.Attach(element);
            element.FireTestEvent();

            // Assert — CommandParameter 가 null 이면 EventArgs 가 파라미터로 전달됨
            receivedParam.Should().BeOfType<EventArgs>("CommandParameter 가 없으면 EventArgs 가 전달되어야 한다");
        }

        // ── AttachInternal 타입 불일치 ────────────────────────────────

        [StaFact]
        public void AttachInternal_WhenWrongType_ShouldThrowInvalidOperationException()
        {
            // Arrange — EventToCommandBehavior 는 FrameworkElement 를 기대
            var behavior = new EventToCommandBehavior();
            var wrongTarget = new System.Windows.Media.DrawingGroup(); // FrameworkElement 아님

            // Act
            var act = () => behavior.AttachInternal(wrongTarget);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        // ── 헬퍼 타입 ─────────────────────────────────────────────────

        private sealed class TestElement : FrameworkElement
        {
            public event EventHandler TestEvent;
            public void FireTestEvent() => TestEvent?.Invoke(this, EventArgs.Empty);
        }

        private sealed class RelayStub : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public RelayStub(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute ?? (() => true);
            }

            public bool CanExecute(object parameter) => _canExecute();
            public void Execute(object parameter) => _execute();
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }

        private sealed class RelayStub<T> : ICommand
        {
            private readonly Action<T> _execute;
            public RelayStub(Action<T> execute) => _execute = execute;
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => _execute((T)parameter);
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }
    }
}
