using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Shake",
        Category = "Transform",
        Summary = "대상을 흔든다. 조건 미달로 버튼을 눌렀을 때의 거부 반응이나 피격 연출에 쓴다.",
        Sample = "Shake")]
    [Serializable]
    public sealed class ShakeNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "세기", Tooltip = "최대 흔들림 폭(픽셀).", Min = 0f, Max = 200f)]
        public float Strength = 12f;

        [MotionParam(Label = "시간", Min = 0f, Max = 3f)]
        public float Duration = 0.3f;

        [MotionParam(Label = "빈도", Tooltip = "초당 방향이 바뀌는 횟수.", Min = 1f, Max = 60f)]
        public float Frequency = 20f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector2 origin = target.anchoredPosition;
            float strength = Strength;

            // 매 프레임 난수를 뽑으면 흔들림이 프레임률에 따라 달라진다. 시작할 때
            // 경로를 뽑아 두고 그 사이를 보간하면 60fps든 30fps든 같은 흔들림이 된다.
            int steps = Mathf.Max(2, Mathf.CeilToInt(Duration * Mathf.Max(1f, Frequency)));
            var path = new Vector2[steps + 1];
            path[0] = Vector2.zero;
            for (int i = 1; i < steps; i++)
            {
                path[i] = UnityEngine.Random.insideUnitCircle;
            }

            // 마지막 점을 0으로 박아 정확히 제자리에서 끝나게 한다.
            path[steps] = Vector2.zero;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = origin;
                }
            });

            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                float scaled = t * steps;
                int index = (int)scaled;
                if (index >= steps)
                {
                    target.anchoredPosition = origin;
                    return;
                }

                Vector2 offset = Vector2.Lerp(path[index], path[index + 1], scaled - index);

                // 뒤로 갈수록 잦아든다.
                target.anchoredPosition = origin + offset * (strength * (1f - t));
            });
        }
    }
}
