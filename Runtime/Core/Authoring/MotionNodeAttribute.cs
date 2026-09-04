using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드의 이름·분류·설명·작동 예시를 선언한다. 에디터의 노드 팔레트와 Node Doctor가 읽는다.
    ///
    /// 설명을 별도 문서가 아니라 여기에 두는 이유는 코드 옆에 있어야 썩지 않고,
    /// 툴이 기계적으로 검사할 수 있기 때문이다.
    ///
    /// <see cref="AttributeUsageAttribute.Inherited"/>가 false인 이유 — 파생 노드가 부모의
    /// 설명을 물려받으면 Node Doctor가 거짓 통과를 낸다. 각 노드는 자기 설명을 스스로 달아야 한다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MotionNodeAttribute : Attribute
    {
        /// <summary>팔레트에 뜨는 이름. 예: "Scale Punch".</summary>
        public string Name { get; set; }

        /// <summary>팔레트 분류. 예: "Transform".</summary>
        public string Category { get; set; }

        /// <summary>이 노드가 무엇을 하고 언제 쓰는지 한두 문장.</summary>
        public string Summary { get; set; }

        /// <summary>
        /// 작동 예시 그래프의 이름. <c>Samples~/Nodes/&lt;Sample&gt;.motiongraph</c>를 가리킨다.
        /// 팔레트에서 이 노드에 호버하면 그 그래프가 그 자리에서 재생된다.
        /// </summary>
        public string Sample { get; set; }

        /// <summary>
        /// 설명과 작동 예시를 모두 갖췄는가. 아니면 팔레트의 "미검증" 섹션으로 격리되고
        /// CI 게이트에서 실패한다. 쓰는 것 자체를 막지는 않는다 — 실험을 막지 않기 위해서다.
        /// </summary>
        public bool IsVerified =>
            !string.IsNullOrWhiteSpace(Summary) && !string.IsNullOrWhiteSpace(Sample);
    }
}
