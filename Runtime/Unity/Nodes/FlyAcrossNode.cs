using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Fly Across",
        Category = "Transform",
        Summary = "대상을 한 지점에서 다른 지점으로 곡선을 그리며 날린다. 획득한 재화가 상단 카운터로 빨려 들어가는 연출에 쓴다.",
        Sample = "FlyAcross")]
    [Serializable]
    public sealed class FlyAcrossNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionSlot(typeof(RectTransform))]
        [MotionParam(Label = "출발", Tooltip = "비워 두면 대상의 지금 위치에서 출발한다.")]
        public SlotRef From;

        [MotionSlot(typeof(RectTransform))]
        [MotionParam(Label = "도착")]
        public SlotRef To;

        [MotionParam(Label = "호 높이", Tooltip = "직선에서 얼마나 부풀릴지(월드 단위). 0이면 직선이다.")]
        public float Arc = 80f;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.5f;

        public EaseKind Ease = EaseKind.InOutCubic;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            RectTransform destination = Resolve<RectTransform>(ctx, To);
            if (destination == null)
            {
                // 도착지가 없으면 날 곳이 없다. 슬롯 해석이 이미 경고를 한 번 남겼다.
                return MotionHandle.Skipped;
            }

            // 캔버스가 서로 다를 수 있으므로 월드 좌표로 계산한다. anchoredPosition은
            // 부모가 다르면 비교할 수 없다.
            RectTransform origin = From.IsValid ? Resolve<RectTransform>(ctx, From) : null;

            Vector3 startPosition = target.position;
            Vector3 fromPosition = origin != null ? origin.position : startPosition;
            Vector3 toPosition = destination.position;
            float arc = Arc;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.position = startPosition;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target == null)
                {
                    return;
                }

                Vector3 straight = Vector3.LerpUnclamped(fromPosition, toPosition, e);

                // sin(pi * e)는 양 끝에서 정확히 0이라 출발점과 도착점을 어긋나게 하지 않는다.
                straight.y += arc * Mathf.Sin(Mathf.Clamp01(e) * Mathf.PI);

                target.position = straight;
            });
        }
    }
}
