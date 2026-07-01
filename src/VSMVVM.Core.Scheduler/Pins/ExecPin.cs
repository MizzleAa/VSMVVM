using System;
using VSMVVM.Core.Scheduler.Nodes;

namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>제어 흐름(Exec) 핀. ValueType은 항상 typeof(void).</summary>
    public sealed class ExecPin : IPin
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PinDirection Direction { get; }
        public PinKind Kind => PinKind.Exec;
        public Type ValueType => typeof(void);
        public INode Owner { get; }

        public ExecPin(string id, string displayName, PinDirection direction, INode owner)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            Direction = direction;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }
    }
}
