using System.Collections.Generic;
using System.Windows;

namespace VSMVVM.WPF.MarkupExtensions
{
    /// <summary>
    /// Behavior를 XAML에서 연결하기 위한 부착 프로퍼티.
    /// </summary>
    public static class Interaction
    {
        #region Triggers

        /// <summary>
        /// Triggers 부착 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty TriggersProperty =
            DependencyProperty.RegisterAttached(
                "Triggers",
                typeof(List<Behaviors.Triggers.EventTrigger>),
                typeof(Interaction),
                new PropertyMetadata(null, OnTriggersChanged));

        /// <summary>
        /// Triggers를 가져옵니다.
        /// </summary>
        public static List<Behaviors.Triggers.EventTrigger> GetTriggers(DependencyObject obj)
        {
            var triggers = (List<Behaviors.Triggers.EventTrigger>)obj.GetValue(TriggersProperty);
            if (triggers == null)
            {
                triggers = new List<Behaviors.Triggers.EventTrigger>();
                obj.SetValue(TriggersProperty, triggers);
            }

            return triggers;
        }

        /// <summary>
        /// Triggers를 설정합니다.
        /// </summary>
        public static void SetTriggers(DependencyObject obj, List<Behaviors.Triggers.EventTrigger> value)
        {
            obj.SetValue(TriggersProperty, value);
        }

        #endregion

        #region Behaviors

        /// <summary>
        /// Behaviors 부착 프로퍼티.
        /// </summary>
        public static readonly DependencyProperty BehaviorsProperty =
            DependencyProperty.RegisterAttached(
                "Behaviors",
                typeof(List<Behaviors.Behaviors.EventToCommandBehavior>),
                typeof(Interaction),
                new PropertyMetadata(null, OnBehaviorsChanged));

        /// <summary>
        /// Behaviors를 가져옵니다.
        /// </summary>
        public static List<Behaviors.Behaviors.EventToCommandBehavior> GetBehaviors(DependencyObject obj)
        {
            var behaviors = (List<Behaviors.Behaviors.EventToCommandBehavior>)obj.GetValue(BehaviorsProperty);
            if (behaviors == null)
            {
                behaviors = new List<Behaviors.Behaviors.EventToCommandBehavior>();
                obj.SetValue(BehaviorsProperty, behaviors);
            }

            return behaviors;
        }

        /// <summary>
        /// Behaviors를 설정합니다.
        /// </summary>
        public static void SetBehaviors(DependencyObject obj, List<Behaviors.Behaviors.EventToCommandBehavior> value)
        {
            obj.SetValue(BehaviorsProperty, value);
        }

        #endregion

        #region Private Methods

        private static void OnTriggersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element))
            {
                return;
            }

            if (e.NewValue is List<Behaviors.Triggers.EventTrigger> triggers)
            {
                foreach (var trigger in triggers)
                {
                    trigger.Attach(element);
                }
            }
        }

        private static void OnBehaviorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element))
            {
                return;
            }

            if (e.NewValue is List<Behaviors.Behaviors.EventToCommandBehavior> behaviors)
            {
                foreach (var behavior in behaviors)
                {
                    behavior.Attach(element);
                }
            }
        }

        #endregion
    }
}
