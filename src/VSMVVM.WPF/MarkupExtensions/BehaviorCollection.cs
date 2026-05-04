using System.Windows;
using VSMVVM.WPF.Behaviors.Behaviors;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// <see cref="Interaction.BehaviorsProperty"/>에 첨부되는
    /// <see cref="EventToCommandBehavior"/> 컬렉션.
    /// </summary>
    public sealed class BehaviorCollection : FreezableCollection<EventToCommandBehavior>
    {
        protected override Freezable CreateInstanceCore() => new BehaviorCollection();
    }
}
