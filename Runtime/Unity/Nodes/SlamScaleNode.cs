using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬램 — 큰 크기에서 <b>줄어들며 박힌다.</b> 닿는 순간 한 번 눌렸다 돌아온다.
    ///
    /// <see cref="PopScaleNode"/>가 "없던 것이 튀어나오는" 등장이라면 이것은 "위에서
    /// 떨어져 꽂히는" 등장이다. IdlePaori는 뽑기 결과에서 귀한 등급에만 이것을 쓴다 —
    /// 1등은 2.2배, 2등은 1.5배에서 떨어진다.
    ///
    /// <b>대상이 자기 자리를 넘어선다.</b> 시작 배율이 1보다 크므로 그 순간 이웃을
    /// 침범한다. 그리드에서 형제 순서는 <b>자리</b>를 정하므로 순서를 바꿔 위로 올릴 수
    /// 없다 — 겹침은 여백이나 오버레이로 푼다.
    /// </summary>
    [MotionNode(
        Name = "Slam Scale",
        Category = "Transform",
        Summary = "큰 크기에서 줄어들며 박힌다. 귀한 결과가 꽂히는 순간에 쓴다.",
        Sample = "SlamScale")]
    [Serializable]
    public sealed class SlamScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(
            Label = "시작 배율",
            Tooltip = "제자리 크기의 몇 배에서 떨어지는가. 1 미만은 1로 잡힌다 - 슬램은 줄어들며 박히는 것이다.",
            Min = 1f, Max = 4f)]
        public float FromScale = 2.2f;

        [MotionParam(Label = "시간", Min = 0f, Max = 2f)]
        public float Duration = 0.28f;

        [MotionParam(
            Label = "낙하 구간",
            Tooltip = "전체 시간 중 내려오는 데 쓰는 비율. 나머지는 눌렸다 돌아오는 데 쓴다.",
            Min = 0.5f, Max = 1f)]
        public float DropRatio = 0.72f;

        [MotionParam(
            Label = "눌리는 정도",
            Tooltip = "박히는 순간 제자리 크기 아래로 눌리는 깊이. 0이면 눌리지 않는다.",
            Min = 0f, Max = 0.5f)]
        public float Impact = 0.12f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 baseScale = MotionBaseScale.Of(target);
            float start = FromScale;
            float drop = DropRatio;
            float impact = Impact;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = baseScale;
                }
            });

            target.localScale = baseScale * (start < 1f ? 1f : start);

            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                target.localScale = baseScale * AppearCurves.Slam(t, start, drop, impact);
            });
        }
    }
}
