using System;
using VSMVVM.Core.Scheduler.Nodes;

namespace VSMVVM.Core.Scheduler.Pins
{
    /// <summary>데이터 핀의 비제네릭 베이스. ValueType이 핀의 타입을 결정합니다.</summary>
    public abstract class DataPin : IPin
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PinDirection Direction { get; }
        public PinKind Kind => PinKind.Data;
        public abstract Type ValueType { get; }
        public INode Owner { get; }

        /// <summary>핀이 미연결일 때 사용하는 기본값. Input 핀에 의미가 있음.</summary>
        public object DefaultValue { get; }

        protected DataPin(string id, string displayName, PinDirection direction, INode owner, object defaultValue)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            Direction = direction;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            DefaultValue = defaultValue;
        }
    }

    /// <summary>강타입 데이터 핀.</summary>
    public sealed class DataPin<T> : DataPin
    {
        public override Type ValueType => typeof(T);

        public new T DefaultValue => (T)base.DefaultValue;

        public DataPin(string id, string displayName, PinDirection direction, INode owner, T defaultValue = default)
            : base(id, displayName, direction, owner, defaultValue)
        {
        }
    }
}
