using VSMVVM.Core.Scheduler.Attributes;
using VSMVVM.Core.Scheduler.Nodes;

namespace VSMVVM.Core.Scheduler.Tests.TestHelpers
{
    /// <summary>
    /// Phase 2 source generator 검증용 노드. 테스트 어셈블리에 두어 generator가 실제로
    /// partial GetPinDescriptors + ModuleInitializer 등록을 emit하는지 확인합니다.
    /// </summary>
    [Node("Core.SmokeTest",
        DisplayName = "Smoke Test",
        Category = "Diagnostics",
        Description = "Phase 2 source generator verification.")]
    public partial class SmokeTestNode : NodeBase
    {
        [ExecInputPin]
        public object In { get; set; }

        [ExecOutputPin]
        public object Then { get; set; }

        [InputPin(DefaultValue = 1)]
        public int Count { get; set; }

        [OutputPin]
        public string Message { get; set; }
    }
}
