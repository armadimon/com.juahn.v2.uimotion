using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [Serializable, MotionNode(Name = "Follow Value", Category = "UI", Summary = "현재 표시값에서 실행별 목표값까지 보간한다. 취소해도 현재 표시값을 유지한다.")]
    public sealed class ValueNode : UnityEffectNode
    {
        [MotionSlot(typeof(MotionValue))] public SlotRef Target = SlotRef.Self;
        public string TargetParameter = "Target";
        public string DurationParameter = "Duration";
        public float Duration = .3f;
        public EaseKind Ease = EaseKind.OutCubic;
        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            var target = Resolve<MotionValue>(ctx, Target);
            if (target == null) return MotionHandle.Skipped;
            var scope = ctx.Scope as MotionScope;
            var from = target.Value;
            var to = scope?.Parameter(TargetParameter, from) ?? from;
            var duration = scope?.Parameter(DurationParameter, Duration) ?? Duration;
            return Run(ctx, duration, Ease, t => { if (target != null) target.Value = Mathf.LerpUnclamped(from, to, t); });
        }
    }
}
