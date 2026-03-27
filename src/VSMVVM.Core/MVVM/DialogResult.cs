namespace VSMVVM.Core.MVVM
{
    /// <summary>
    /// 다이얼로그 결과 열거형.
    /// </summary>
    public enum DialogResultType
    {
        /// <summary>
        /// 결과 없음 (닫힘).
        /// </summary>
        None,

        /// <summary>
        /// 확인 (OK / Yes).
        /// </summary>
        OK,

        /// <summary>
        /// 취소 (Cancel).
        /// </summary>
        Cancel,

        /// <summary>
        /// 예.
        /// </summary>
        Yes,

        /// <summary>
        /// 아니오.
        /// </summary>
        No
    }

    /// <summary>
    /// 다이얼로그 버튼 프리셋.
    /// </summary>
    public enum DialogButtons
    {
        /// <summary>
        /// 확인.
        /// </summary>
        OK,

        /// <summary>
        /// 확인 · 취소.
        /// </summary>
        OKCancel,

        /// <summary>
        /// 예 · 아니오.
        /// </summary>
        YesNo,

        /// <summary>
        /// 예 · 아니오 · 취소.
        /// </summary>
        YesNoCancel
    }

    /// <summary>
    /// 다이얼로그 결과를 담는 제네릭 래퍼.
    /// </summary>
    public sealed class DialogResult<T>
    {
        /// <summary>
        /// 버튼 결과.
        /// </summary>
        public DialogResultType Result { get; }

        /// <summary>
        /// 반환 데이터.
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// 결과를 생성합니다.
        /// </summary>
        public DialogResult(DialogResultType result, T data)
        {
            Result = result;
            Data = data;
        }
    }
}
