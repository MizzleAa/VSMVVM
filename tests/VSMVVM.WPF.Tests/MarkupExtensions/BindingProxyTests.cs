using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using FluentAssertions;
using VSMVVM.WPF.MarkupExtensions;
using Xunit;

namespace VSMVVM.WPF.Tests.MarkupExtensions
{
    public class BindingProxyTests
    {
        // ── Data DP 기본 값 ───────────────────────────────────────────

        [StaFact]
        public void Data_WhenNotSet_ShouldBeNull()
        {
            // Arrange / Act
            var proxy = new BindingProxy();

            // Assert
            proxy.Data.Should().BeNull();
        }

        // ── 직접 값 설정 ──────────────────────────────────────────────

        [StaFact]
        public void Data_WhenSetDirectly_ShouldReturnValue()
        {
            // Arrange
            var proxy = new BindingProxy();

            // Act
            proxy.Data = "hello";

            // Assert
            proxy.Data.Should().Be("hello");
        }

        [StaFact]
        public void Data_WhenSetToObject_ShouldReturnSameInstance()
        {
            // Arrange
            var proxy = new BindingProxy();
            var obj = new object();

            // Act
            proxy.Data = obj;

            // Assert
            proxy.Data.Should().BeSameAs(obj);
        }

        // ── 바인딩 — OneWay 소스 변경 시 Data 업데이트 ───────────────

        [StaFact]
        public void Data_WhenBoundToSource_ShouldReceiveSourceValue()
        {
            // Arrange
            var proxy = new BindingProxy();
            var source = new SimpleModel { Value = "initial" };
            BindingOperations.SetBinding(
                proxy,
                BindingProxy.DataProperty,
                new Binding(nameof(SimpleModel.Value)) { Source = source, Mode = BindingMode.OneWay });

            // Assert 초기값
            proxy.Data.Should().Be("initial");

            // Act — 소스 변경
            source.Value = "updated";

            // Assert — proxy.Data 가 업데이트되어야 한다
            proxy.Data.Should().Be("updated");
        }

        [StaFact]
        public void Data_WhenTwoWayBound_ShouldSyncBothDirections()
        {
            // Arrange
            var proxy = new BindingProxy();
            var source = new SimpleModel { Value = "start" };
            BindingOperations.SetBinding(
                proxy,
                BindingProxy.DataProperty,
                new Binding(nameof(SimpleModel.Value))
                {
                    Source = source,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                });

            // Act — proxy 를 통해 값 변경 (→ source 로 역방향)
            proxy.Data = "changedByProxy";

            // Assert
            source.Value.Should().Be("changedByProxy");
        }

        // ── Freezable.CreateInstanceCore ─────────────────────────────

        [StaFact]
        public void CreateInstanceCore_ShouldReturnNewBindingProxy()
        {
            // Arrange
            var proxy = new BindingProxy();

            // Act — Clone() 이 CreateInstanceCore 를 호출
            var clone = proxy.Clone();

            // Assert
            clone.Should().BeOfType<BindingProxy>();
            clone.Should().NotBeSameAs(proxy);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────

        private sealed class SimpleModel : INotifyPropertyChanged
        {
            private string _value;
            public string Value
            {
                get => _value;
                set
                {
                    if (_value == value) return;
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
