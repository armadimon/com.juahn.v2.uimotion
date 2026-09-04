using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 다른 그래프 에셋을 노드 하나처럼 실행한다. 자주 쓰는 관용구를 한 곳에서 고치기 위한
    /// 재사용 단위다 — 복붙 그래프가 쌓이는 것을 막는다.
    ///
    /// 슬롯은 바깥 그래프와 공유한다. 같은 플레이어의 같은 계층이기 때문이다.
    /// </summary>
    [MotionNode(
        Name = "Sub Graph",
        Category = "Flow",
        Summary = "다른 그래프를 여기서 통째로 재생한다. 슬롯은 바깥과 공유한다.",
        Sample = "SubGraph")]
    [Serializable]
    public sealed class SubGraphAssetNode : SubGraphNode
    {
        [MotionParam(Label = "그래프", Tooltip = "재생할 그래프 에셋.")]
        public MotionGraph Target;

        protected override IMotionGraphView ResolveGraph()
        {
            // Unity의 가짜 null을 진짜 null로 바꾼다. 그냥 돌려주면 파괴된 에셋이
            // null이 아닌 참조로 나간다.
            return Target == null ? null : (IMotionGraphView)Target;
        }
    }
}
