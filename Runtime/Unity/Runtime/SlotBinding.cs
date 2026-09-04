using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯 이름 하나와 그것이 가리키는 실제 오브젝트. <b>프리팹의 플레이어가 갖는다</b> —
    /// 그래프가 아니다. 그래서 그래프 에셋 하나를 여러 프리팹이 재사용할 수 있다.
    ///
    /// <see cref="Target"/>이 <c>UnityEngine.Object</c>인 이유는 무엇이 들어올지 모르기
    /// 때문이다. 노드가 요구하는 타입으로 바꾸는 것은 <see cref="MotionSlots"/>가 한다.
    /// </summary>
    [Serializable]
    public struct SlotBinding
    {
        public string Name;
        public UnityEngine.Object Target;

        public SlotBinding(string name, UnityEngine.Object target)
        {
            Name = name;
            Target = target;
        }
    }
}
