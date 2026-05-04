using System.Collections.Generic;
using System.Windows;
using VSMVVM.WPF.Behaviors.Base;

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

        #region GeneralBehaviors — 일반 Behavior&lt;T&gt; 컬렉션

        /// <summary>
        /// GeneralBehaviors 부착 프로퍼티. 모든 <see cref="BehaviorBase"/> 파생 베비어를 받는다.
        /// EventToCommandBehavior 외 일반 Behavior&lt;T&gt; 들 (마우스/키보드/사이즈 베비어 등)을
        /// XAML에서 attach 하기 위한 컬렉션.
        /// </summary>
        public static readonly DependencyProperty GeneralBehaviorsProperty =
            DependencyProperty.RegisterAttached(
                "GeneralBehaviors",
                typeof(List<BehaviorBase>),
                typeof(Interaction),
                new PropertyMetadata(null, OnGeneralBehaviorsChanged));

        /// <summary>GeneralBehaviors를 가져옵니다.</summary>
        public static List<BehaviorBase> GetGeneralBehaviors(DependencyObject obj)
        {
            var list = (List<BehaviorBase>)obj.GetValue(GeneralBehaviorsProperty);
            if (list == null)
            {
                list = new List<BehaviorBase>();
                obj.SetValue(GeneralBehaviorsProperty, list);
            }

            return list;
        }

        /// <summary>GeneralBehaviors를 설정합니다.</summary>
        public static void SetGeneralBehaviors(DependencyObject obj, List<BehaviorBase> value)
        {
            obj.SetValue(GeneralBehaviorsProperty, value);
        }

        private static void OnGeneralBehaviorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is List<BehaviorBase> behaviors)
            {
                foreach (var behavior in behaviors)
                {
                    behavior.AttachInternal(d);
                }
            }
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
