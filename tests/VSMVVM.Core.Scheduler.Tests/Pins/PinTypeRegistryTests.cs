using System;
using System.Collections.Generic;
using FluentAssertions;
using VSMVVM.Core.Scheduler.Pins;
using Xunit;

namespace VSMVVM.Core.Scheduler.Tests.Pins
{
    /// <summary>
    /// 묶음 A — PinTypeRegistry 계약 테스트 (TDD RED 단계).
    /// 외부 호스트가 사용자 클래스를 데이터 핀 타입으로 등록할 수 있는 확장 포인트.
    /// </summary>
    public class PinTypeRegistryTests
    {
        // === PinTypeInfo POCO ===

        [Fact]
        public void PinTypeInfo_RequiresClrType_AndDefaultsDisplayNameFromClrType()
        {
            var info = new PinTypeInfo(typeof(int));
            info.ClrType.Should().Be(typeof(int));
            info.DisplayName.Should().Be("Int32",
                "DisplayName 이 명시되지 않으면 ClrType.Name 사용");
            info.Category.Should().Be("Primitive");
        }

        [Fact]
        public void PinTypeInfo_Ctor_NullClrType_Throws()
        {
            Action act = () => new PinTypeInfo(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void PinTypeInfo_StableName_ForOpenGeneric_UsesShortFormWithBackticks()
        {
            // List<> 같은 open generic 의 stable name 은 "System.Collections.Generic.List`1" 형태.
            var info = new PinTypeInfo(typeof(List<>), category: "Collection");
            info.StableName.Should().Be("System.Collections.Generic.List`1");
        }

        [Fact]
        public void PinTypeInfo_StableName_ForClosedGeneric_IncludesTypeArgsByStableName()
        {
            // List<int> → "System.Collections.Generic.List`1[System.Int32]"
            var info = new PinTypeInfo(typeof(List<int>));
            info.StableName.Should().Be("System.Collections.Generic.List`1[System.Int32]");
        }

        [Fact]
        public void PinTypeInfo_StableName_ForNestedGeneric_HandlesRecursive()
        {
            var info = new PinTypeInfo(typeof(Dictionary<string, List<int>>));
            info.StableName.Should().Be(
                "System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[System.Int32]]");
        }

        // === DefaultPinTypeRegistry — 기본 등록 ===

        [Fact]
        public void DefaultPinTypeRegistry_RegistersPrimitives_OnConstruction()
        {
            var registry = new DefaultPinTypeRegistry();
            registry.Get(typeof(int)).Should().NotBeNull();
            registry.Get(typeof(double)).Should().NotBeNull();
            registry.Get(typeof(string)).Should().NotBeNull();
            registry.Get(typeof(bool)).Should().NotBeNull();
            registry.Get(typeof(object)).Should().NotBeNull();
        }

        [Fact]
        public void Register_NewType_BecomesQueryableByClrTypeAndStableName()
        {
            var registry = new DefaultPinTypeRegistry();
            // Guid 는 이미 기본 등록 — 등록되지 않은 사용자 타입으로 검증.
            var info = new PinTypeInfo(typeof(CustomNotRegistered), displayName: "Custom", category: "Custom");

            registry.Register(info);

            registry.Get(typeof(CustomNotRegistered)).Should().BeSameAs(info);
            registry.GetByStableName(typeof(CustomNotRegistered).FullName).Should().BeSameAs(info);
        }

        [Fact]
        public void Register_DuplicateClrType_ThrowsByDefault()
        {
            var registry = new DefaultPinTypeRegistry();
            var first = new PinTypeInfo(typeof(CustomNotRegistered));
            registry.Register(first);

            Action act = () => registry.Register(new PinTypeInfo(typeof(CustomNotRegistered)));
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*already registered*");
        }

        /// <summary>이 어셈블리에만 존재하고 기본 registry 에 등록되지 않은 더미 타입.</summary>
        private sealed class CustomNotRegistered { }

        [Fact]
        public void All_ReturnsAllRegisteredTypes_InRegistrationOrder()
        {
            var registry = new DefaultPinTypeRegistry();
            var initialCount = registry.All.Count;
            registry.Register(new PinTypeInfo(typeof(CustomNotRegistered)));

            registry.All.Should().HaveCount(initialCount + 1);
            registry.All[initialCount].ClrType.Should().Be(typeof(CustomNotRegistered));
        }

        // === Generic type instantiation (List<>, Dictionary<,>, Array) ===

        [Fact]
        public void GetOrCreate_ClosedGenericFromRegisteredOpenGeneric_AutoCreatesInstanceInfo()
        {
            // List<> 가 등록되어 있으면, List<int> 를 요청하면 자동으로 만들어진다.
            var registry = new DefaultPinTypeRegistry();
            // DefaultPinTypeRegistry 는 기본으로 List<> 와 Dictionary<,> 의 open generic 을 등록함.
            var listOfInt = registry.GetOrCreate(typeof(List<int>));

            listOfInt.Should().NotBeNull();
            listOfInt.ClrType.Should().Be(typeof(List<int>));
            // 한 번 만들어진 인스턴스는 캐시되어 동일 참조 반환.
            registry.GetOrCreate(typeof(List<int>)).Should().BeSameAs(listOfInt);
        }

        [Fact]
        public void GetOrCreate_DictionaryWithRegisteredArgs_AutoCreates()
        {
            var registry = new DefaultPinTypeRegistry();
            var info = registry.GetOrCreate(typeof(Dictionary<string, int>));
            info.Should().NotBeNull();
            info.ClrType.Should().Be(typeof(Dictionary<string, int>));
        }

        [Fact]
        public void GetOrCreate_ArrayOfRegisteredType_AutoCreates()
        {
            var registry = new DefaultPinTypeRegistry();
            var info = registry.GetOrCreate(typeof(int[]));
            info.Should().NotBeNull();
            info.ClrType.Should().Be(typeof(int[]));
            info.Category.Should().Be("Collection");
        }

        [Fact]
        public void GetOrCreate_UnregisteredCustomType_ReturnsNull()
        {
            var registry = new DefaultPinTypeRegistry();
            // 등록되지 않은 사용자 정의 타입은 자동 생성 안 함 — 호스트가 명시적으로 Register 해야 함.
            var info = registry.GetOrCreate(typeof(PinTypeRegistryTests));
            info.Should().BeNull();
        }
    }
}
