using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프가 바깥세상에 무언가를 알리는 유일한 통로.
    ///
    /// <b>왜 사운드 노드와 햅틱 노드를 만들지 않는가</b> — 프로젝트마다 오디오 시스템이
    /// 다르므로 코어에 넣으면 "어떤 프로젝트에서든 단독으로 쓸 수 있다"가 깨진다.
    /// 그래프는 <c>"sfx:click"</c> 같은 문자열만 던지고, 그것을 받아 실제로 소리를 내거나
    /// 진동시키는 쪽은 프로젝트가 구현한다.
    ///
    /// 프로젝트 부트스트랩에서 구독하고, <b>씬 전환 때 구독을 해제한다</b> —
    /// static 이벤트라 구독이 남으면 파괴된 오브젝트를 붙잡는다.
    /// </summary>
    public static class MotionSignals
    {
        /// <summary>신호가 발생했다. 두 번째 인자는 신호를 낸 플레이어의 오브젝트다(없으면 null).</summary>
        public static event Action<string, GameObject> Received;

        /// <summary>
        /// 플레이 시작마다 구독을 비운다.
        ///
        /// <b>왜 필요한가</b> — Enter Play Mode Options로 도메인 리로드를 끄면 static 이벤트가
        /// 플레이 세션을 넘어 살아남아 지난 세션의 파괴된 오브젝트를 붙잡는다.
        /// <see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>은 씬 로드 전이므로
        /// 이번 세션의 정상 구독은 지우지 않는다. <see cref="MotionPump"/>가 쓰는 것과 같은 방식이다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Received = null;
        }

        /// <summary>
        /// 구독자를 하나씩 부른다. <b>구독자 하나의 예외가 나머지를 막지 않는다</b> —
        /// 멀티캐스트 델리게이트를 통째로 부르면 첫 예외에서 호출이 끊기고, 그 예외가
        /// 그래프 실행 스택을 타고 올라가 펌프의 틱까지 멈춘다.
        /// </summary>
        public static void Emit(string signal, GameObject source)
        {
            if (string.IsNullOrEmpty(signal))
            {
                return;
            }

            Action<string, GameObject> handler = Received;
            if (handler == null)
            {
                return;
            }

            Delegate[] subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<string, GameObject>)subscribers[i])(signal, source);
                }
                catch (Exception error)
                {
                    Debug.LogException(error, source);
                }
            }
        }

        /// <summary>구독을 전부 끊는다. 테스트와 씬 전환이 쓴다.</summary>
        public static void Clear()
        {
            Received = null;
        }
    }
}
