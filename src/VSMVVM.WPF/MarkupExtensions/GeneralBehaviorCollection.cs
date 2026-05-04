using System.Windows;
using VSMVVM.WPF.Behaviors.Base;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// <see cref="Interaction.GeneralBehaviorsProperty"/>에 첨부되는 <see cref="BehaviorBase"/> 컬렉션.
    /// FreezableCollection&lt;T&gt;을 상속하여 WPF XAML 파서의 implicit collection-add 패턴과
    /// 호환됩니다 (per-instance 기본값).
    /// </summary>
    public sealed class GeneralBehaviorCollection : FreezableCollection<BehaviorBase>
    {
        protected override Freezable CreateInstanceCore() => new GeneralBehaviorCollection();
    }
}
