using System;

namespace VSMVVM.Core.Attributes
{
    /// <summary>
    /// [Property] 필드에 적용하면, 프로퍼티 변경 시 지정된 Command의 CanExecute를 자동으로 재평가합니다.
    /// Source Generator가 setter에 CommandName.RaiseCanExecuteChanged() 호출을 삽입합니다.
    /// </summary>
    /// <example>
    /// <code>
    /// [Property]
    /// [NotifyCanExecuteChangedFor(nameof(DecrementCommand))]
    /// [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    /// private int _counter;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public sealed class NotifyCanExecuteChangedForAttribute : Attribute
    {
        /// <summary>
        /// CanExecute를 재평가할 Command 프로퍼티 이름.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// NotifyCanExecuteChangedForAttribute 생성자.
        /// </summary>
        /// <param name="commandName">CanExecute를 재평가할 Command 프로퍼티 이름 (nameof 사용 권장).</param>
        public NotifyCanExecuteChangedForAttribute(string commandName)
        {
            CommandName = commandName;
        }
    }
}
