using System;
using FluentAssertions;
using VSMVVM.WPF.Services;
using Xunit;

namespace VSMVVM.WPF.Tests.Services
{
    public class UndoRedoServiceTests
    {
        private static UndoRedoService Create() => new UndoRedoService();

        // ── 초기 상태 ────────────────────────────────────────────────

        [Fact]
        public void Initial_CanUndo_ShouldBeFalse()
        {
            // Arrange / Act
            var svc = Create();

            // Assert
            svc.CanUndo.Should().BeFalse();
        }

        [Fact]
        public void Initial_CanRedo_ShouldBeFalse()
        {
            // Arrange / Act
            var svc = Create();

            // Assert
            svc.CanRedo.Should().BeFalse();
        }

        // ── Push ─────────────────────────────────────────────────────

        [Fact]
        public void Push_WhenCalled_ShouldEnableCanUndo()
        {
            // Arrange
            var svc = Create();

            // Act
            svc.Push(() => { }, () => { });

            // Assert
            svc.CanUndo.Should().BeTrue();
        }

        [Fact]
        public void Push_WhenCalled_ShouldNotEnableCanRedo()
        {
            // Arrange
            var svc = Create();

            // Act
            svc.Push(() => { }, () => { });

            // Assert
            svc.CanRedo.Should().BeFalse();
        }

