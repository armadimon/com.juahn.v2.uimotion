using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// 원상 복구가 같은 트리거를 다시 발사하는 경우.
    ///
    /// 억지 상황처럼 보이지만 실제 경로가 있다. 자기 자신을 끄는 SetActive 노드가
    /// <c>SetActive(false)</c>를 부르면 Unity가 <b>동기적으로</b> OnDisable을 부르고,
    /// 그것이 StopAll -> 취소 -> 복구 <c>SetActive(true)</c> -> 동기 OnEnable ->
    /// <c>Fire("Start")</c>로 이어진다. 전부 하나의 호출 스택 안에서 일어난다.
    /// </summary>
    [TestFixture]
    public sealed class TriggerReentrancyTests
    {
        /// <summary>취소될 때 자기 트리거를 다시 발사하는 노드.</summary>
        private sealed class RefiringNode : MotionEffectNode
        {
            public string Trigger = "Start";
            public int MaxRefires = 1;

            public int Starts;
            public int Refires;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                Starts++;

                ITriggerSink sink = ctx.Triggers;
                ctx.Scope.Remember(delegate
                {
                    if (sink == null || Refires >= MaxRefires)
                    {
                        return;
                    }

                    Refires++;
                    sink.Fire(Trigger);
                });

                // 한 프레임에 끝나지 않아야 취소 경로를 탈 수 있다.
                return MotionHandle.FromTimer(10f, null);
            }
        }

        private static MotionRuntime Build(RefiringNode node, TriggerPolicy policy)
        {
            var graph = new FakeGraph();
            NodeId id = graph.Add(node);
            graph.DeclareTrigger("Start", id, policy);

            var runtime = new MotionRuntime(graph, null, null);
            runtime.AutoLoopAfterStart = false;
            return runtime;
        }

        [Test]
        public void Stop_WhenRevertRefiresSameTrigger_KeepsTheNewScope()
        {
            var node = new RefiringNode();
            MotionRuntime runtime = Build(node, TriggerPolicy.Restart);

            runtime.Fire("Start");
            runtime.Tick(0.1f);
            Assert.That(runtime.IsPlaying("Start"), Is.True, "사전 조건");

            runtime.Stop("Start");

            Assert.That(node.Refires, Is.EqualTo(1), "복구가 재발사했어야 한다");
            Assert.That(runtime.IsPlaying("Start"), Is.True,
                "재발사가 만든 스코프를 취소도 없이 버리면 오브젝트가 아무 연출 없이 굳는다");

            // NodeRun은 지연 시작이라 OnPlay는 다음 Tick에서야 돈다.
            runtime.Tick(0.1f);
            Assert.That(node.Starts, Is.EqualTo(2), "새 스코프가 실제로 노드를 재생해야 한다");
        }

        [Test]
        public void Restart_WhenRevertRefiresSameTrigger_DoesNotCreateAnExtraScope()
        {
            // 이 경로는 오늘은 무해하다 — Begin이 지연 시작이라 버려지는 스코프가
            // 아무 자원도 붙잡지 않기 때문이다. 그래도 막아 두는 이유는 두 가지다.
            // 취소도 없이 버려지는 스코프를 만드는 것 자체가 낭비이고, Begin이 언제든
            // 즉시 시작으로 바뀌면 그 순간 진짜 버그가 된다.
            //
            // 관측 가능한 것은 "스코프를 몇 개 만들었나"뿐이라 러너를 직접 쓴다.
            int created = 0;
            TriggerRunner runner = null;

            runner = new TriggerRunner("Start", TriggerPolicy.Restart, delegate
            {
                created++;
                var scope = new MotionScope("Start", null);

                // 첫 스코프만 취소될 때 같은 트리거를 다시 발사한다.
                if (created == 1)
                {
                    scope.Remember(delegate { runner.Fire(); });
                }

                // Begin을 부르지 않으면 IsDone이 false라 재생 중으로 잡힌다.
                return scope;
            });

            runner.Fire();
            Assert.That(created, Is.EqualTo(1), "사전 조건");

            runner.Fire();

            Assert.That(created, Is.EqualTo(2),
                "재진입 발사가 이미 새 스코프를 세웠는데 또 만들면 하나가 취소도 없이 버려진다");
            Assert.That(runner.IsPlaying, Is.True);
        }

        [Test]
        public void Stop_WithoutRefire_StillClearsTheScope()
        {
            // 위 가드가 정상 경로를 망가뜨리지 않는지 확인한다.
            var node = new RefiringNode { MaxRefires = 0 };
            MotionRuntime runtime = Build(node, TriggerPolicy.Restart);

            runtime.Fire("Start");
            runtime.Tick(0.1f);
            runtime.Stop("Start");

            Assert.That(runtime.IsPlaying("Start"), Is.False);
            Assert.That(node.Starts, Is.EqualTo(1));
        }

        [Test]
        public void StopAll_RevertsLaterTriggersFirst()
        {
            // 두 트리거가 같은 대상을 만지면 되돌리는 순서가 결과를 정한다.
            // Start가 위치 O를 기억한 채 도는 중에 Loop가 시작하면 Loop는 그 시점의
            // 중간 위치 X를 기억한다. 선언 순서대로 되돌리면 O를 복원한 뒤 X를 덮어써
            // 오브젝트가 엉뚱한 자리에 남는다. 나중에 시작한 것부터 되돌려야 한다.
            var log = new ExecutionLog();

            var graph = new FakeGraph();
            NodeId first = graph.Add(new RecordingEffect("start", 10f) { Log = log, RegisterRevert = true });
            NodeId second = graph.Add(new RecordingEffect("loop", 10f) { Log = log, RegisterRevert = true });
            graph.DeclareTrigger("Start", first);
            graph.DeclareTrigger("Loop", second);

            var runtime = new MotionRuntime(graph, null, null);
            runtime.AutoLoopAfterStart = false;

            runtime.Fire("Start");
            runtime.Tick(0.1f);
            runtime.Fire("Loop");
            runtime.Tick(0.1f);

            runtime.StopAll();

            int loopRevert = IndexOf(log, "loop:revert");
            int startRevert = IndexOf(log, "start:revert");

            Assert.That(loopRevert, Is.GreaterThanOrEqualTo(0), "Loop가 되돌려져야 한다");
            Assert.That(startRevert, Is.GreaterThanOrEqualTo(0), "Start가 되돌려져야 한다");
            Assert.That(loopRevert, Is.LessThan(startRevert),
                "나중에 선언된 트리거가 먼저 되돌아와야 앞의 복원이 마지막에 남는다");
        }

        private static int IndexOf(ExecutionLog log, string entry)
        {
            for (int i = 0; i < log.Entries.Count; i++)
            {
                if (log.Entries[i] == entry)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
