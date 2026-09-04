using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionRuntimeTests
    {
        private FakeGraph _graph;
        private ExecutionLog _log;
        private FakeLog _sink;

        [SetUp]
        public void SetUp()
        {
            _graph = new FakeGraph();
            _log = new ExecutionLog();
            _sink = new FakeLog();
        }

        private MotionRuntime Make()
        {
            return new MotionRuntime(_graph, new FakeSlotResolver(), _sink);
        }

        private NodeId DeclareTrigger(string name, string effectName, float duration = 0f,
            TriggerPolicy policy = TriggerPolicy.Restart)
        {
            NodeId entry = _graph.Add(new RecordingEffect(effectName, duration) { Log = _log });
            _graph.DeclareTrigger(name, entry, policy);
            return entry;
        }

        [Test]
        public void Fire_RunsDeclaredTrigger()
        {
            DeclareTrigger("Click", "click");
            MotionRuntime runtime = Make();

            runtime.Fire("Click");
            runtime.Tick(0f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "click:start", "click:end" }));
        }

        [Test]
        public void Fire_UnknownTrigger_WarnsOnceAndIgnores()
        {
            MotionRuntime runtime = Make();

            runtime.Fire("Nope");
            runtime.Fire("Nope");
            runtime.Tick(0f);

            Assert.That(_sink.Warnings.Count, Is.EqualTo(1), "같은 오타를 매번 경고하지 않는다");
        }

        [Test]
        public void IsPlaying_UnknownTrigger_IsFalse()
        {
            MotionRuntime runtime = Make();
            Assert.That(runtime.IsPlaying("Nope"), Is.False);
        }

        [Test]
        public void StartCompletion_AutoFiresLoop()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start");
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.StartTrigger);
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);
        }

        [Test]
        public void StartCompletion_DoesNotFireLoop_WhenAutoLoopDisabled()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start");
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            MotionRuntime runtime = Make();
            runtime.AutoLoopAfterStart = false;

            runtime.Fire(MotionRuntime.StartTrigger);
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
        }

        [Test]
        public void StartCompletion_DoesNothing_WhenGraphHasNoLoop()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start");
            MotionRuntime runtime = Make();

            Assert.DoesNotThrow(delegate
            {
                runtime.Fire(MotionRuntime.StartTrigger);
                runtime.Tick(0f);
            });
            Assert.That(_sink.Warnings.Count, Is.EqualTo(0), "없는 Loop를 자동 발사해 경고를 내면 안 된다");
        }

        [Test]
        public void StartCancelled_DoesNotFireLoop()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start", 1f);
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.StartTrigger);
            runtime.Tick(0.5f);
            runtime.Stop(MotionRuntime.StartTrigger);
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False,
                "닫히는 중에 유지 연출이 다시 시작되면 안 된다");
        }

        [Test]
        public void FireEnd_StopsLoop()
        {
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            DeclareTrigger(MotionRuntime.EndTrigger, "end", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.LoopTrigger);
            runtime.Tick(0.5f);
            runtime.Fire(MotionRuntime.EndTrigger);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
            Assert.That(runtime.IsPlaying(MotionRuntime.EndTrigger), Is.True);
        }

        [Test]
        public void FireEnd_WithoutLoop_DoesNotWarn()
        {
            DeclareTrigger(MotionRuntime.EndTrigger, "end", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.EndTrigger);

            Assert.That(_sink.Warnings.Count, Is.EqualTo(0), "없는 Loop를 멈추려다 경고를 내면 안 된다");
        }

        [Test]
        public void StopAll_StopsEverything()
        {
            DeclareTrigger("A", "a", 1f);
            DeclareTrigger("B", "b", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire("A");
            runtime.Fire("B");
            runtime.Tick(0.1f);
            runtime.StopAll();

            Assert.That(runtime.IsPlaying("A"), Is.False);
            Assert.That(runtime.IsPlaying("B"), Is.False);
        }

        [Test]
        public void TriggersRunIndependently()
        {
            DeclareTrigger("A", "a", 1f);
            DeclareTrigger("B", "b", 2f);
            MotionRuntime runtime = Make();

            runtime.Fire("A");
            runtime.Fire("B");
            runtime.Tick(1f);

            Assert.That(runtime.IsPlaying("A"), Is.False);
            Assert.That(runtime.IsPlaying("B"), Is.True);
        }

        [Test]
        public void WaitFor_InvokesOnNaturalCompletion()
        {
            DeclareTrigger("A", "a", 1f);
            MotionRuntime runtime = Make();

            int called = 0;
            runtime.Fire("A");
            runtime.Tick(0f);
            runtime.WaitFor("A", delegate { called++; });

            runtime.Tick(0.5f);
            Assert.That(called, Is.EqualTo(0));

            runtime.Tick(0.5f);
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void WaitFor_InvokesImmediately_WhenNotPlaying()
        {
            // 브릿지가 Fire("End") 뒤에 WaitFor를 부른다. End 트리거가 없는 그래프에서도
            // 콜백이 와야 프리젠터가 영영 닫히지 않는 일이 없다.
            MotionRuntime runtime = Make();

            int called = 0;
            runtime.WaitFor("Missing", delegate { called++; });

            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void WaitFor_InvokesOnCancel()
        {
            DeclareTrigger("A", "a", 1f);
            MotionRuntime runtime = Make();

            int called = 0;
            runtime.Fire("A");
            runtime.Tick(0f);
            runtime.WaitFor("A", delegate { called++; });
            runtime.Stop("A");

            Assert.That(called, Is.EqualTo(1), "취소로 끝나도 대기자를 풀어 줘야 한다");
        }

        [Test]
        public void WaitFor_NullCallback_IsIgnored()
        {
            MotionRuntime runtime = Make();
            Assert.DoesNotThrow(delegate { runtime.WaitFor("Nope", null); });
        }

        [Test]
        public void WaitFor_MultipleWaiters_AllInvokedOnce()
        {
            DeclareTrigger("A", "a", 1f);
            MotionRuntime runtime = Make();

            int a = 0;
            int b = 0;
            runtime.Fire("A");
            runtime.Tick(0f);
            runtime.WaitFor("A", delegate { a++; });
            runtime.WaitFor("A", delegate { b++; });

            runtime.Tick(1f);
            runtime.Tick(1f);

            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.EqualTo(1));
        }

        [Test]
        public void StopTriggerNode_StopsAnotherTrigger()
        {
            NodeId loopEntry = _graph.Add(new RecordingEffect("loop", 5f) { Log = _log });
            _graph.DeclareTrigger(MotionRuntime.LoopTrigger, loopEntry);

            NodeId stopEntry = _graph.Add(new StopTriggerNode { TriggerName = MotionRuntime.LoopTrigger });
            _graph.DeclareTrigger("Kill", stopEntry);

            MotionRuntime runtime = Make();
            runtime.Fire(MotionRuntime.LoopTrigger);
            runtime.Tick(0.1f);
            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);

            runtime.Fire("Kill");
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
        }

        [Test]
        public void TriggerNode_JustPropagatesToChildren()
        {
            NodeId entry = _graph.Add(new TriggerNode { TriggerName = "Click" });
            _graph.Link(entry, _graph.Add(new RecordingEffect("fx") { Log = _log }));
            _graph.DeclareTrigger("Click", entry);

            MotionRuntime runtime = Make();
            runtime.Fire("Click");
            runtime.Tick(0f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:start", "fx:end" }));
        }

        [Test]
        public void ResetDiagnostics_AllowsWarningAgain()
        {
            MotionRuntime runtime = Make();

            runtime.Fire("Nope");
            runtime.ResetDiagnostics();
            runtime.Fire("Nope");

            Assert.That(_sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateTriggerNames_FirstOneWins()
        {
            // 그래프가 같은 이름을 두 번 선언해도 실행기가 죽으면 안 된다.
            NodeId first = _graph.Add(new RecordingEffect("first") { Log = _log });
            NodeId second = _graph.Add(new RecordingEffect("second") { Log = _log });
            _graph.DeclareTrigger("Dup", first);
            _graph.DeclareTrigger("Dup", second);

            MotionRuntime runtime = Make();
            runtime.Fire("Dup");
            runtime.Tick(0f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "first:start", "first:end" }));
        }
    }
}