        [Fact]
        public void Push_WhenCalledAfterUndo_ShouldClearRedoStack()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });
            svc.Undo();
            svc.CanRedo.Should().BeTrue("undo 후 redo 스택에 항목 존재 전제");

            // Act
            svc.Push(() => { }, () => { });

            // Assert
            svc.CanRedo.Should().BeFalse("새 Push 는 redo 스택을 초기화해야 한다");
        }

        [Fact]
        public void Push_WhenNullUndoAction_ShouldThrowArgumentNullException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.Push(null, () => { });

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("undo");
        }

        [Fact]
        public void Push_WhenNullRedoAction_ShouldThrowArgumentNullException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.Push(() => { }, null);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("redo");
        }

        [Fact]
        public void Push_WhenExceedsCapacity_ShouldRemoveOldestEntry()
        {
            // Arrange
            var svc = Create();
            svc.Capacity = 3;
            int undoCallTarget = -1;
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                svc.Push(() => { undoCallTarget = captured; }, () => { });
            }

            // Act — 4번째 Push: Capacity=3이므로 가장 오래된 항목(i=0) 제거
            svc.Push(() => { undoCallTarget = 99; }, () => { });

            // Undo 3번 → 역순으로 3, 2, 99 순으로 호출되어야 하며, 0번은 제거됨
            svc.Undo(); // i=99
            undoCallTarget.Should().Be(99);
            svc.Undo(); // i=2
            undoCallTarget.Should().Be(2);
            svc.Undo(); // i=1
            undoCallTarget.Should().Be(1);
            svc.CanUndo.Should().BeFalse("용량 초과로 제거된 항목(i=0)은 undo 불가");
        }

        [Fact]
        public void Push_WhenStateChanged_ShouldFireEvent()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Push(() => { }, () => { });

            // Assert
            count.Should().Be(1);
        }

        // ── Undo ─────────────────────────────────────────────────────

        [Fact]
        public void Undo_WhenStackHasItems_ShouldCallUndoAction()
        {
            // Arrange
            var svc = Create();
            bool undoCalled = false;
            svc.Push(() => { undoCalled = true; }, () => { });

            // Act
            svc.Undo();

            // Assert
            undoCalled.Should().BeTrue();
        }

        [Fact]
        public void Undo_WhenStackHasItems_ShouldMoveItemToRedoStack()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });

            // Act
            svc.Undo();

            // Assert
            svc.CanUndo.Should().BeFalse();
            svc.CanRedo.Should().BeTrue();
        }

        [Fact]
        public void Undo_WhenStackEmpty_ShouldNotFireStateChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Undo();

            // Assert
            count.Should().Be(0, "빈 undo 스택에서 Undo 는 이벤트를 발화하지 않는다");
        }

        [Fact]
        public void Undo_WhenCalledTwice_ShouldExecuteActionsInLIFOOrder()
        {
            // Arrange
            var svc = Create();
            int lastUndo = -1;
            svc.Push(() => { lastUndo = 1; }, () => { });
            svc.Push(() => { lastUndo = 2; }, () => { });

            // Act
            svc.Undo();
            int firstUndoResult = lastUndo;
            svc.Undo();
            int secondUndoResult = lastUndo;

            // Assert — 마지막 Push 항목이 먼저 undo
            firstUndoResult.Should().Be(2, "마지막 push 항목이 먼저 undo");
            secondUndoResult.Should().Be(1);
        }

        [Fact]
        public void Undo_WhenStateChanged_ShouldFireEvent()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Undo();

            // Assert
            count.Should().Be(1);
        }

        // ── Redo ─────────────────────────────────────────────────────

        [Fact]
        public void Redo_WhenRedoStackHasItems_ShouldCallRedoAction()
        {
            // Arrange
            var svc = Create();
            bool redoCalled = false;
            svc.Push(() => { }, () => { redoCalled = true; });
            svc.Undo();

            // Act
            svc.Redo();

            // Assert
            redoCalled.Should().BeTrue();
        }

        [Fact]
        public void Redo_WhenRedoStackHasItems_ShouldMoveItemBackToUndoStack()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });
            svc.Undo();

            // Act
            svc.Redo();

            // Assert
            svc.CanUndo.Should().BeTrue();
            svc.CanRedo.Should().BeFalse();
        }

        [Fact]
        public void Redo_WhenStackEmpty_ShouldNotFireStateChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Redo();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void Redo_WhenStateChanged_ShouldFireEvent()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });
            svc.Undo();
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Redo();

            // Assert
            count.Should().Be(1);
        }

        // ── Clear ────────────────────────────────────────────────────

        [Fact]
        public void Clear_WhenBothStacksHaveItems_ShouldEmptyBothStacks()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });
            svc.Push(() => { }, () => { });
            svc.Undo();

            // Act
            svc.Clear();

            // Assert
            svc.CanUndo.Should().BeFalse();
            svc.CanRedo.Should().BeFalse();
        }

        [Fact]
        public void Clear_WhenStacksNotEmpty_ShouldFireStateChanged()
        {
            // Arrange
            var svc = Create();
            svc.Push(() => { }, () => { });
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Clear();

            // Assert
            count.Should().Be(1);
        }

        [Fact]
        public void Clear_WhenBothStacksEmpty_ShouldNotFireStateChanged()
        {
            // Arrange
            var svc = Create();
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Clear();

            // Assert
            count.Should().Be(0, "비어있을 때 Clear 는 StateChanged 를 발화하지 않는다");
        }

        // ── Capacity ─────────────────────────────────────────────────

        [Fact]
        public void Capacity_WhenSetToZero_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.Capacity = 0;

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Capacity_WhenSetToNegative_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            var svc = Create();

            // Act
            Action act = () => svc.Capacity = -1;

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Capacity_WhenReducedBelowCurrentCount_ShouldTrimOldestEntries()
        {
            // Arrange
            var svc = Create();
            int lastUndo = -1;
            for (int i = 0; i < 5; i++)
            {
                int captured = i;
                svc.Push(() => { lastUndo = captured; }, () => { });
            }

            // Act — 용량을 현재 크기(5) 미만으로 줄임
            svc.Capacity = 2;

            // Assert — 2개 항목만 남음 (가장 최근 2개: i=3, i=4)
            svc.Undo(); lastUndo.Should().Be(4);
            svc.Undo(); lastUndo.Should().Be(3);
            svc.CanUndo.Should().BeFalse("정리 후 2개 항목만 남음");
        }

        // ── 인터페이스 명시적 이벤트 구현 확인 ───────────────────────

        [Fact]
        public void InterfaceStateChanged_WhenPushed_ShouldFireEvent()
        {
            // Arrange
            IUndoRedoService svc = Create();
            int count = 0;
            svc.StateChanged += (_, _) => count++;

            // Act
            svc.Push(() => { }, () => { });

            // Assert
            count.Should().Be(1, "인터페이스 명시적 이벤트도 정상 발화해야 한다");
        }
    }
}
