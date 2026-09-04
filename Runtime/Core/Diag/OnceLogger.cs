using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 같은 키의 경고를 한 번만 통과시키는 로그 래퍼.
    ///
    /// 방치형 게임이라 연출이 몇 시간씩 매 프레임 돈다. 미할당 슬롯 하나가
    /// 콘솔을 수십만 줄로 덮으면 진짜 문제를 찾을 수 없다.
    /// <b>오류는 억제하지 않는다</b> — 놓치면 안 되는 것이기 때문이다.
    /// </summary>
    public sealed class OnceLogger : IMotionLog
    {
        private readonly IMotionLog _sink;
        private readonly HashSet<string> _seen = new HashSet<string>();

        public OnceLogger(IMotionLog sink)
        {
            _sink = sink;
        }

        /// <summary><paramref name="key"/>가 처음일 때만 경고한다.</summary>
        public void WarnOnce(string key, string message)
        {
            if (!_seen.Add(key))
            {
                return;
            }

            Warn(message);
        }

        /// <summary>억제 없이 그대로 흘려보낸다.</summary>
        public void Warn(string message)
        {
            if (_sink != null)
            {
                _sink.Warn(message);
            }
        }

        public void Error(string message)
        {
            if (_sink != null)
            {
                _sink.Error(message);
            }
        }

        /// <summary>억제 기록을 비운다. 플레이어가 다시 활성화될 때 부른다.</summary>
        public void Reset()
        {
            _seen.Clear();
        }
    }
}
