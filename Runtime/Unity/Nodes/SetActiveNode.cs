using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Set Active",
        Category = "Object",
        Summary = "대상을 켜거나 끈다. 즉시 끝난다. 연출 중간에 파티클이나 이펙트 오브젝트를 켤 때 쓴다.",
        Sample = "SetActive")]
    [Serializable]
    public sealed class SetActiveNode : UnityEffectNode
    {
        [MotionSlot(typeof(GameObject))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "켜기")]
        public bool Active = true;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            GameObject target = Resolve<GameObject>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            bool wasActive = target.activeSelf;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.SetActive(wasActive);
                }
            });

            target.SetActive(Active);
            return MotionHandle.Completed;
        }
    }
}
