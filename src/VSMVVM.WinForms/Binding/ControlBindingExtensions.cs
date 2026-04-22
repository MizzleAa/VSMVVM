using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Forms;

namespace VSMVVM.WinForms.Binding
{
    /// <summary>
    /// Property 단방향/양방향 바인딩 확장 메서드.
    /// 사용자가 INotifyPropertyChanged 구독이나 Dispose 해제 신경 안 쓰게 합니다.
    /// </summary>
    public static class ControlBindingExtensions
    {
        #region Label / Text (one-way)

        public static BindingHandle BindText<TVm>(this Label label, TVm vm, Expression<Func<TVm, string>> path)
            => BindOneWay(label, vm, path, v => label.Text = v ?? string.Empty);

        public static BindingHandle BindText<TVm, TValue>(this Label label, TVm vm, Expression<Func<TVm, TValue>> path)
            => BindOneWay(label, vm, path, v => label.Text = v?.ToString() ?? string.Empty);

        #endregion

        #region TextBox (two-way)

        public static BindingHandle BindText<TVm>(this TextBox textBox, TVm vm, Expression<Func<TVm, string>> path, bool twoWay = true)
        {
            var propName = GetPropertyName(path);
            var getter = path.Compile();
            var setter = twoWay ? BuildSetter(path) : null;

            bool suppress = false;

            void Apply()
            {
                var v = getter(vm) ?? string.Empty;
                if (textBox.Text == v) return;
                suppress = true;
                try { textBox.Text = v; }
                finally { suppress = false; }
            }

            Apply();

            PropertyChangedEventHandler onPc = (s, e) =>
            {
                if (e.PropertyName != propName) return;
                if (textBox.InvokeRequired)
                {
                    try { textBox.BeginInvoke((Action)Apply); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }
                else
                {
                    Apply();
                }
            };

            EventHandler onTextChanged = null;
            if (setter != null)
            {
                onTextChanged = (s, e) =>
                {
                    if (suppress) return;
                    setter(vm, textBox.Text);
                };
                textBox.TextChanged += onTextChanged;
            }

            var inpc = vm as INotifyPropertyChanged;
            if (inpc != null) inpc.PropertyChanged += onPc;

            return new BindingHandle(textBox, () =>
            {
                if (inpc != null) inpc.PropertyChanged -= onPc;
                if (onTextChanged != null) textBox.TextChanged -= onTextChanged;
            });
        }

        #endregion

        #region Visible / Enabled (one-way bool)

        public static BindingHandle BindVisible<TVm>(this Control control, TVm vm, Expression<Func<TVm, bool>> path)
            => BindOneWay(control, vm, path, v => control.Visible = v);

        public static BindingHandle BindEnabled<TVm>(this Control control, TVm vm, Expression<Func<TVm, bool>> path)
            => BindOneWay(control, vm, path, v => control.Enabled = v);

        #endregion

        #region Checked (two-way)

        public static BindingHandle BindChecked<TVm>(this CheckBox checkBox, TVm vm, Expression<Func<TVm, bool>> path, bool twoWay = true)
        {
            var propName = GetPropertyName(path);
            var getter = path.Compile();
            var setter = twoWay ? BuildSetter(path) : null;

            bool suppress = false;

            void Apply()
            {
                var v = getter(vm);
                if (checkBox.Checked == v) return;
                suppress = true;
                try { checkBox.Checked = v; }
                finally { suppress = false; }
            }

            Apply();

            PropertyChangedEventHandler onPc = (s, e) =>
            {
                if (e.PropertyName != propName) return;
                if (checkBox.InvokeRequired)
                {
                    try { checkBox.BeginInvoke((Action)Apply); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }
                else
                {
                    Apply();
                }
            };

            EventHandler onCc = null;
            if (setter != null)
            {
                onCc = (s, e) =>
                {
                    if (suppress) return;
                    setter(vm, checkBox.Checked);
                };
                checkBox.CheckedChanged += onCc;
            }

            var inpc = vm as INotifyPropertyChanged;
            if (inpc != null) inpc.PropertyChanged += onPc;

            return new BindingHandle(checkBox, () =>
            {
                if (inpc != null) inpc.PropertyChanged -= onPc;
                if (onCc != null) checkBox.CheckedChanged -= onCc;
            });
        }

        #endregion

        #region ForeColor / BackColor (one-way)

        public static BindingHandle BindForeColor<TVm>(this Control control, TVm vm, Expression<Func<TVm, Color>> path)
            => BindOneWay(control, vm, path, v => control.ForeColor = v);

        public static BindingHandle BindBackColor<TVm>(this Control control, TVm vm, Expression<Func<TVm, Color>> path)
            => BindOneWay(control, vm, path, v => control.BackColor = v);

        #endregion

        #region Core one-way helper

        private static BindingHandle BindOneWay<TVm, TValue>(
            Control owner,
            TVm vm,
            Expression<Func<TVm, TValue>> path,
            Action<TValue> apply)
        {
            var propName = GetPropertyName(path);
            var getter = path.Compile();

            void Invoke()
            {
                var v = getter(vm);
                apply(v);
            }

            Invoke();

            PropertyChangedEventHandler onPc = (s, e) =>
            {
                if (e.PropertyName != propName) return;
                if (owner.InvokeRequired)
                {
                    try { owner.BeginInvoke((Action)Invoke); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }
                else
                {
                    Invoke();
                }
            };

            var inpc = vm as INotifyPropertyChanged;
            if (inpc != null) inpc.PropertyChanged += onPc;

            return new BindingHandle(owner, () =>
            {
                if (inpc != null) inpc.PropertyChanged -= onPc;
            });
        }

        #endregion

        #region Expression parsing

        private static string GetPropertyName<TVm, TValue>(Expression<Func<TVm, TValue>> path)
        {
            if (path.Body is MemberExpression me)
                return me.Member.Name;
            if (path.Body is UnaryExpression ue && ue.Operand is MemberExpression me2)
                return me2.Member.Name;
            throw new ArgumentException("Binding path must be a simple property accessor (e.g. v => v.Name).", nameof(path));
        }

        private static Action<TVm, TValue> BuildSetter<TVm, TValue>(Expression<Func<TVm, TValue>> path)
        {
            if (!(path.Body is MemberExpression me)) return null;
            if (!(me.Member is PropertyInfo pi) || !pi.CanWrite) return null;
            return (vm, value) => pi.SetValue(vm, value);
        }

        #endregion
    }
}
