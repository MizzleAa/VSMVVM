using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VSMVVM.Core.Scheduler.Pins;
using VSMVVM.Core.Scheduler.Runtime;

namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 노드 인스턴스가 들고 있는 Random 캐시. Seed 가 변경되면 재생성.
    /// </summary>
    internal static class RandomPool
    {
        private static readonly Dictionary<Guid, (long seedKey, Random rng)> _byNode = new();
        private static readonly object _lock = new();

        /// <summary>seed &lt; 0 이면 매번 새 시드 (시간 기반), 그 외에는 노드별 고정.</summary>
        public static Random Get(Guid nodeId, int seed)
        {
            lock (_lock)
            {
                // seed < 0 = 매번 새 시드 — 호출 마다 다른 Random 인스턴스.
                if (seed < 0) return new Random();
                if (_byNode.TryGetValue(nodeId, out var entry) && entry.seedKey == seed)
                {
                    return entry.rng;
                }
                var rng = new Random(seed);
                _byNode[nodeId] = (seed, rng);
                return rng;
            }
        }
    }

    /// <summary>
    /// Min/Max(inclusive) 사이 정수 무작위. Seed: -1=매번 새, 0 이상=고정.
    /// </summary>
    public sealed class RandomIntNode : NodeBase
    {
        public const string TypeIdConst = "Core.Random.Int";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Min",  "Min",  PinDirection.Input,  PinKind.Data, typeof(int), 0),
            new PinDescriptor("Max",  "Max",  PinDirection.Input,  PinKind.Data, typeof(int), 100),
            new PinDescriptor("Seed", "Seed", PinDirection.Input,  PinKind.Data, typeof(int), -1),
            new PinDescriptor("Out",  "Out",  PinDirection.Output, PinKind.Data, typeof(int), 0),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var min = context.GetInput<int>(this, "Min");
            var max = context.GetInput<int>(this, "Max");
            var seed = context.GetInput<int>(this, "Seed");
            if (max < min) max = min;
            var rng = RandomPool.Get(Id, seed);
            // Random.Next(min, max) 는 max exclusive 이므로 +1.
            var v = rng.Next(min, max == int.MaxValue ? max : max + 1);
            context.SetOutput(this, "Out", v);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Random Int", "Random",
            "Random integer in [Min, Max] (inclusive). Seed -1 = fresh per call.", 0,
            typeof(RandomIntNode), () => new RandomIntNode(), PinSpec);
    }

    /// <summary>
    /// Min/Max 사이 실수 무작위. Seed: -1=매번 새, 0 이상=고정.
    /// </summary>
    public sealed class RandomDoubleNode : NodeBase
    {
        public const string TypeIdConst = "Core.Random.Double";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Min",  "Min",  PinDirection.Input,  PinKind.Data, typeof(double), 0.0),
            new PinDescriptor("Max",  "Max",  PinDirection.Input,  PinKind.Data, typeof(double), 1.0),
            new PinDescriptor("Seed", "Seed", PinDirection.Input,  PinKind.Data, typeof(int),   -1),
            new PinDescriptor("Out",  "Out",  PinDirection.Output, PinKind.Data, typeof(double), 0.0),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var min = context.GetInput<double>(this, "Min");
            var max = context.GetInput<double>(this, "Max");
            var seed = context.GetInput<int>(this, "Seed");
            if (max < min) max = min;
            var rng = RandomPool.Get(Id, seed);
            var v = min + rng.NextDouble() * (max - min);
            context.SetOutput(this, "Out", v);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Random Double", "Random",
            "Random double in [Min, Max]. Seed -1 = fresh per call.", 0,
            typeof(RandomDoubleNode), () => new RandomDoubleNode(), PinSpec);
    }

    /// <summary>
    /// 50:50 동전 던지기. Probability(0..1) 로 true 확률 조정 가능. Seed: -1=매번 새, 0 이상=고정.
    /// </summary>
    public sealed class RandomBoolNode : NodeBase
    {
        public const string TypeIdConst = "Core.Random.Bool";
        public override string TypeId => TypeIdConst;

        internal static readonly PinDescriptor[] PinSpec = new[]
        {
            new PinDescriptor("Probability", "Probability", PinDirection.Input,  PinKind.Data, typeof(double), 0.5),
            new PinDescriptor("Seed",        "Seed",        PinDirection.Input,  PinKind.Data, typeof(int),   -1),
            new PinDescriptor("Out",         "Out",         PinDirection.Output, PinKind.Data, typeof(bool),  false),
        };

        protected override IReadOnlyList<PinDescriptor> GetPinDescriptors() => PinSpec;

        public override Task EvaluateAsync(ExecutionContext context)
        {
            var p = context.GetInput<double>(this, "Probability");
            var seed = context.GetInput<int>(this, "Seed");
            if (p < 0) p = 0; else if (p > 1) p = 1;
            var rng = RandomPool.Get(Id, seed);
            var v = rng.NextDouble() < p;
            context.SetOutput(this, "Out", v);
            return Task.CompletedTask;
        }

        internal static NodeMetadata CreateMetadata() => new NodeMetadata(
            TypeIdConst, "Random Bool", "Random",
            "Random bool with given Probability (0..1). Seed -1 = fresh per call.", 0,
            typeof(RandomBoolNode), () => new RandomBoolNode(), PinSpec);
    }
}
