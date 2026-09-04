using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 다른 트리거를 멈춘다. 상시 도는 강조 연출을 특정 시점에 끄는 데 쓴다.
    /// </summary>
    [MotionNode(Name = "Stop Trigger", Category = "Flow",
        Summary = "다른 트리거를 멈춘다. 상시 도는 강조 연출을 특정 시점에 끌 때 쓴다.",
        Sample = "StopTrigger")]
    [Serializable]
    public sealed class StopTriggerNode : MotionFlowNode
    {
        public string TriggerName;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (ctx != null && ctx.Triggers != null && !string.IsNullOrEmpty(TriggerName))
            {
                ctx.Triggers.Stop(TriggerName);
            }

            return MotionHandle.Completed;
        }
    }
}
