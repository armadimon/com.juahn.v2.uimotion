using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [Serializable, MotionNode(Name = "Sway", Category = "Transform", Summary = "기준 회전을 중심으로 왕복한다. Loop 전용, 취소 시 복원한다.")]
    public sealed class SwayNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))] public SlotRef Target = SlotRef.Self;
        public float Degrees = 4f;
        public float Period = .9f;
        public override bool Reverts => true;
        public override bool BlocksChildren => true;
        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            var target = Resolve<Transform>(ctx, Target);
            if (target == null) return MotionHandle.Skipped;
            var origin = target.localRotation;
            Remember(ctx, () => { if (target != null) target.localRotation = origin; });
            return MotionHandle.Forever(elapsed =>
            {
                if (target != null) target.localRotation = origin * Quaternion.Euler(0, 0, Degrees * Mathf.Sin(elapsed * 2f * Mathf.PI / Mathf.Max(.01f, Period)));
            });
        }
    }
}
