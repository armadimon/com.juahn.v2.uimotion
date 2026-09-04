using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// Unity 대상을 만지는 효과 노드의 공통 베이스.
    ///
    /// 13개 노드가 전부 똑같이 하는 세 가지 — 슬롯 해석, 원상 복구 등록, 트윈 실행 —
    /// 를 여기 모은다. 노드를 새로 만드는 사람이 그 규약을 다시 발명하지 않게 하기 위해서다.
    ///
    /// <b>이 클래스에는 <c>[MotionNode]</c>를 달지 않는다.</b> 어트리뷰트가
    /// <c>Inherited = false</c>라 파생이 물려받지 않으므로 팔레트에 뜨지 않는다.
    /// </summary>
    [Serializable]
    public abstract class UnityEffectNode : MotionEffectNode
    {
        /// <summary>슬롯을 원하는 타입으로. 실패하면 null이고 경고가 한 번 남는다.</summary>
        protected static T Resolve<T>(IMotionContext ctx, SlotRef slot) where T : class
        {
            return MotionSlots.Resolve<T>(ctx, slot);
        }

        /// <summary>
        /// 취소될 때 되돌릴 것을 등록한다. <b>자연 완료 시에는 실행되지 않는다</b> —
        /// 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다.
        /// </summary>
        protected static void Remember(IMotionContext ctx, Action revert)
        {
            if (ctx != null && ctx.Scope != null && revert != null)
            {
                ctx.Scope.Remember(revert);
            }
        }

        /// <summary>
        /// 이징된 진행률을 흘리는 트윈을 시작한다. 호스트에 백엔드가 꽂혀 있으면 그것을,
        /// 아니면 내장 러너를 쓴다.
        /// </summary>
        protected static IMotionHandle Run(IMotionContext ctx, float duration, EaseKind ease, Action<float> onEased)
        {
            return ctx.Tween().Run(duration, ease, onEased);
        }
    }
}
