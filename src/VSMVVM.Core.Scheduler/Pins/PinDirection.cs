namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>핀의 방향. Input(노드로 들어옴) / Output(노드에서 나감).</summary>
    public enum PinDirection
    {
        Input,
        Output,
    }

    /// <summary>핀의 종류. Exec(제어 흐름) / Data(값 전달).</summary>
    public enum PinKind
    {
        Exec,
        Data,
    }
}
