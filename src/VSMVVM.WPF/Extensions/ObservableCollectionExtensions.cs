using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace VSMVVM.WPF.Extensions
{
    /// <summary>
    /// ObservableCollection 갱신 헬퍼. WPF UI 스레드 점유를 분산시켜
    /// LoadingOverlay 같은 애니메이션이 frame 진행을 유지하도록 한다.
    /// </summary>
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// 컬렉션을 Clear 하고 새 items 로 채우되, batchSize 항목마다
        /// Dispatcher.Yield(Background) 로 양보. 매 yield 사이에 layout/render pass 가
        /// 끼어들어 spinner / progress 같은 애니메이션이 정상 진행됨.
        /// 반드시 UI 스레드에서 호출해야 함.
        /// </summary>
        /// <param name="batchSize">한 번에 추가할 항목 수. 기본 5.
        /// 0 이하면 Yield 없이 한 번에 추가 (작은 컬렉션용).</param>
        public static async Task ReplaceWithAsync<T>(
            this ObservableCollection<T> collection,
            IEnumerable<T> items,
            int batchSize = 5)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            if (items == null) throw new ArgumentNullException(nameof(items));

            collection.Clear();

            // Clear 직후 한 번 양보 — 컨테이너 정리 + 애니메이션 frame.
            if (batchSize > 0)
                await Dispatcher.Yield(DispatcherPriority.Background);

            int i = 0;
            foreach (var item in items)
            {
                collection.Add(item);
                i++;
                if (batchSize > 0 && i % batchSize == 0)
                    await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }
    }
}