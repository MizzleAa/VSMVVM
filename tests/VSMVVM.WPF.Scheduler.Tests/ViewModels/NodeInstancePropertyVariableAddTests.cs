using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Nodes;
using VSMVVM.Core.Scheduler.Nodes.BuiltIn;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;
using VSMVVM.WPF.Scheduler.ViewModels;
using Xunit;
using ExecutionContext = VSMVVM.Core.Scheduler.Runtime.ExecutionContext;

namespace VSMVVM.WPF.Scheduler.Tests.ViewModels
{
    /// <summary>
    /// Phase M — 인스펙터의 Variable ComboBox 가 비어있는 그래프에서도 사용자가 변수를 즉시 추가할 수 있도록
    /// "+ Add new variable…" 마법 항목을 항상 마지막에 노출.
    /// </summary>
    [Collection(nameof(PaletteRegistryCollection))]
    public class NodeInstancePropertyVariableAddTests
    {
        public NodeInstancePropertyVariableAddTests()
        {
            BuiltInNodes.EnsureRegistered();
        }

        [Fact]
        public void VariableName_Candidates_AlwaysEndsWith_AddNewMagicItem()
        {
            var setNode = new SetVariableNode();
            var nvm = new NodeViewModel(setNode, 0, 0)
            {
                VariableNameCandidatesProvider = () => new[] { "score", "name" },
                AddNewVariableRequested = () => "newVar",
            };
            nvm.RefreshInstancePropertyCandidates();

            var variableProp = FindVariableProperty(nvm);
            variableProp.Candidates.Should().NotBeEmpty();
            variableProp.Candidates[^1].Should().Be(NodeInstancePropertyViewModel.AddNewVariableMagicValue);
            variableProp.Candidates.Should().Contain("score");
            variableProp.Candidates.Should().Contain("name");
        }

        [Fact]
        public void EmptyVariables_StillExposes_AddNewMagicItem()
        {
            var getNode = new GetVariableNode();
            var nvm = new NodeViewModel(getNode, 0, 0)
            {
                VariableNameCandidatesProvider = () => System.Array.Empty<string>(),
                AddNewVariableRequested = () => "auto1",
            };
            nvm.RefreshInstancePropertyCandidates();

            var variableProp = FindVariableProperty(nvm);
            variableProp.Candidates.Should().ContainSingle()
                .Which.Should().Be(NodeInstancePropertyViewModel.AddNewVariableMagicValue);
        }

        [Fact]
        public void SelectingMagicItem_InvokesCallback_AndSetsValueToReturnedName()
        {
            int callCount = 0;
            var setNode = new SetVariableNode();
            var nvm = new NodeViewModel(setNode, 0, 0)
            {
                VariableNameCandidatesProvider = () => System.Array.Empty<string>(),
                AddNewVariableRequested = () =>
                {
                    callCount++;
                    return "createdVar";
                },
            };
            nvm.RefreshInstancePropertyCandidates();

            var variableProp = FindVariableProperty(nvm);
            variableProp.Value = NodeInstancePropertyViewModel.AddNewVariableMagicValue;

            callCount.Should().Be(1);
            setNode.VariableName.Should().Be("createdVar");
        }

        [Fact]
        public void CallbackReturnsNull_KeepsExistingValue()
        {
            var setNode = new SetVariableNode { VariableName = "original" };
            var nvm = new NodeViewModel(setNode, 0, 0)
            {
                VariableNameCandidatesProvider = () => new[] { "original" },
                AddNewVariableRequested = () => null, // 사용자 취소
            };
            nvm.RefreshInstancePropertyCandidates();

            var variableProp = FindVariableProperty(nvm);
            variableProp.Value = NodeInstancePropertyViewModel.AddNewVariableMagicValue;

            setNode.VariableName.Should().Be("original");
        }

        [Fact]
        public void NoCallback_MagicItem_NotExposed()
        {
            var setNode = new SetVariableNode();
            var nvm = new NodeViewModel(setNode, 0, 0)
            {
                VariableNameCandidatesProvider = () => new[] { "x" },
                // AddNewVariableRequested null — 마법 항목 미노출.
            };
            nvm.RefreshInstancePropertyCandidates();

            var variableProp = FindVariableProperty(nvm);
            variableProp.Candidates.Should().NotContain(NodeInstancePropertyViewModel.AddNewVariableMagicValue);
            variableProp.Candidates.Should().Equal("x");
        }

        private static NodeInstancePropertyViewModel FindVariableProperty(NodeViewModel nvm)
        {
            foreach (var p in nvm.InstanceProperties)
                if (p.Kind == NodeInstancePropertyKind.VariableName) return p;
            throw new System.InvalidOperationException("Variable property not found.");
        }
    }
}
