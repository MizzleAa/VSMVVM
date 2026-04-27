using System;
using System.Collections.Generic;

#nullable enable
namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// Undo/Redo 스택 서비스.
    /// </summary>
    public interface IUndoRedoService
    {
        bool CanUndo { get; }
        bool CanRedo { get; }

        /// <summary>상태 변경(Push/Undo/Redo/Clear) 시 발화.</summary>
        event EventHandler StateChanged;

        /// <summary>새 액션을 Push한다. 이미 실행된 후의 상태를 전제로 하며, 호출 시점에서 undo/redo는 실행하지 않는다.</summary>
        void Push(Action undo, Action redo);

        void Undo();
        void Redo();
        void Clear();
    }

    /// <summary>
    /// 기본 Undo/Redo 구현. 두 개의 스택과 상한(<see cref="Capacity"/>, 기본 50)을 가진다.
    /// </summary>
    public sealed class UndoRedoService : IUndoRedoService
    {
        private readonly LinkedList<(Action undo, Action redo)> _undo = new();
        private readonly Stack<(Action undo, Action redo)> _redo = new();
        private int _capacity = 50;

        /// <summary>파라미터 없는 기본 생성자. DI 컨테이너 호환.</summary>
        public UndoRedoService() { }

        /// <summary>Undo 스택의 최대 크기. 초과 시 오래된 항목부터 제거된다. 양수여야 한다.</summary>
        public int Capacity
        {
            get => _capacity;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _capacity = value;
                while (_undo.Count > _capacity)
                    _undo.RemoveFirst();
            }
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public event EventHandler? StateChanged;

        public void Push(Action undo, Action redo)
        {
            if (undo == null) throw new ArgumentNullException(nameof(undo));
            if (redo == null) throw new ArgumentNullException(nameof(redo));

            _undo.AddLast((undo, redo));
            while (_undo.Count > _capacity)
                _undo.RemoveFirst();

            _redo.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var last = _undo.Last!.Value;
            _undo.RemoveLast();
            last.undo();
            _redo.Push(last);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var next = _redo.Pop();
            next.redo();
            _undo.AddLast(next);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            if (_undo.Count == 0 && _redo.Count == 0) return;
            _undo.Clear();
            _redo.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        // 인터페이스의 event를 non-nullable로 선언하면서 구현의 nullable event로 구현하기 위함
        event EventHandler IUndoRedoService.StateChanged
        {
            add => StateChanged += value;
            remove => StateChanged -= value;
        }
    }
}
