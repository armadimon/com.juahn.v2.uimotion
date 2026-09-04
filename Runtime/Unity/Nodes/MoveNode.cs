using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Move",
        Category = "Transform",
        Summary = "대상을 지정한 위치로 옮긴다. 팝업이 아래에서 올라오거나 패널이 옆으로 미끄러지는 연출에 쓴다.",
        Sample = "Move")]
    [Serializable]
    public sealed class MoveNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표", Tooltip = "상대 이동이면 시작 위치로부터의 오프셋이다.")]
        public Vector2 To;

        [MotionParam(Label = "상대 이동", Tooltip = "켜면 지금 위치를 기준으로 더한다. 프리팹마다 위치가 달라도 같은 그래프를 쓸 수 있다.")]
        public bool Relative = true;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.25f;

        public EaseKind Ease = EaseKind.OutCubic;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector2 from = target.anchoredPosition;
            Vector2 to = Relative ? from + To : To;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = from;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                }
            });
        }
    }
}
