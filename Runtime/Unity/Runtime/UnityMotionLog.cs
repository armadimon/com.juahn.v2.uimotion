using System;
using UnityEngine;

// System을 들이면 Object가 UnityEngine.Object와 System.Object 사이에서 모호해진다.
// 이 파일에서 Object는 언제나 Unity 쪽이다 - 콘솔이 눌렀을 때 계층에서 선택할 대상.
using Object = UnityEngine.Object;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 코어의 진단을 Unity 콘솔로 흘린다.
    ///
    /// 문맥 오브젝트를 함께 넘기는 이유는 콘솔에서 메시지를 눌렀을 때
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

        /// <summary>
        /// 이 로그를 지나간 모든 진단. <b>에디터 도구가 구독한다.</b>
        ///
        /// 콘솔만으로는 부족한 이유 — 경고는 인스턴스당 한 번만 나오고(<c>OnceLogger</c>),
        /// 다른 로그에 밀려 올라가거나 콘솔을 지우면 다시 볼 방법이 없다. 어느 플레이어에서
        /// 났는지도 메시지 문구로만 알 수 있다.
        ///
        /// <b>구독은 에디터에서만 한다.</b> 빌드에서 이것을 붙잡으면 목록이 무한히 자란다.
        /// 구독자가 없으면 아무 비용도 들지 않는다.
        ///
        /// 인자는 순서대로 심각도 · 본문 · 문맥 오브젝트(없을 수 있다)다. 본문은 콘솔에
        /// 찍히는 것과 같은 접두사 없는 원문이다.
        /// </summary>
        public static event Action<MotionIssueLevel, string, Object> Emitted;

        public void Warn(string message)
        {
            Debug.LogWarning("[UiMotion] " + message, _context);
            Emit(MotionIssueLevel.Warning, message, _context);
        }

        public void Error(string message)
        {
            Debug.LogError("[UiMotion] " + message, _context);
            Emit(MotionIssueLevel.Error, message, _context);
        }

        /// <summary>
        /// 구독자에게 흘린다.
        ///
        /// <b>구독자의 예외가 로그를 타고 올라가면 안 된다.</b> 이 로그는 연출이 도는
        /// 도중에 불리므로, 에디터 도구 하나가 던진 예외가 재생을 끊는 것은
        /// 진단 도구가 만들 수 있는 가장 나쁜 결과다.
        /// </summary>
        private static void Emit(MotionIssueLevel level, string message, Object context)
        {
            Action<MotionIssueLevel, string, Object> handlers = Emitted;
            if (handlers == null)
            {
                return;
            }

            try
            {
                handlers(level, message, context);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }
    }
}
