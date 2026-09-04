using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거 발사 창구이자 위상 규약의 주인. 그래프 하나 + 슬롯 해석기 하나에 대해
    /// 하나 만든다(플레이어당 하나).
    /// </summary>
    public sealed class MotionRuntime : ITriggerSink
    {
        public const string StartTrigger = "Start";
        public const string LoopTrigger = "Loop";
        public const string EndTrigger = "End";

        private readonly IMotionGraphView _graph;
        private readonly ISlotResolver _resolver;
        private readonly OnceLogger _log;
        private readonly Dictionary<string, TriggerRunner> _runners = new Dictionary<string, TriggerRunner>();
        private readonly List<TriggerRunner> _order = new List<TriggerRunner>();
        private readonly Dictionary<string, List<Action>> _waiters = new Dictionary<string, List<Action>>();
        private readonly object _host;

        public MotionRuntime(IMotionGraphView graph, ISlotResolver resolver, IMotionLog log)
            : this(graph, resolver, log, null)
        {
        }

        /// <param name="host">
        /// 효과 노드가 캐스트해서 쓰는 호스트. Unity 계층은 여기에 <c>MotionPlayer</c>를 넣는다.
        /// </param>
        public MotionRuntime(IMotionGraphView graph, ISlotResolver resolver, IMotionLog log, object host)
        {
            _graph = graph;
            _resolver = resolver;
            _log = new OnceLogger(log);
            _host = host;

            BuildRunners();
        }

        /// <summary><see cref="StartTrigger"/>가 자연 완료하면 <see cref="LoopTrigger"/>를 자동 발사할지.</summary>
        public bool AutoLoopAfterStart { get; set; } = true;

        public void Fire(string trigger)
        {
            TriggerRunner runner;
            if (!_runners.TryGetValue(trigger, out runner))
            {
                _log.WarnOnce("trigger:" + trigger,
                    "graph '" + _graph.GraphName + "' has no trigger '" + trigger + "'");
                ReleaseWaiters(trigger);
                return;
            }

            // End는 유지 연출을 끊고 들어간다. 닫히는 중에 계속 떠다니면 안 된다.
            if (trigger == EndTrigger)
            {
                Stop(LoopTrigger);
            }

            runner.Fire();
        }

        public void Stop(string trigger)
        {
            TriggerRunner runner;
            if (_runners.TryGetValue(trigger, out runner))
            {
                runner.Stop();
                ReleaseWaiters(trigger);
            }
        }

        /// <summary>
        /// 모든 트리거를 멈춘다.
        ///
        /// <b>선언의 역순으로 멈춘다.</b> 두 트리거가 같은 대상을 만지면 되돌리는 순서가
        /// 결과를 정하기 때문이다 — <c>Start</c>가 원래 위치 O를 기억한 채 도는 중에
        /// <c>Loop</c>가 시작하면 <c>Loop</c>는 그 시점의 중간 위치 X를 기억한다.
        /// 선언 순서대로 되돌리면 O를 복원한 뒤 X로 덮어써 오브젝트가 엉뚱한 자리에 남는다.
        ///
        /// 나중에 선언된 것이 대개 나중에 시작하므로, 역순이 스코프 안의
        /// "등록의 역순" 규칙과 결이 맞는다.
        /// </summary>
        public void StopAll()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                _order[i].Stop();
                ReleaseWaiters(_order[i].Name);
            }
        }

        public bool IsPlaying(string trigger)
        {
            TriggerRunner runner;
            return _runners.TryGetValue(trigger, out runner) && runner.IsPlaying;
        }

        /// <summary>
        /// 트리거가 끝나면 <paramref name="onCompleted"/>를 부른다. 자연 완료든 취소든 부른다 —
        /// 대기자를 영영 붙잡아 두면 팝업이 닫히지 않는다.
        ///
        /// 지금 재생 중이 아니면 <b>즉시</b> 부른다.
        /// </summary>
        public void WaitFor(string trigger, Action onCompleted)
        {
            if (onCompleted == null)
            {
                return;
            }

            if (!IsPlaying(trigger))
            {
                onCompleted();
                return;
            }

            List<Action> list;
            if (!_waiters.TryGetValue(trigger, out list))
            {
                list = new List<Action>();
                _waiters[trigger] = list;
            }

            list.Add(onCompleted);
        }

        public void Tick(float deltaSeconds)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].Tick(deltaSeconds);
            }
        }

        /// <summary>경고 억제 기록을 비운다. 플레이어가 다시 활성화될 때 부른다.</summary>
        public void ResetDiagnostics()
        {
            _log.Reset();
        }

        private void BuildRunners()
        {
            IReadOnlyList<TriggerDeclaration> triggers = _graph.Triggers;
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];

                // 같은 이름을 두 번 선언한 그래프가 와도 죽지 않는다. 먼저 것이 이긴다.
                if (decl == null || string.IsNullOrEmpty(decl.Name) || _runners.ContainsKey(decl.Name))
                {
                    continue;
                }

                string name = decl.Name;
                NodeId entry = decl.Entry;

                var runner = new TriggerRunner(name, decl.Policy, delegate { return CreateScope(name, entry); });
                runner.CompletedNaturally += delegate { OnRunnerCompleted(name); };

                _runners[name] = runner;
                _order.Add(runner);
            }
        }

        private MotionScope CreateScope(string triggerName, NodeId entry)
        {
            var scope = new MotionScope(triggerName, _log);
            scope.Begin(new MotionContext(_graph, scope, _resolver, _log, this, 0, _host), entry);
            return scope;
        }

        private void OnRunnerCompleted(string triggerName)
        {
            ReleaseWaiters(triggerName);

            if (triggerName != StartTrigger || !AutoLoopAfterStart)
            {
                return;
            }

            // 그래프에 Loop가 없으면 조용히 넘어간다 — 없는 트리거를 발사해 경고를 내면 안 된다.
            if (_runners.ContainsKey(LoopTrigger))
            {
                Fire(LoopTrigger);
            }
        }

        private void ReleaseWaiters(string triggerName)
        {
            List<Action> list;
            if (!_waiters.TryGetValue(triggerName, out list))
            {
                return;
            }

            _waiters.Remove(triggerName);

            for (int i = 0; i < list.Count; i++)
            {
                list[i]();
            }
        }
    }
}
