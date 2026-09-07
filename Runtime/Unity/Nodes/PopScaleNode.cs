using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 등장 팝 — 작게 시작해 제자리 크기로 <b>넘쳤다 돌아온다.</b>
    ///
    /// <see cref="ScaleNode"/>와 나누는 이유는 둘이다.
    ///
    /// 하나, <b>시작 크기를 명시한다.</b> <see cref="ScaleNode"/>는 지금 크기에서 목표로 가므로
    /// 이미 제자리 크기인 대상에는 아무 일도 일어나지 않는다. 등장은 반드시 작은 데서
    /// 시작해야 하고, 다시 발사해도 같은 연출이 나와야 한다.
    ///
    /// 둘, <b>넘치는 세기를 연다.</b> <c>EaseKind.OutBack</c>의 오버슈트는 상수라 세기를
    /// 바꿀 수 없다. 그런데 등장 연출에서 세기는 곧 무게다 — 평범한 칸과 귀한 칸을
    /// 가르는 것이 그것 하나다.
    ///
    /// 수치는 IdlePaori의 <c>SlotRevealGrid</c>에서 옮겨 왔다. 일반 칸 0.18초·1.7,
    /// 강조 칸 0.28초·3.2.
    /// </summary>
    [MotionNode(
        Name = "Pop Scale",
        Category = "Transform",
        Summary = "작게 시작해 제자리 크기로 넘쳤다 돌아온다. 칸이나 보상이 터져 나오는 등장에 쓴다.",
        Sample = "PopScale")]
    [Serializable]
    public sealed class PopScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(
            Label = "시작 배율",
            Tooltip = "제자리 크기의 몇 배에서 시작하는가. 0이면 아무것도 없는 데서 터져 나온다.",
            Min = 0f, Max = 1f)]
        public float FromScale;

        [MotionParam(
            Label = "넘치는 세기",
            Tooltip = "0이면 넘치지 않고 그대로 도착한다. 1.7이 기본 감각, 3.2면 크게 튄다.",
            Min = 0f, Max = 6f)]
        public float Overshoot = AppearCurves.DefaultOvershoot;

        [MotionParam(Label = "시간", Min = 0f, Max = 2f)]
        public float Duration = 0.18f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            // 지금 크기가 아니라 제자리 크기를 기준으로 잡는다. 등장 도중에 다시 발사되면
            // 지금 크기는 연출 중간값이라 그것을 기준으로 삼으면 점점 작아진다.
            Vector3 baseScale = MotionBaseScale.Of(target);
            Vector3 from = baseScale * FromScale;
            float overshoot = Overshoot;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = baseScale;
                }
            });

            target.localScale = from;

            // 이징을 받지 않는다 - 이 노드의 곡선이 곧 이 노드다. 밖에서 이징을 겹치면
            // 오버슈트가 두 번 적용돼 저작한 세기와 다른 것이 나온다.
            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                float k = AppearCurves.OutBack(t, overshoot);
                target.localScale = Vector3.LerpUnclamped(from, baseScale, k);
            });
        }
    }
}
