using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 정해진 시간을 기다린 뒤 자식으로 넘어간다.
    ///
    /// 자식을 소유하지 않는다 — 기다림이 끝나면 실행기가 알아서 자식을 잇는다.
    /// 그래서 구현이 타이머 하나로 끝난다.
    /// </summary>
    [MotionNode(Name = "Delay", Category = "Flow",
        Summary = "정해진 시간을 기다린 뒤 자식으로 넘어간다.",
        Sample = "Delay")]
    [Serializable]
    public sealed class DelayNode : MotionFlowNode
    {
        [MotionParam(Label = "지연(초)", Min = 0f, Max = 5f)]
        public float Seconds;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (Seconds <= 0f)
            {
                return MotionHandle.Completed;
            }

            return MotionHandle.FromTimer(Seconds, null);
        }
    }
}
