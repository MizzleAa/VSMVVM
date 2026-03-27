using System.Windows;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// Freezable 기반 바인딩 프록시.
    /// DataContext 범위 밖 (DataGrid 컬럼, ContextMenu 등)에서 바인딩이 필요할 때 사용합니다.
    /// </summary>
    public sealed class BindingProxy : Freezable
    {
        #region DependencyProperties

        /// <summary>
        /// Data 종속성 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(
                nameof(Data),
                typeof(object),
                typeof(BindingProxy),
                new PropertyMetadata(null));

        /// <summary>
        /// 바인딩 대상 데이터.
        /// </summary>
        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        #endregion

        #region Freezable Override

        /// <summary>
        /// Freezable 인스턴스를 생성합니다.
        /// </summary>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        #endregion
    }
}
