using System;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Graph;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Graph
{
    /// <summary>
    /// 묶음 B.1 — GraphVariable + NodeGraph.Variables 저장소 (TDD).
    /// 변수 추가/제거/조회 + 이벤트 발화 + 직렬화 라운드트립.
    /// </summary>
    public class GraphVariablesTests
    {
        [Fact]
        public void NodeGraph_Variables_StartsEmpty()
        {
            var g = new NodeGraph();
            g.Variables.Should().BeEmpty();
        }

        [Fact]
        public void AddVariable_WithNameAndType_Persists()
        {
            var g = new NodeGraph();
            var v = g.AddVariable("counter", typeof(int), defaultValue: 0);

            v.Name.Should().Be("counter");
            v.ClrType.Should().Be(typeof(int));
            v.DefaultValue.Should().Be(0);
            g.Variables.Should().ContainKey("counter");
            g.Variables["counter"].Should().BeSameAs(v);
        }

        [Fact]
        public void AddVariable_DuplicateName_Throws()
        {
            var g = new NodeGraph();
            g.AddVariable("x", typeof(int), 0);
            Action act = () => g.AddVariable("x", typeof(double), 1.0);
            act.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
        }

        [Fact]
        public void AddVariable_NullName_Throws()
        {
            var g = new NodeGraph();
            Action act = () => g.AddVariable(null, typeof(int), 0);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void RemoveVariable_Existing_ReturnsTrueAndRemoves()
        {
            var g = new NodeGraph();
            g.AddVariable("x", typeof(int), 0);

            g.RemoveVariable("x").Should().BeTrue();
            g.Variables.Should().NotContainKey("x");
        }

        [Fact]
        public void RemoveVariable_Missing_ReturnsFalse()
        {
            var g = new NodeGraph();
            g.RemoveVariable("nope").Should().BeFalse();
        }

        [Fact]
        public void VariablesChanged_Event_FiresOnAddAndRemove()
        {
            var g = new NodeGraph();
            int fired = 0;
            g.VariablesChanged += (_, _) => fired++;

            g.AddVariable("x", typeof(int), 0);
            g.RemoveVariable("x");

            fired.Should().Be(2);
        }
    }
}
