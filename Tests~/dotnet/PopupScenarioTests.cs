using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// 설계 문서의 예시 팝업 연출을 그대로 조립해 돌린다.
    ///
    ///   Start: [Punch 0.2s, Fade 0.15s] 동시  →  완료 시 Loop 자동 발사
    ///   Loop:  Float 1s 무한 반복 (중단되면 원위치로 복구)
    ///   End:   Shrink 0.15s
    ///
    /// 단위 테스트가 전부 통과해도 조합에서만 드러나는 결함이 있다.
    /// </summary>
    [TestFixture]
    public sealed class PopupScenarioTests
    {
        private FakeGraph _graph;
        private ExecutionLog _log;
        private FakeLog _sink;
        private MotionRuntime _runtime;

        [SetUp]
        public void SetUp()
        {
            _graph = new FakeGraph { GraphName = "PopupOpen" };
            _log = new ExecutionLog();
            _sink = new FakeLog();

            NodeId startEntry = _graph.Add(new ParallelNode());
            _graph.Link(startEntry, _graph.Add(new RecordingEffect("punch", 0.2f) { Log = _log }));
            _graph.Link(startEntry, _graph.Add(new RecordingEffect("fade", 0.15f) { Log = _log }));
            _graph.DeclareTrigger(MotionRuntime.StartTrigger, startEntry);

            NodeId loopEntry = _graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            _graph.Link(loopEntry, _graph.Add(new RecordingEffect("float", 1f)
            {
                Log = _log,
                RegisterRevert = true
            }));
            _graph.DeclareTrigger(MotionRuntime.LoopTrigger, loopEntry);

            NodeId endEntry = _graph.Add(new RecordingEffect("shrink", 0.15f) { Log = _log });
            _graph.DeclareTrigger(MotionRuntime.EndTrigger, endEntry);

            _runtime = new MotionRuntime(_graph, new FakeSlotResolver(), _sink);
        }

        [Test]
        public void OpenSequence_StartsBothEffectsTogether()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.1f);

            // Parallel의 프라이밍 덕에 둘 다 시작돼 있어야 한다.
            Assert.That(_log.Entries, Does.Contain("punch:start"));
            Assert.That(_log.Entries, Does.Contain("fade:start"));
            Assert.That(_log.Entries, Does.Not.Contain("fade:end"), "0.1초에는 아직 아무것도 안 끝났다");
        }

        [Test]
        public void OpenSequence_ShorterEffectFinishesFirst()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.15f);

            Assert.That(_log.Entries, Does.Contain("fade:end"));
            Assert.That(_log.Entries, Does.Not.Contain("punch:end"), "punch는 0.2초짜리다");
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False,
                "Start가 아직 안 끝났으므로 Loop도 시작되지 않았다");
        }

        [Test]
        public void OpenSequence_AutoFiresLoop_WhenStartCompletes()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.15f);
            _runtime.Tick(0.05f);

            Assert.That(_log.Entries, Does.Contain("punch:end"));
            Assert.That(_runtime.IsPlaying(MotionRuntime.StartTrigger), Is.False);
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True,
                "Start가 끝나면 Loop가 자동으로 이어진다");
            Assert.That(_log.Entries, Does.Contain("float:start"));
        }

        [Test]
        public void LoopKeepsRunning_Indefinitely()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            for (int i = 0; i < 100; i++)
            {
                _runtime.Tick(1f);
            }

            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True,
                "무한 반복은 스스로 끝나지 않는다");
        }

        [Test]
        public void CloseSequence_StopsLoopAndRevertsIt()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);
            _runtime.Tick(0.5f);

            _runtime.Fire(MotionRuntime.EndTrigger);

            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
            Assert.That(_log.Entries, Does.Contain("float:revert"),
                "유지 연출이 끊기면 원래 위치로 되돌아가야 한다");
        }

        [Test]
        public void CloseSequence_HostCanWaitForEnd()
        {
            // 브릿지가 UiService의 CloseTransitionTask로 나르는 흐름이다.
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            bool closed = false;
            _runtime.Fire(MotionRuntime.EndTrigger);
            _runtime.Tick(0f);
            _runtime.WaitFor(MotionRuntime.EndTrigger, delegate { closed = true; });

            _runtime.Tick(0.1f);
            Assert.That(closed, Is.False, "닫힘 연출이 도는 동안은 기다린다");

            _runtime.Tick(0.05f);
            Assert.That(closed, Is.True);
            Assert.That(_log.Entries, Does.Contain("shrink:end"));
        }

        [Test]
        public void ReopenWhileClosing_RestartsCleanly()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);
            _runtime.Fire(MotionRuntime.EndTrigger);
            _runtime.Tick(0.05f);

            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True,
                "다시 열면 Loop가 되살아난다");
        }

        [Test]
        public void StopAll_LeavesNothingRunning_AndReverts()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);
            _runtime.Tick(0.5f);

            _runtime.StopAll();

            Assert.That(_runtime.IsPlaying(MotionRuntime.StartTrigger), Is.False);
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
            Assert.That(_runtime.IsPlaying(MotionRuntime.EndTrigger), Is.False);
            Assert.That(_log.Entries, Does.Contain("float:revert"),
                "전부 멈추면 유지 연출의 복구도 실행된다");
        }

        [Test]
        public void NoWarnings_OnHappyPath()
        {
            // 정상 경로에서 경고가 나오면 방치형 게임의 콘솔이 몇 시간 뒤 쓸모없어진다.
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            for (int i = 0; i < 10; i++)
            {
                _runtime.Tick(1f);
            }

            _runtime.Fire(MotionRuntime.EndTrigger);
            _runtime.Tick(0.15f);

            Assert.That(_sink.Warnings.Count, Is.EqualTo(0), "정상 경로에 경고가 없어야 한다");
            Assert.That(_sink.Errors.Count, Is.EqualTo(0));
        }

        [Test]
        public void LongRunningLoop_DoesNotAccumulateLogSpam()
        {
            // 방치형이라 몇 시간을 돈다. 로그가 무한히 쌓이면 안 된다.
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            for (int i = 0; i < 5000; i++)
            {
                _runtime.Tick(0.016f);
            }

            Assert.That(_sink.Warnings.Count, Is.EqualTo(0));
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);
        }
    }
}
