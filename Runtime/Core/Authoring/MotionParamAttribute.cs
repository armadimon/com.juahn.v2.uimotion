using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 파라미터 필드의 표시 정보. UnityEngine의 <c>[Tooltip]</c>·<c>[Range]</c>를 코어에서
    /// 쓸 수 없으므로 이것이 대신한다. 그래프 창의 노드 인스펙터가 읽는다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class MotionParamAttribute : Attribute
    {
        /// <summary>인스펙터에 뜨는 이름. 비우면 필드 이름을 쓴다.</summary>
        public string Label { get; set; }

        public string Tooltip { get; set; }

        /// <summary>숫자 필드의 슬라이더 하한. <see cref="Max"/>와 함께 지정할 때만 슬라이더가 뜬다.</summary>
        public float Min { get; set; }

        public float Max { get; set; }

        public bool HasRange => Max > Min;
    }
}
