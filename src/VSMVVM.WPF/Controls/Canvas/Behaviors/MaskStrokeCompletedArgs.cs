using System;
using VSMVVM.WPF.Imaging;

#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// <see cref="MaskBehavior"/>가 stroke/IO/인스턴스 op 완료 시 VM 에 전달하는 args.
    /// VM 은 <see cref="UndoAction"/> / <see cref="RedoAction"/> 만 호출하면 됨 — 내부 구현(Snapshot vs Diff) 무관.
    /// Snapshot 기반(load 등 전체 교체) 과 Diff 기반(stroke/edit 등 부분 변경) 둘 다 같은 args 로 통일.
    /// </summary>
    public sealed class MaskStrokeCompletedArgs
    {
        /// <summary>Snapshot 기반 생성자 — Load 등 전체 교체용. 내부에서 Restore(before)/Restore(after) 로 wrap.</summary>
        public MaskStrokeCompletedArgs(MaskLayerSnapshot before, MaskLayerSnapshot after, Action<MaskLayerSnapshot> restore)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (restore == null) throw new ArgumentNullException(nameof(restore));

            Before = before;
            After = after;
            Restore = restore;
            UndoAction = () => restore(before);
            RedoAction = () => restore(after);
        }

        /// <summary>Diff 기반 생성자 — stroke/edit 등 부분 변경용. 메모리 절감 (200MB → 수 KB).</summary>
        public MaskStrokeCompletedArgs(Action undo, Action redo)
        {
            UndoAction = undo ?? throw new ArgumentNullException(nameof(undo));
            RedoAction = redo ?? throw new ArgumentNullException(nameof(redo));
        }

        /// <summary>Snapshot 기반 args 일 때만 non-null. Diff 기반은 null.</summary>
        public MaskLayerSnapshot? Before { get; }
        public MaskLayerSnapshot? After { get; }
        public Action<MaskLayerSnapshot>? Restore { get; }

        /// <summary>Undo 콜백. VM 이 Undo 스택에 push.</summary>
        public Action UndoAction { get; }

        /// <summary>Redo 콜백. VM 이 Redo 스택에 push.</summary>
        public Action RedoAction { get; }
    }
}