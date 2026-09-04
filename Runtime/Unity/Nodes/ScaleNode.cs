using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Scale",
        Category = "Transform",
        Summary = "대상의 크기를 바꾼다. 팝업이 작게 나타나 커지는 등장 연출에 쓴다.",
        Sample = "Scale")]
    [Serializable]
    public sealed class ScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 배율", Tooltip = "상대 배율이면 지금 크기에 성분별로 곱한다.")]
        public Vector3 To = Vector3.one;

        [MotionParam(Label = "상대 배율")]
        public bool Relative;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.25f;

        public EaseKind Ease = EaseKind.OutBack;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 from = target.localScale;
            Vector3 to = Relative
                ? new Vector3(from.x * To.x, from.y * To.y, from.z * To.z)
                : To;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = from;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(from, to, e);
                }
            });
        }
    }
}
