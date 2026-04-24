using System;
using VSMVVM.WPF.Imaging;

#nullable enable
namespace VSMVVM.WPF.Controls.Behaviors
{
    /// <summary>
    /// <see cref="MaskBehavior"/>가 stroke/IO 작업 완료 시 VM에 전달하는 Full 스냅샷 쌍.
    /// VM은 MaskLayer 인스턴스를 참조하지 않고 <see cref="Restore"/> 델리게이트로만 복원한다.
    /// 라벨/인스턴스 버퍼와 인스턴스 메타데이터를 모두 보존한다.
    /// </summary>
    public sealed class MaskStrokeCompletedArgs
    {
        public MaskStrokeCompletedArgs(MaskLayerSnapshot before, MaskLayerSnapshot after, Action<MaskLayerSnapshot> restore)
        {
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
            Restore = restore ?? throw new ArgumentNullException(nameof(restore));
        }

        public MaskLayerSnapshot Before { get; }
        public MaskLayerSnapshot After { get; }

        /// <summary>주어진 스냅샷을 MaskLayer에 복원한다. Undo/Redo에서 사용.</summary>
        public Action<MaskLayerSnapshot> Restore { get; }
    }
}