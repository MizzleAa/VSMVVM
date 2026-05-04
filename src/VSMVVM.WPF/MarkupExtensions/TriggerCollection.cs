using System.Windows;
using VSMVVM.WPF.Behaviors.Triggers;
using EventTrigger = VSMVVM.WPF.Behaviors.Triggers.EventTrigger;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// <see cref="Interaction.TriggersProperty"/>에 첨부되는 <see cref="EventTrigger"/> 컬렉션.
    /// </summary>
    public sealed class TriggerCollection : FreezableCollection<EventTrigger>
    {
        protected override Freezable CreateInstanceCore() => new TriggerCollection();
    }
}
