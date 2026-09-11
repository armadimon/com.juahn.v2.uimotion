using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [Serializable, MotionNode(Name = "Rect Pose", Category = "Transform", Summary = "레이아웃 후 저장한 제자리 기준의 두 오프셋 사이를 이동한다.")]
    public sealed class RectPoseNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))] public SlotRef Target = SlotRef.Self;
        public Vector2 FromOffset, ToOffset;
        public float Duration = .25f;
        public EaseKind Ease = EaseKind.OutCubic;
        public override bool Reverts => true;
        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            var target = Resolve<RectTransform>(ctx, Target);
            if (target == null) return MotionHandle.Skipped;
            var player = ctx.Host as MotionPlayer;
            var rest = player == null ? target.anchoredPosition : player.BaseAnchoredPositionOf(target);
            var from = rest + FromOffset; var to = rest + ToOffset;
            Remember(ctx, () => { if (target != null) target.anchoredPosition = rest; });
            target.anchoredPosition = from;
            return Run(ctx, Duration, Ease, t => { if (target != null) target.anchoredPosition = Vector2.LerpUnclamped(from, to, t); });
        }
    }
}
