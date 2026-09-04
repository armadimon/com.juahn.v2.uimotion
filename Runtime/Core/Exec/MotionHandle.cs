using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 흔한 <see cref="IMotionHandle"/>들을 만드는 팩토리.
    /// </summary>
    public static class MotionHandle
    {
        private static readonly DoneHandle CompletedInstance = new DoneHandle();
        private static readonly DoneHandle SkippedInstance = new DoneHandle();

        /// <summary>정상적으로 즉시 끝난 핸들. 상태가 없어 공유해도 안전하다.</summary>
        public static IMotionHandle Completed => CompletedInstance;

        /// <summary>
        /// 실행되지 못하고 건너뛴 핸들 — 슬롯이 비었거나 대상이 없을 때 돌려준다.
        /// 완료와 구분되는 이유는 진단 때문이다. 실행기는 둘을 똑같이 다룬다.
        /// </summary>
        public static IMotionHandle Skipped => SkippedInstance;

        /// <summary>이 핸들이 <see cref="Skipped"/>인가.</summary>
        public static bool IsSkipped(IMotionHandle handle)
        {
            return ReferenceEquals(handle, SkippedInstance);
        }

        /// <summary>
        /// <paramref name="duration"/>초 동안 진행률 [0,1]을 <paramref name="onProgress"/>로 보내는 핸들.
        /// 끝날 때 반드시 정확히 1을 한 번 보낸다 — 그러지 않으면 페이드인이 0.97에서 멈춘다.
        /// <paramref name="onProgress"/>는 null이어도 된다(Delay처럼 진행률이 필요 없는 경우).
        /// </summary>
        public static IMotionHandle FromTimer(float duration, Action<float> onProgress)
        {
            return new TimerHandle(duration, onProgress);
        }

        private sealed class DoneHandle : IMotionHandle
        {
            public bool IsDone => true;

            public void Tick(float deltaSeconds)
            {
            }

            public void Cancel()
            {
            }
        }

        private sealed class TimerHandle : IMotionHandle
        {
            private readonly Action<float> _onProgress;
            private MotionTimer _timer;
            private bool _finished;

            public TimerHandle(float duration, Action<float> onProgress)
            {
                _timer = new MotionTimer(duration);
                _onProgress = onProgress;
                _finished = false;
            }

            public bool IsDone => _finished;

            public void Tick(float deltaSeconds)
            {
                if (_finished)
                {
                    return;
                }

                _timer.Advance(deltaSeconds);

                // Normalized가 알아서 1로 클램프하므로 오버슈트해도 끝값은 정확히 1이다.
                float t = _timer.Normalized;

                if (_onProgress != null)
                {
                    _onProgress(t);
                }

                if (_timer.IsDone)
                {
                    _finished = true;
                }
            }

            public void Cancel()
            {
                _finished = true;
            }
        }
    }
}
