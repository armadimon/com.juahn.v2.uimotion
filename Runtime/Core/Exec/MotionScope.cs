using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거 1회 발사가 만드는 실행 단위. 그 발사가 시작한 모든 것을 소유하고,
    /// 취소되면 전부 정리한다.
    ///
    /// 그래프가 아니라 <b>스코프가</b> 실행 상태를 갖는다. 같은 그래프 에셋을 수백 개
    /// 오브젝트가 동시에 쓰는 것이 정상적인 사용법이기 때문이다.
    /// </summary>
    public sealed class MotionScope : IMotionScope
    {
        private readonly List<Action> _reverts = new List<Action>();
        private readonly IMotionLog _log;

        private bool _done;
        private bool _cancelled;
        private NodeRun _root;

        public MotionScope(string triggerName, IMotionLog log = null)
        {
            TriggerName = triggerName;
            _log = log;
        }

        /// <summary>이 스코프를 만든 트리거의 이름. 진단용.</summary>
        public string TriggerName { get; }

        /// <summary>끝났는가. 자연 완료와 취소를 모두 포함한다.</summary>
        public bool IsDone => _done;

        /// <summary>중간에 끊겼는가.</summary>
        public bool IsCancelled => _cancelled;

        /// <summary>끝났을 때 한 번 발생한다. 자연 완료든 취소든 한 번만.</summary>
        public event Action Completed;

        /// <summary>
        /// 취소될 때 실행할 복구 동작을 등록한다. 등록의 역순으로 실행된다 —
        /// 나중에 등록된 변경이 앞의 것 위에 쌓여 있기 때문이다.
        ///
        /// 이미 취소된 스코프에 등록하면 <b>그 자리에서 즉시</b> 실행한다.
        /// 안 그러면 늦게 도착한 등록을 아무도 되돌리지 않아 값이 남는다.
        /// </summary>
        public void Remember(Action revert)
        {
            if (revert == null)
            {
                return;
            }

            if (_done)
            {
                // 취소된 스코프에 늦게 도착한 등록은 그 자리에서 되돌린다 —
                // 안 그러면 아무도 되돌리지 않아 값이 남는다.
                // 자연 완료된 스코프는 애초에 되돌리지 않으므로 그냥 버린다.
                // 쌓아 두면 캡처된 참조까지 영원히 남는다.
                if (_cancelled)
                {
                    RunRevert(revert);
                }

                return;
            }

            _reverts.Add(revert);
        }

        /// <summary>
        /// 진입 노드부터 실행을 시작한다. 진입이 <see cref="NodeId.None"/>이면
        /// 할 일이 없는 것이므로 즉시 완료한다.
        ///
        /// 이미 시작했거나 끝난 스코프에 다시 부르면 무시한다 — 트리가 두 개 생기면
        /// 취소가 한쪽만 끊게 된다.
        /// </summary>
        public void Begin(IMotionContext ctx, NodeId entry)
        {
            if (_done || _root != null)
            {
                return;
            }

            if (!entry.IsValid)
            {
                CompleteNaturally();
                return;
            }

            _root = new NodeRun(ctx, entry);
        }

        /// <summary>시간을 진행시킨다. 실행 트리가 전부 끝나면 자연 완료로 표시한다.</summary>
        public void Tick(float deltaSeconds)
        {
            if (_done || _root == null)
            {
                return;
            }

            _root.Tick(deltaSeconds);

            if (_root.IsDone)
            {
                CompleteNaturally();
            }
        }

        /// <summary>즉시 끊고 등록된 복구를 역순으로 전부 실행한다. 두 번 불러도 한 번만 동작한다.</summary>
        public void Cancel()
        {
            if (_done)
            {
                return;
            }

            _cancelled = true;
            _done = true;

            if (_root != null)
            {
                _root.Cancel();
            }

            for (int i = _reverts.Count - 1; i >= 0; i--)
            {
                RunRevert(_reverts[i]);
            }

            _reverts.Clear();

            RaiseCompleted();
        }

        /// <summary>
        /// 자연 완료로 표시한다. <b>복구를 실행하지 않는다</b> —
        /// 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다.
        /// </summary>
        internal void CompleteNaturally()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            _reverts.Clear();

            RaiseCompleted();
        }

        private void RaiseCompleted()
        {
            Action handler = Completed;
            if (handler != null)
            {
                handler();
            }
        }

        private void RunRevert(Action revert)
        {
            // 대상 하나가 이미 파괴돼 예외가 나도 나머지는 전부 되돌려야 한다.
            try
            {
                revert();
            }
            catch (Exception e)
            {
                if (_log != null)
                {
                    _log.Error("revert failed in scope '" + TriggerName + "': " + e.Message);
                }
            }
        }
    }
}
