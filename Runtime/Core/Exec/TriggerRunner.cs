using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거 하나의 재생 상태. 한 번에 스코프 하나만 들고, 재발사를 정책대로 처리한다.
    ///
    /// 스코프를 만드는 일은 바깥에서 넘겨준 팩토리가 한다 —
    /// 그래서 이 클래스는 그래프도 슬롯도 모른다.
    /// </summary>
    public sealed class TriggerRunner
    {
        private readonly Func<MotionScope> _scopeFactory;
        private readonly TriggerPolicy _policy;

        private MotionScope _current;
        private bool _queued;

        public TriggerRunner(string name, TriggerPolicy policy, Func<MotionScope> scopeFactory)
        {
            Name = name;
            _policy = policy;
            _scopeFactory = scopeFactory;
        }

        public string Name { get; }

        public bool IsPlaying => _current != null && !_current.IsDone;

        /// <summary>자연 완료했을 때만 발생한다. 취소로 끝난 경우에는 발생하지 않는다.</summary>
        public event Action CompletedNaturally;

        public void Fire()
        {
            if (!IsPlaying)
            {
                StartNew();
                return;
            }

            switch (_policy)
            {
                case TriggerPolicy.Ignore:
                    return;

                case TriggerPolicy.Queue:
                    // 대기는 하나만 유지한다. 무한히 쌓이면 연타 한 번에 연출이 수십 번 돈다.
                    _queued = true;
                    return;

                default:
                {
                    // Tick과 같은 이유로 필드가 아니라 지역 변수로 잡는다 — 아래 참조.
                    MotionScope restarting = _current;
                    restarting.Cancel();

                    if (!ReferenceEquals(_current, restarting))
                    {
                        // 취소가 돌린 복구가 이미 이 트리거를 다시 발사했다.
                        // 그것이 곧 재시작이므로 여기서 또 시작하면 두 번 돈다.
                        return;
                    }

                    _current = null;
                    StartNew();
                    return;
                }
            }
        }

        public void Stop()
        {
            _queued = false;

            // Tick과 같은 이유로 필드가 아니라 지역 변수로 잡는다.
            //
            // Cancel은 등록된 원상 복구를 이 호출 스택 안에서 전부 돌리는데, 그 복구가
            // 같은 트리거를 다시 발사할 수 있다. 실제 경로가 있다 — 자기 자신을 끄는
            // SetActive 노드가 취소되면 복구가 오브젝트를 다시 켜고, Unity가 그 자리에서
            // 동기적으로 OnEnable을 불러 Start가 재발사된다.
            //
            // 그때 아래에서 _current를 무조건 null로 만들면 방금 만들어진 스코프가
            // 취소도 되지 않은 채 사라진다. 오브젝트는 아무 연출 없이 굳고, 오류는
            // 하나도 나오지 않는다.
            MotionScope current = _current;
            if (current == null)
            {
                return;
            }

            current.Cancel();

            if (ReferenceEquals(_current, current))
            {
                _current = null;
            }
        }

        public void Tick(float deltaSeconds)
        {
            // 필드가 아니라 지역 변수로 잡는다. _current.Tick 안에서 노드가
            // 같은 트리거에 Stop()이나 Fire()를 부르면 필드가 바뀌기 때문이다
            // (자기 자신을 멈추는 StopTriggerNode 등). NodeRun이 지연 시작이라
            // 그 OnPlay가 바로 이 호출 스택 안에서 실행된다.
            MotionScope current = _current;
            if (current == null)
            {
                return;
            }

            current.Tick(deltaSeconds);

            // 재진입한 Stop()/Fire()가 이미 정리를 끝냈다면 여기서 손대지 않는다.
            if (!ReferenceEquals(_current, current))
            {
                return;
            }

            if (!current.IsDone)
            {
                return;
            }

            bool wasCancelled = current.IsCancelled;
            _current = null;

            if (!wasCancelled)
            {
                RaiseCompleted();
            }

            if (_queued)
            {
                _queued = false;
                StartNew();
            }
        }

        private void StartNew()
        {
            _current = _scopeFactory();

            // 팩토리가 진입 노드 없이 만든 스코프는 이미 끝나 있다.
            // 붙들고 있으면 IsPlaying이 영원히 false인 채로 남아 다음 Tick이 무의미해진다.
            if (_current != null && _current.IsDone)
            {
                bool wasCancelled = _current.IsCancelled;
                _current = null;

                if (!wasCancelled)
                {
                    RaiseCompleted();
                }
            }
        }

        private void RaiseCompleted()
        {
            Action handler = CompletedNaturally;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
