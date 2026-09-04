namespace Juahn.UiMotion
{
    /// <summary>
    /// <see cref="IMotionContext"/>의 기본 구현. 스코프 하나당 하나 만들어 그 스코프의
    /// 모든 노드가 공유한다.
    /// </summary>
    public sealed class MotionContext : IMotionContext
    {
        private readonly ISlotResolver _resolver;

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log)
            : this(graph, scope, resolver, log, null)
        {
        }

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers)
            : this(graph, scope, resolver, log, triggers, 0)
        {
        }

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers, int depth)
        {
            Graph = graph;
            Scope = scope;
            _resolver = resolver;
            Log = log;
            Triggers = triggers;
            Depth = depth;
        }

        public IMotionGraphView Graph { get; }

        public IMotionScope Scope { get; }

        public IMotionLog Log { get; }

        public ITriggerSink Triggers { get; }

        public int Depth { get; }

        public object ResolveSlot(SlotRef slot)
        {
            return _resolver == null ? null : _resolver.Resolve(slot);
        }
    }
}
