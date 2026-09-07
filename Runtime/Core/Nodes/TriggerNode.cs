using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거의 진입점. <b>이 노드가 트리거 선언의 유일한 진실이다.</b>
    ///
    /// 그래프의 트리거 목록은 저작하는 값이 아니라 이 노드들에서 계산되는 파생값이다 —
    /// 슬롯 목록이 노드의 <c>[MotionSlot]</c> 필드에서 계산되는 것과 같다. 진실이 하나면
    /// 어긋날 수 없고, "이 노드가 Start의 진입점"이라는 사실이 캔버스에 그대로 보인다.
    ///
    /// 실행에는 관여하지 않는다 — 즉시 완료하고 자식으로 흘려보낸다.
    /// </summary>
    [MotionNode(Name = "Trigger", Category = "Flow",
        Summary = "트리거의 진입점. 이 노드의 이름으로 Fire를 부르면 아래로 이어진 연출이 돈다.",
        Sample = "Trigger")]
    [Serializable]
    public sealed class TriggerNode : MotionFlowNode
    {
        /// <summary>
        /// <c>Fire</c>에 넘길 이름. <c>Start</c> · <c>Loop</c> · <c>End</c>는 예약 이름으로
        /// 위상 규약이 붙는다 (<see cref="MotionRuntime"/> 참조). 그 밖의 이름은 자유다.
        /// </summary>
        public string TriggerName;

        /// <summary>
        /// 이미 재생 중인데 다시 발사했을 때의 처리.
        ///
        /// 예전에는 그래프의 트리거 목록에 있었다. 노드로 옮긴 이유는 진실을 하나로
        /// 모으기 위해서다 — 두 곳에 있으면 어긋났을 때 무엇이 맞는지 알 수 없다.
        /// </summary>
        public TriggerPolicy Policy = TriggerPolicy.Restart;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            return MotionHandle.Completed;
        }
    }
}
