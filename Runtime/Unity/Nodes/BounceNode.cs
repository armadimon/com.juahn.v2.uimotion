using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Bounce",
        Category = "Transform",
        Summary = "대상을 통통 튀게 한다. 끝나지 않으므로 Loop 트리거에 문다. 눌러 달라고 조르는 버튼에 쓴다.",
        Sample = "Bounce")]
    [Serializable]
    public sealed class BounceNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "높이", Tooltip = "튀어 오르는 높이(픽셀).", Min = 0f, Max = 200f)]
        public float Height = 16f;

        [MotionParam(Label = "주기", Tooltip = "한 번 튀는 데 걸리는 시간(초).", Min = 0.1f, Max = 10f)]
        public float Period = 0.8f;

        [MotionParam(Label = "쉬는 시간", Tooltip = "튄 다음 가만히 있는 시간(초).", Min = 0f, Max = 10f)]
        public float RestTime = 0.4f;

        public override bool Reverts => true;

        public override bool BlocksChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector2 origin = target.anchoredPosition;
            float height = Height;
            float period = Mathf.Max(0.01f, Period);
            float cycle = period + Mathf.Max(0f, RestTime);

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = origin;
                }
            });

            return MotionHandle.Forever(delegate(float elapsed)
            {
                if (target == null)
                {
                    return;
                }

                float inCycle = elapsed % cycle;

                if (inCycle >= period)
                {
                    // 쉬는 구간. 정확히 원점에 놓는다.
                    target.anchoredPosition = origin;
                    return;
                }

                // 반원 궤적이라 올라갈 때 빠르고 꼭대기에서 느려진다 — 중력에 가까운 느낌이다.
                float t = inCycle / period;
                float offset = height * Mathf.Sin(t * Mathf.PI);
                target.anchoredPosition = new Vector2(origin.x, origin.y + offset);
            });
        }
    }
}
