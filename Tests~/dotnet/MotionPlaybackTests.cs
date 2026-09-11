using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    public sealed class MotionPlaybackTests
    {
        private sealed class ReadParameter : MotionEffectNode
        {
            public readonly List<float> Values = new List<float>();
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            { Values.Add(((MotionScope)ctx.Scope).Parameter("Target")); return MotionHandle.Completed; }
        }
        private sealed class Throwing : MotionEffectNode
        {
            public int RevertCount;
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            { ctx.Scope.Remember(() => RevertCount++); throw new InvalidOperationException("node failure"); }
        }
        private sealed class TextSlot : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target = new SlotRef("Visual");
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }
        private sealed class NumberSlot : MotionEffectNode
        {
            [MotionSlot(typeof(int))] public SlotRef Target = new SlotRef("Visual");
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }
        private static MotionRuntime Runtime(MotionNodeBase node, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            var graph = new FakeGraph(); graph.DeclareTrigger("Start", graph.Add(node), policy);
            return new MotionRuntime(graph, null, null) { AutoLoopAfterStart = false };
        }
        [Test] public void RestartSeparatesCanceledAndCompletedRunIds()
        {
            var runtime = Runtime(new RecordingEffect("effect", 1f));
            var first = runtime.Play("Start"); runtime.Tick(.2f);
            var second = runtime.Play("Start"); var callbacks = 0;
            first.WhenCompleted(result => { callbacks++; Assert.That(result.RunId, Is.EqualTo(first.RunId)); });
            Assert.That(first.Result.Outcome, Is.EqualTo(MotionOutcome.Canceled));
            Assert.That(second.IsDone, Is.False); Assert.That(second.RunId, Is.Not.EqualTo(first.RunId));
            runtime.Tick(1f); runtime.StopAll();
            Assert.That(second.Result.Outcome, Is.EqualTo(MotionOutcome.Completed)); Assert.That(callbacks, Is.EqualTo(1));
        }
        [Test] public void IgnoreSkipsOnlyTheNewRequest()
        {
            var runtime = Runtime(new RecordingEffect("effect", 1f), TriggerPolicy.Ignore);
            var first = runtime.Play("Start"); var ignored = runtime.Play("Start");
            Assert.That(ignored.Result.Outcome, Is.EqualTo(MotionOutcome.Skipped)); Assert.That(first.IsDone, Is.False);
            runtime.StopAll(); Assert.That(first.Result.Outcome, Is.EqualTo(MotionOutcome.Canceled));
        }
        [Test] public void QueueKeepsOnlyLatestPendingRequestAndStopCancelsIt()
        {
            var runtime = Runtime(new RecordingEffect("effect", 1f), TriggerPolicy.Queue);
            var first = runtime.Play("Start"); var second = runtime.Play("Start"); var third = runtime.Play("Start");
            Assert.That(second.Result.Outcome, Is.EqualTo(MotionOutcome.Canceled));
            runtime.Tick(1f); Assert.That(first.Result.Outcome, Is.EqualTo(MotionOutcome.Completed));
            Assert.That(third.IsDone, Is.False); runtime.StopAll();
            Assert.That(third.Result.Outcome, Is.EqualTo(MotionOutcome.Canceled));
        }
        [Test] public void FailureRestoresStateAndReleasesLegacyWaiters()
        {
            var node = new Throwing(); var runtime = Runtime(node); var completed = false;
            var run = runtime.Play("Start"); runtime.WaitFor("Start", () => completed = true); runtime.Tick(.1f);
            Assert.That(run.Result.Outcome, Is.EqualTo(MotionOutcome.Failed)); Assert.That(run.Result.Error, Is.TypeOf<InvalidOperationException>());
            Assert.That(node.RevertCount, Is.EqualTo(1)); Assert.That(completed, Is.True);
        }
        [Test] public void SharedSubgraphUsesIndependentParameterSnapshots()
        {
            var read = new ReadParameter(); var inner = new FakeGraph(); inner.DeclareTrigger("Start", inner.Add(read));
            var outer = new FakeGraph(); outer.DeclareTrigger("Start", outer.Add(new FakeSubGraphNode { Target = inner }));
            var a = new MotionRuntime(outer, null, null); var b = new MotionRuntime(outer, null, null);
            var parameters = new Dictionary<string, float> { ["Target"] = 10 };
            a.Play("Start", parameters); parameters["Target"] = 20; b.Play("Start", parameters); parameters["Target"] = 999;
            a.Tick(.1f); b.Tick(.1f); Assert.That(read.Values, Is.EqualTo(new[] { 10f, 20f }));
        }
        [Test] public void RecursiveSlotsDetectMissingReferencesCyclesAndConflictingTypes()
        {
            var inner = new FakeGraph(); inner.DeclareTrigger("Start", inner.Add(new TextSlot()));
            var outer = new FakeGraph(); outer.DeclareTrigger("Start", outer.Add(new FakeSubGraphNode { Target = inner }));
            Assert.That(GraphSlotCollector.Collect(outer).Single().RequiredType, Is.EqualTo(typeof(string)));
            outer.Add(new NumberSlot()); inner.Add(new FakeSubGraphNode { Target = outer }); inner.Add(new FakeSubGraphNode());
            var issues = new List<MotionGraphIssue>(); GraphSlotCollector.Collect(outer, issues);
            Assert.That(issues.Count, Is.EqualTo(3)); Assert.That(MotionGraphValidator.HasErrors(issues), Is.True);
        }
        [Test] public void InfiniteEndInNestedGraphIsRejected()
        {
            var inner = new FakeGraph(); inner.DeclareTrigger("Start", inner.Add(new RepeatNode { Count = -1 }));
            var outer = new FakeGraph(); outer.DeclareTrigger("End", outer.Add(new FakeSubGraphNode { Target = inner }));
            Assert.That(MotionGraphValidator.IsFinite(outer, "End"), Is.False);
        }
    }
}
