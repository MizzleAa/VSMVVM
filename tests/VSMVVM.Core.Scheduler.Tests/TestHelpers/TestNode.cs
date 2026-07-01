using System.Collections.Generic;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Pins;

namespace VSMVVM.Core.Scheduler.Tests.TestHelpers
{
    /// <summary>
    /// Phase 1 테스트용 노드. Source Generator가 없는 단계에서 핀 디스크립터를 생성자로 직접 주입.
    /// </summary>
    internal sealed class TestNode : NodeBase
    {
        private readonly string _typeId;
        private readonly IReadOnlyList<PinDescriptor> _pins;

        public TestNode(string typeId, params PinDescriptor[] pins)
        {
            _typeId = typeId;
            _pins = pins ?? System.Array.Empty<PinDescriptor>();
        }

        public override string TypeId => _typeId;

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => _pins;
    }

    /// <summary>핀 디스크립터를 짧게 생성하는 빌더.</summary>
    internal static class Pin
    {
        public static PinDescriptor ExecIn(string id = "In") =>
            new PinDescriptor(id, id, PinDirection.Input, PinKind.Exec, typeof(void), null);

        public static PinDescriptor ExecOut(string id = "Then") =>
            new PinDescriptor(id, id, PinDirection.Output, PinKind.Exec, typeof(void), null);

        public static PinDescriptor DataIn<T>(string id, T defaultValue = default) =>
            new PinDescriptor(id, id, PinDirection.Input, PinKind.Data, typeof(T), defaultValue);

        public static PinDescriptor DataOut<T>(string id) =>
            new PinDescriptor(id, id, PinDirection.Output, PinKind.Data, typeof(T), null);
    }
}
