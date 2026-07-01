using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes
{
    /// <summary>
    /// 노드의 추상 베이스. 구현 클래스는 <see cref="GetPinDescriptors"/>를 통해 핀 구성을 제공하고
    /// (Source Generator가 [Node] 부착 클래스에 partial로 emit), <see cref="ExecuteAsync"/>를 오버라이드합니다.
    /// </summary>
    public abstract class NodeBase : INode
    {
        private Guid _id;
        private Lazy<IReadOnlyList<IPin>> _pins;
        private readonly Dictionary<string, object> _literalInputs = new(StringComparer.Ordinal);

        public Guid Id => _id;
        public abstract string TypeId { get; }
        public IReadOnlyList<IPin> Pins => _pins.Value;

        /// <summary>
        /// 가변 핀 노드(예: LogNode 의 ArgCount 변경, ParameterGroupNode 의 PinCount 변경)가 핀 구성을 바꾼 후 호출.
        /// 다음 <see cref="Pins"/> 접근 시 GetPinDescriptors() 가 다시 호출되어 새 핀 목록이 만들어진다.
        /// </summary>
        protected void InvalidatePins()
        {
            _pins = new Lazy<IReadOnlyList<IPin>>(BuildPins);
        }

        /// <summary>
        /// 미연결 데이터 입력 핀의 사용자 리터럴 값. 직렬화 시 NodeDto.Inputs에 emit되고,
        /// ExecutionContext.GetInput이 연결 없는 경우 PinDescriptor.DefaultValue 보다 우선합니다.
        /// </summary>
        public IReadOnlyDictionary<string, object> LiteralInputs => _literalInputs;

        /// <summary>리터럴 값을 설정/제거. value가 null이면 제거 (DefaultValue로 폴백).</summary>
        public void SetLiteralInput(string pinId, object value)
        {
            if (pinId == null) throw new ArgumentNullException(nameof(pinId));
            if (value == null) _literalInputs.Remove(pinId);
            else _literalInputs[pinId] = value;
        }

        protected NodeBase()
        {
            _id = Guid.NewGuid();
            _pins = new Lazy<IReadOnlyList<IPin>>(BuildPins);
        }

        /// <summary>
        /// 노드 Id를 재설정. NodeGraphSerializer.Deserialize 가 라운드트립 시 원본 Id를 복원하기 위해 사용.
        /// 그 외 호출 금지.
        /// </summary>
        internal void SetIdForDeserialization(Guid id) => _id = id;

        /// <summary>
        /// 노드의 추가 인스턴스 상태를 JSON으로 직렬화. 기본은 no-op.
        /// LiteralInputs는 별도로 처리되므로 여기에 다시 emit할 필요 없음.
        /// </summary>
        public virtual void WriteState(Utf8JsonWriter writer) { }

        /// <summary>WriteState가 emit한 JSON 객체를 받아 인스턴스 상태를 복원. 기본은 no-op.</summary>
        public virtual void ReadState(JsonElement state) { }

        /// <summary>
        /// 핀 디스크립터 반환. Source Generator가 partial로 override 합니다.
        /// 수동 노드 작성 시(혹은 Phase 1 테스트)에는 직접 override.
        /// </summary>
        protected abstract IReadOnlyList<PinDescriptor> GetPinDescriptors();

        private IReadOnlyList<IPin> BuildPins()
        {
            var descriptors = GetPinDescriptors() ?? Array.Empty<PinDescriptor>();
            var typeArgs = (this as IPolymorphicNode)?.TypeArguments;
            var list = new List<IPin>(descriptors.Count);
            foreach (var d in descriptors)
            {
                list.Add(CreatePinFromDescriptor(d, typeArgs));
            }
            return list;
        }

        private IPin CreatePinFromDescriptor(PinDescriptor d, IReadOnlyDictionary<string, Type> typeArgs)
        {
            if (d.Kind == PinKind.Exec)
            {
                return new ExecPin(d.Id, d.DisplayName, d.Direction, this);
            }

            // Phase K — 다형성 핀이면 TypeArguments 로 실제 타입 치환. 누락 시 object 폴백.
            var valueType = d.ValueType;
            if (d.IsPolymorphic && typeArgs != null && typeArgs.TryGetValue(d.TypeParameterName, out var concrete) && concrete != null)
            {
                valueType = concrete;
            }

            // Data 핀은 강타입 DataPin<T>를 reflection으로 생성하여 ValueType을 정확히 보존.
            var pinType = typeof(DataPin<>).MakeGenericType(valueType);
            return (IPin)Activator.CreateInstance(pinType, d.Id, d.DisplayName, d.Direction, this, d.DefaultValue);
        }

        public virtual Task<ExecutionFlow> ExecuteAsync(ExecutionContext context)
        {
            return Task.FromResult(ExecutionFlow.Halt);
        }

        public virtual Task EvaluateAsync(ExecutionContext context)
        {
            return ExecuteAsync(context);
        }
    }
}
