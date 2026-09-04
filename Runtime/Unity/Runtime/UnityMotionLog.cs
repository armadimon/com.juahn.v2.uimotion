using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 코어의 진단을 Unity 콘솔로 흘린다.
    ///
    /// <paramref name="context"/>를 함께 넘기는 이유는 콘솔에서 메시지를 눌렀을 때
    /// 문제를 낸 오브젝트가 계층에서 선택되게 하기 위해서다. 방치형 게임에서
    /// "어떤 팝업의 어떤 슬롯이 비었는가"를 찾는 데 이것이 결정적이다.
    /// </summary>
    public sealed class UnityMotionLog : IMotionLog
    {
        private readonly Object _context;

        public UnityMotionLog(Object context)
        {
            _context = context;
        }

        public void Warn(string message)
        {
            Debug.LogWarning("[UiMotion] " + message, _context);
        }

        public void Error(string message)
        {
            Debug.LogError("[UiMotion] " + message, _context);
        }
    }
}
