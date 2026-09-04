using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Set Active",
        Category = "Object",
        Summary = "대상을 켜거나 끈다. 즉시 끝난다. 연출 중간에 파티클이나 이펙트 오브젝트를 켤 때 쓴다."
                  + " '취소 시 복구'는 기본으로 꺼져 있다 — 자기 자신(Self)을 끄는 데 쓰면서 켜면"
                  + " 꺼지는 순간 다시 켜져 숨기기가 동작하지 않는다.",
        Sample = "SetActive")]
    [Serializable]
    public sealed class SetActiveNode : UnityEffectNode
    {
        [MotionSlot(typeof(GameObject))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "켜기")]
        public bool Active = true;

        /// <summary>
        /// 기본값이 false인 이유 — SetActive는 트윈이 아니라 이산적인 상태 변경이라
        /// "중단되면 되돌린다"가 대개 원하는 동작이 아니다. 켜 둔 이펙트를 연출이
        /// 끊겼다고 되돌려 끄면 화면이 오히려 어긋난다.
        ///
        /// 게다가 <see cref="SlotRef.Self"/>를 끄는 경우에는 Unity가 <c>SetActive(false)</c>에서
        /// <c>MotionPlayer.OnDisable</c> -> <c>StopAll</c> -> 스코프 취소를 <b>동기적으로</b>
        /// 부르므로, 방금 등록한 복구가 그 자리에서 실행되어 오브젝트가 되살아난다.
        /// </summary>
        [MotionParam(
            Label = "취소 시 복구",
            Tooltip = "켜면 연출이 취소될 때 원래 켜짐/꺼짐 상태로 돌아온다."
                      + " 자기 자신을 끄는 데 쓰는 노드에서 켜면 꺼지자마자 다시 켜진다.")]
        public bool RestoreOnCancel;

        public override bool Reverts => RestoreOnCancel;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            GameObject target = Resolve<GameObject>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            if (RestoreOnCancel)
            {
                bool wasActive = target.activeSelf;

                Remember(ctx, delegate
                {
                    if (target != null)
                    {
                        target.SetActive(wasActive);
                    }
                });
            }

            target.SetActive(Active);
            return MotionHandle.Completed;
        }
    }
}
