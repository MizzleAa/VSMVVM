namespace VSMVVM.Core.Scheduler.Nodes.BuiltIn
{
    /// <summary>
    /// 빌트인 노드 등록 진입점. 라이브러리(VSMVVM.Core.Scheduler) 자체에는 Source Generator가 적용되지 않으므로
    /// 빌트인 노드들은 [Node] 속성을 쓰지 않고 직접 NodeMetadata를 구성합니다.
    /// <see cref="EnsureRegistered"/>는 멱등(idempotent)하며, 외부에서 UnregisterForTests 등으로 일부가 지워진
    /// 상태에서도 누락된 항목만 재등록합니다.
    /// Scheduler 진입 코드(SchedulerService, NodeGraph.AddNode(typeId, ...))가 자동 호출합니다.
    /// </summary>
    public static class BuiltInNodes
    {
        private static readonly object _lock = new object();

        public static void EnsureRegistered()
        {
            lock (_lock)
            {
                EnsureOne(StartNode.TypeIdConst, StartNode.CreateMetadata);
                EnsureOne(EndNode.TypeIdConst, EndNode.CreateMetadata);
                EnsureOne(SequenceNode.TypeIdConst, SequenceNode.CreateMetadata);
                EnsureOne(BranchNode.TypeIdConst, BranchNode.CreateMetadata);
                EnsureOne(ForkNode.TypeIdConst, ForkNode.CreateMetadata);
                EnsureOne(JoinNode.TypeIdConst, JoinNode.CreateMetadata);
                EnsureOne(DelayNode.TypeIdConst, DelayNode.CreateMetadata);
                EnsureOne(RepeatNode.TypeIdConst, RepeatNode.CreateMetadata);
                EnsureOne(LogNode.TypeIdConst, LogNode.CreateMetadata);
                EnsureOne(OutputNode.TypeIdConst, OutputNode.CreateMetadata);
                EnsureOne(InputNode.TypeIdConst, InputNode.CreateMetadata);
                EnsureOne(AssertNode.TypeIdConst, AssertNode.CreateMetadata);
                EnsureOne(GuardNode.TypeIdConst, GuardNode.CreateMetadata);
                EnsureOne(RangeAssertNode.TypeIdConst, RangeAssertNode.CreateMetadata);
                // Phase L — 다형성 ConstantNode 단일 등록 (ItemType 은 인스턴스 속성).
                EnsureOne(ConstantNode.TypeIdConst, ConstantNode.CreateMetadata);
                EnsureOne(RandomIntNode.TypeIdConst, RandomIntNode.CreateMetadata);
                EnsureOne(RandomDoubleNode.TypeIdConst, RandomDoubleNode.CreateMetadata);
                EnsureOne(RandomBoolNode.TypeIdConst, RandomBoolNode.CreateMetadata);
                EnsureOne(ToggleNode.TypeIdConst, ToggleNode.CreateMetadata);

                // Phase K — 다형성 Variable Get/Set 단일 등록 (ItemType + VariableName 은 인스턴스 속성).
                EnsureOne(GetVariableNode.TypeIdConst, GetVariableNode.CreateMetadata);
                EnsureOne(SetVariableNode.TypeIdConst, SetVariableNode.CreateMetadata);

                // List 컬렉션 노드 — Phase K 다형성. 단일 등록 (ItemType 은 인스턴스 속성).
                EnsureOne(ListAddNode.TypeIdConst, ListAddNode.CreateMetadata);
                EnsureOne(ListGetNode.TypeIdConst, ListGetNode.CreateMetadata);
                EnsureOne(ListCountNode.TypeIdConst, ListCountNode.CreateMetadata);
                EnsureOne(ListClearNode.TypeIdConst, ListClearNode.CreateMetadata);
                EnsureOne(ListContainsNode.TypeIdConst, ListContainsNode.CreateMetadata);
                EnsureOne(ListRemoveAtNode.TypeIdConst, ListRemoveAtNode.CreateMetadata);
            }
        }

        private static void EnsureOne(string typeId, System.Func<NodeMetadata> factory)
        {
            if (NodeMetadataRegistry.Get(typeId) != null) return;
            NodeMetadataRegistry.Register(factory());
        }


        /// <summary>
        /// 호스트가 임의 키/값 타입 쌍에 대해 Dictionary&lt;K,V&gt; 조작 노드 7종 등록 (멱등).
        /// Set / Get / ContainsKey / Remove / Keys / Values / Count.
        /// </summary>
        public static void EnsureMapNodesRegistered<TKey, TValue>()
        {
            EnsureOne(MapSetNode<TKey, TValue>.TypeIdConst, MapSetNode<TKey, TValue>.CreateMetadata);
            EnsureOne(MapGetNode<TKey, TValue>.TypeIdConst, MapGetNode<TKey, TValue>.CreateMetadata);
            EnsureOne(MapContainsKeyNode<TKey, TValue>.TypeIdConst, MapContainsKeyNode<TKey, TValue>.CreateMetadata);
            EnsureOne(MapRemoveNode<TKey, TValue>.TypeIdConst, MapRemoveNode<TKey, TValue>.CreateMetadata);
            EnsureOne(MapKeysNode<TKey, TValue>.TypeIdConst, MapKeysNode<TKey, TValue>.CreateMetadata);
            EnsureOne(MapValuesNode<TKey, TValue>.TypeIdConst, MapValuesNode<TKey, TValue>.CreateMetadata);
            EnsureOne(MapCountNode<TKey, TValue>.TypeIdConst, MapCountNode<TKey, TValue>.CreateMetadata);
        }
    }
}
