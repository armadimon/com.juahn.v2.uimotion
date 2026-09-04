using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Rotate",
        Category = "Transform",
        Summary = "대상을 회전시킨다. 로딩 스피너나 획득 아이콘의 한 바퀴 돌기에 쓴다.",
        Sample = "Rotate")]
    [Serializable]
    public sealed class RotateNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 각도", Tooltip = "상대 회전이면 지금 각도에 더한다. 360을 넘겨 여러 바퀴를 돌릴 수 있다.")]
        public Vector3 ToEuler;

        [MotionParam(Label = "상대 회전")]
        public bool Relative = true;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.4f;

        public EaseKind Ease = EaseKind.Linear;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            // 쿼터니언이 아니라 오일러각을 보간한다. 쿼터니언은 최단 경로로 돌기 때문에
            // "두 바퀴 돌기"(720도) 같은 지시를 표현할 수 없다.
            Vector3 fromEuler = target.localEulerAngles;
            Vector3 toEuler = Relative ? fromEuler + ToEuler : ToEuler;
            Quaternion fromRotation = target.localRotation;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localRotation = fromRotation;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target != null)
                {
                    target.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(fromEuler, toEuler, e));
                }
            });
        }
    }
}
