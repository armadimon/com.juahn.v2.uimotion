using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 실행 순서를 정하는 노드. 대상을 만지지 않는다.
    ///
    /// 자식을 직접 조율하는 흐름 노드는 <see cref="MotionNodeBase.OwnsChildren"/>을 true로 덮는다.
    /// 덮지 않으면(예: <c>Delay</c>) 실행기가 자기 핸들이 끝난 뒤 자식들을 이어 붙인다.
    /// </summary>
    [Serializable]
    public abstract class MotionFlowNode : MotionNodeBase
    {
    }
}
