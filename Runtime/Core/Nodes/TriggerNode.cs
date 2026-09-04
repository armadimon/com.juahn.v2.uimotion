using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거의 진입 표식. 아무 일도 하지 않고 자식으로 흘려보낸다.
    ///
    /// 실행에는 필요 없지만 그래프 창에서 "여기가 이 트리거의 시작"을 보여 주는
    /// 앵커 역할을 한다. 그래서 실행 의미가 아니라 <b>편집 의미</b>를 갖는 노드다.
    /// </summary>
    [MotionNode(Name = "Trigger", Category = "Flow",
        Summary = "트리거의 진입 표식. 실행에는 관여하지 않고 자식으로 흘려보낸다.",
        Sample = "Trigger")]
    [Serializable]
    public sealed class TriggerNode : MotionFlowNode
    {
        public string TriggerName;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            return MotionHandle.Completed;
        }
    }
}
