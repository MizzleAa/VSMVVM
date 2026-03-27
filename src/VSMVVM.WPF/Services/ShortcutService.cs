using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace VSMVVM.WPF.Services
{
    /// <summary>
    /// 단축키 서비스 인터페이스.
    /// </summary>
    public interface IShortcutService
    {
        /// <summary>
        /// 글로벌 단축키 등록.
        /// </summary>
        void RegisterGlobal(Key key, ModifierKeys modifiers, Action action);

        /// <summary>
        /// 스코프 단축키 등록 (특정 FrameworkElement에서만 동작).
        /// </summary>
        void RegisterScoped(FrameworkElement scope, Key key, ModifierKeys modifiers, Action action);

        /// <summary>
        /// 글로벌 단축키 해제.
        /// </summary>
        void UnregisterGlobal(Key key, ModifierKeys modifiers);

        /// <summary>
        /// 모든 단축키 해제.
        /// </summary>
        void ClearAll();
    }

    /// <summary>
    /// 글로벌/스코프 단축키 서비스 구현체.
    /// </summary>
    public sealed class ShortcutService : IShortcutService
    {
        #region Inner Types

        /// <summary>
        /// 단축키 등록 키.
        /// </summary>
        private struct ShortcutKey : IEquatable<ShortcutKey>
        {
            public Key Key;
            public ModifierKeys Modifiers;

            public bool Equals(ShortcutKey other)
            {
                return Key == other.Key && Modifiers == other.Modifiers;
            }

            public override bool Equals(object obj)
            {
                return obj is ShortcutKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Key * 397) ^ (int)Modifiers;
                }
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<ShortcutKey, KeyBinding> _globalBindings = new Dictionary<ShortcutKey, KeyBinding>();

        #endregion

        #region IShortcutService

        public void RegisterGlobal(Key key, ModifierKeys modifiers, Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var shortcutKey = new ShortcutKey { Key = key, Modifiers = modifiers };

            // 기존 바인딩 제거
            UnregisterGlobal(key, modifiers);

            var command = new Core.MVVM.RelayCommand(action);
            var keyBinding = new KeyBinding(command, key, modifiers);

            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.InputBindings.Add(keyBinding);
            }

            _globalBindings[shortcutKey] = keyBinding;
        }

        public void RegisterScoped(FrameworkElement scope, Key key, ModifierKeys modifiers, Action action)
        {
            if (scope == null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var command = new Core.MVVM.RelayCommand(action);
            var keyBinding = new KeyBinding(command, key, modifiers);
            scope.InputBindings.Add(keyBinding);
        }

        public void UnregisterGlobal(Key key, ModifierKeys modifiers)
        {
            var shortcutKey = new ShortcutKey { Key = key, Modifiers = modifiers };

            if (_globalBindings.TryGetValue(shortcutKey, out var binding))
            {
                if (Application.Current?.MainWindow != null)
                {
                    Application.Current.MainWindow.InputBindings.Remove(binding);
                }

                _globalBindings.Remove(shortcutKey);
            }
        }

        public void ClearAll()
        {
            if (Application.Current?.MainWindow != null)
            {
                foreach (var binding in _globalBindings.Values)
                {
                    Application.Current.MainWindow.InputBindings.Remove(binding);
                }
            }

            _globalBindings.Clear();
        }

        #endregion
    }
}
