using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Float",
        Category = "Transform",
        Summary = "대상을 천천히 위아래로 부유시킨다. 끝나지 않으므로 Loop 트리거에 문다. 취소되면 제자리로 돌아온다.",
        Sample = "Float")]
    [Serializable]
    public sealed class FloatNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "진폭", Tooltip = "각 축의 최대 이동 폭(픽셀).")]
        public Vector2 Amplitude = new Vector2(0f, 8f);

        [MotionParam(Label = "주기", Tooltip = "한 번 왕복하는 데 걸리는 시간(초).", Min = 0.1f, Max = 10f)]
        public float Period = 2f;

        [MotionParam(Label = "위상", Tooltip = "0에서 1. 여러 오브젝트를 어긋나게 띄울 때 쓴다.", Min = 0f, Max = 1f)]
        public float Phase;

        public override bool Reverts => true;

        public override bool BlocksChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            // 원점을 시작할 때 한 번만 잡는다. 이것을 매번 다시 잡으면 사인파의 중간 위치가
            // 다음 기준이 되어 오브젝트가 갈수록 밀린다. 방치형에서 몇 시간 뒤에 드러나는
            // 종류의 버그다.
            Vector2 origin = target.anchoredPosition;
            Vector2 amplitude = Amplitude;
            float period = Mathf.Max(0.01f, Period);
            float phase = Phase;

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

                float wave = Mathf.Sin((elapsed / period + phase) * 2f * Mathf.PI);
                target.anchoredPosition = origin + amplitude * wave;
            });
        }
    }
}
