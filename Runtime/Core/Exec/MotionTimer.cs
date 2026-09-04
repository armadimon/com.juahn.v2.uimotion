using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 지속시간이 있는 진행 상태. 시간은 바깥에서 <see cref="Advance"/>로 주입한다 —
    /// 코어가 시계를 갖지 않으므로 테스트가 결정적이다.
    ///
    /// <b>구조체다.</b> 프로퍼티가 아니라 필드에 담아야 <see cref="Advance"/>의 변경이 남는다.
    ///
    /// 직렬화되지 않는 런타임 전용 타입이라 <c>readonly</c> private 필드를 쓴다 —
    /// 그래프 에셋에 저장되는 값 타입(<see cref="NodeId"/> 등)과는 규칙이 다르다.
    /// </summary>
    [Serializable]
    public struct MotionTimer
    {
        private readonly float _duration;
        private float _elapsed;

        /// <summary>
        /// 지속시간 0 이하는 "즉시"를 뜻한다 — 만들자마자 완료 상태다.
        /// </summary>
        public MotionTimer(float duration)
        {
            _duration = duration > 0f ? duration : 0f;
            _elapsed = 0f;
        }

        public float Duration => _duration;

        public float Elapsed => _elapsed;

        public bool IsDone => _elapsed >= _duration;

        /// <summary>진행률 [0,1]. 지속시간이 0이면 항상 1이다.</summary>
        public float Normalized
        {
            get
            {
                if (_duration <= 0f)
                {
                    return 1f;
                }

                float t = _elapsed / _duration;
                return t >= 1f ? 1f : t;
            }
        }

        /// <summary>
        /// 완료 후 초과한 시간. 반복 노드가 이 값을 다음 사이클로 넘겨
        /// 사이클마다 한 프레임씩 밀리는 것을 막는다. 진행 중에는 0이다.
        /// </summary>
        public float Overshoot
        {
            get
            {
                float over = _elapsed - _duration;
                return over > 0f ? over : 0f;
            }
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0f)
            {
                _elapsed += deltaSeconds;
            }
        }

        public void Reset()
        {
            _elapsed = 0f;
        }
    }
}
