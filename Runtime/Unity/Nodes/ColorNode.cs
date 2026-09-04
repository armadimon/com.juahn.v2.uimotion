using System;
using UnityEngine;
using UnityEngine.UI;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Color",
        Category = "Graphics",
        Summary = "대상의 색을 바꾼다. 버튼이 눌렸을 때 어두워지거나 경고로 붉어지는 연출에 쓴다.",
        Sample = "Color")]
    [Serializable]
    public sealed class ColorNode : UnityEffectNode
    {
        [MotionSlot(typeof(Graphic))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 색")]
        public Color To = Color.white;

        [MotionParam(Label = "알파 유지", Tooltip = "켜면 색만 바꾸고 투명도는 건드리지 않는다. Fade와 겹쳐 쓸 때 켠다.")]
        public bool KeepAlpha = true;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.15f;

        public EaseKind Ease = EaseKind.OutQuad;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Graphic graphic = Resolve<Graphic>(ctx, Target);
            if (graphic == null)
            {
                return MotionHandle.Skipped;
            }

            Color from = graphic.color;
            Color to = To;
            if (KeepAlpha)
            {
                to.a = from.a;
            }

            Remember(ctx, delegate
            {
                if (graphic != null)
                {
                    graphic.color = from;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (graphic != null)
                {
                    graphic.color = Color.LerpUnclamped(from, to, e);
                }
            });
        }
    }
}
