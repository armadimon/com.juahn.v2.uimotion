using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프가 선언한 슬롯 하나. 프리팹의 플레이어가 이 목록을 보고 인스펙터를 그린다.
    /// </summary>
    [Serializable]
    public sealed class SlotDeclaration
    {
        public string Name;

        /// <summary>
        /// 이 슬롯에 넣을 수 있는 타입. 코어는 검사하지 않고 나르기만 한다 —
        /// 실제 대조는 Unity 계층과 에디터 인스펙터가 한다.
        /// </summary>
        [NonSerialized] public Type RequiredType;

        public SlotDeclaration()
        {
        }

        public SlotDeclaration(string name, Type requiredType = null)
        {
            Name = name;
            RequiredType = requiredType;
        }
    }
}
