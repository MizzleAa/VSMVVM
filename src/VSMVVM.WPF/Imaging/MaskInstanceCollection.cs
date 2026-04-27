using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// <see cref="MaskInstance"/> 컬렉션. 단조증가 ID 발번, 인덱스/라벨 조회 유틸 제공.
    /// </summary>
    public class MaskInstanceCollection : ObservableCollection<MaskInstance>
    {
        /// <summary>0은 배경 예약 ID.</summary>
        public const uint BackgroundId = 0;

        private uint _nextId = 1;

        /// <summary>다음 사용 가능한 인스턴스 ID 를 반환하고 내부 카운터를 증가시킨다.</summary>
        public uint NextId() => _nextId++;

        /// <summary>현재 카운터 값을 발번 없이 조회. Diff 기록에서 nextId before/after 비교용.</summary>
        public uint PeekNextId() => _nextId;

        /// <summary>ID 로 인스턴스를 찾는다. 없으면 null.</summary>
        public MaskInstance? GetById(uint id)
        {
            foreach (var inst in this)
                if (inst.Id == id) return inst;
            return null;
        }

        /// <summary>특정 라벨에 속한 인스턴스들을 필터.</summary>
        public IEnumerable<MaskInstance> ByLabel(int labelIndex)
            => this.Where(i => i.LabelIndex == labelIndex);

        /// <summary>
        /// 발번 카운터를 강제 갱신. 스냅샷 복원 시 기존 ID 최대값보다 커야 하므로 호출.
        /// </summary>
        public void EnsureNextIdAtLeast(uint value)
        {
            if (value >= _nextId) _nextId = value + 1;
        }
    }
}