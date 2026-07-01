using System;

namespace VSMVVM.Core.Scheduler.Graph
{
    /// <summary>
    /// 그래프 단위 변수 정의. UE Blueprint 의 Variables 패널 개념.
    /// 호스트가 Get/Set 노드를 통해 사용한다. ExecutionContext.Variables 가 런타임 값 저장소.
    /// </summary>
    public sealed class GraphVariable
    {
        public string Name { get; }
        public Type ClrType { get; }
        public object DefaultValue { get; }

        public GraphVariable(string name, Type clrType, object defaultValue)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name must not be null or whitespace.", nameof(name));
            Name = name;
            ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            DefaultValue = defaultValue;
        }
    }
}
