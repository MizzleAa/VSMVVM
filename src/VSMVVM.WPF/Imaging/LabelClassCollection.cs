using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

#nullable enable
namespace VSMVVM.WPF.Imaging
{
    /// <summary>
    /// <see cref="LabelClass"/> 컬렉션. Index 0 = 배경은 생성 시 자동 예약되며 제거할 수 없다.
    /// </summary>
    public class LabelClassCollection : ObservableCollection<LabelClass>
    {
        /// <summary>배경 라벨의 인덱스.</summary>
        public const int BackgroundIndex = 0;

        /// <summary>라벨 인덱스의 최댓값(마스크가 byte 저장이라 0~255).</summary>
        public const int MaxIndex = 255;

        public LabelClassCollection()
        {
            // 배경 라벨 예약
            base.Add(new LabelClass
            {
                Index = BackgroundIndex,
                Name = "Background",
                Color = Colors.Transparent,
                IsVisible = true,
            });
        }

        /// <summary>다음 사용 가능한 최소 인덱스(1~255)를 반환.</summary>
        public int NextFreeIndex()
        {
            for (int i = 1; i <= MaxIndex; i++)
            {
                if (!this.Any(l => l.Index == i))
                    return i;
            }
            throw new InvalidOperationException("라벨 인덱스 1~255가 모두 사용 중입니다.");
        }

        /// <summary>이름/색상으로 새 라벨을 추가하고 반환.</summary>
        public LabelClass Add(string name, Color color)
        {
            var label = new LabelClass
            {
                Index = NextFreeIndex(),
                Name = name,
                Color = color,
                IsVisible = true,
            };
            Add(label);
            return label;
        }

        /// <summary>인덱스로 라벨을 찾는다. 없으면 null.</summary>
        public LabelClass? GetByIndex(int index)
        {
            return this.FirstOrDefault(l => l.Index == index);
        }

        /// <summary>배경(Index=0) 제거를 거부하는 오버라이드.</summary>
        protected override void RemoveItem(int index)
        {
            if (index >= 0 && index < Count && this[index].Index == BackgroundIndex)
                return; // 배경 라벨은 제거 불가
            base.RemoveItem(index);
        }

        /// <summary>배경 라벨을 다른 것으로 덮어쓰지 못하도록.</summary>
        protected override void SetItem(int index, LabelClass item)
        {
            if (index >= 0 && index < Count && this[index].Index == BackgroundIndex)
                return;
            base.SetItem(index, item);
        }

        /// <summary>전체 Clear 시에도 배경은 유지.</summary>
        protected override void ClearItems()
        {
            base.ClearItems();
            base.Add(new LabelClass
            {
                Index = BackgroundIndex,
                Name = "Background",
                Color = Colors.Transparent,
                IsVisible = true,
            });
        }
    }
}
