using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// CollectionChanged 이벤트를 배치 모드로 제어할 수 있는 ObservableCollection.
    /// 대량 추가/삭제 시 이벤트를 한 번만 발생시켜 UI 성능을 최적화합니다.
    /// </summary>
    public class BatchObservableCollection<T> : ObservableCollection<T>
    {
        #region Fields

        // 재진입 안전을 위해 depth counter 사용.
        // BeginBatch/AddRange/RemoveRange가 중첩 호출되어도 깊이가 0이 될 때만 최종 발화한다.
        private int _batchDepth;
        private bool _hasChanges;

        #endregion

        #region Constructors

        public BatchObservableCollection()
        {
        }

        public BatchObservableCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 배치 작업을 시작합니다. using 블록과 함께 사용하세요.
        /// 중첩 호출 안전: BatchScope가 모두 dispose되어 깊이가 0이 될 때만 발화합니다.
        /// </summary>
        public IDisposable BeginBatch()
        {
            EnterBatch();
            return new BatchScope(this);
        }

        /// <summary>
        /// 컬렉션에 여러 항목을 한 번에 추가합니다.
        /// CollectionChanged 이벤트는 완료 후 1회 발생합니다.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            EnterBatch();
            try
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                    _hasChanges = true;
                }
            }
            finally
            {
                ExitBatch();
            }
        }

        /// <summary>
        /// 컬렉션에서 여러 항목을 한 번에 제거합니다.
        /// </summary>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            EnterBatch();
            try
            {
                foreach (var item in items)
                {
                    if (Items.Remove(item))
                    {
                        _hasChanges = true;
                    }
                }
            }
            finally
            {
                ExitBatch();
            }
        }

        #endregion

        #region Overrides

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (_batchDepth > 0)
            {
                _hasChanges = true;
                return;
            }

            base.OnCollectionChanged(e);
        }

        #endregion

        #region Private Methods

        private void EnterBatch()
        {
            _batchDepth++;
        }

        private void ExitBatch()
        {
            if (_batchDepth <= 0)
                return;

            _batchDepth--;

            if (_batchDepth == 0 && _hasChanges)
            {
                _hasChanges = false;
                base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        #endregion

        #region Inner Class

        private sealed class BatchScope : IDisposable
        {
            private readonly BatchObservableCollection<T> _collection;
            private bool _disposed;

            public BatchScope(BatchObservableCollection<T> collection)
            {
                _collection = collection;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                _collection.ExitBatch();
            }
        }

        #endregion
    }
}
