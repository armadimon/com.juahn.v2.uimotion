using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Punch Scale",
        Category = "Transform",
        Summary = "대상을 잠깐 부풀렸다 되돌린다. 획득, 강조, 버튼 눌림 순간에 쓴다.",
        Sample = "PunchScale")]
    [Serializable]
    public sealed class PunchScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "세기", Tooltip = "0.2면 최대 20퍼센트까지 부푼다. 음수면 쪼그라든다.", Min = -1f, Max = 1f)]
        public float Amplitude = 0.2f;

        [MotionParam(Label = "시간", Min = 0f, Max = 2f)]
        public float Duration = 0.2f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 from = target.localScale;
            float amplitude = Amplitude;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = from;
                }
            });

            // 이징을 받지 않는 이유 — 펀치는 자기 곡선을 갖는다. sin(pi * t)는 t=0과 t=1에서
            // 정확히 0이고 t=0.5에서 정확히 1이다. 그래서 정확히 제자리에서 끝난다.
            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                float wave = Mathf.Sin(t * Mathf.PI);
                target.localScale = from * (1f + amplitude * wave);
            });
        }
    }
}
