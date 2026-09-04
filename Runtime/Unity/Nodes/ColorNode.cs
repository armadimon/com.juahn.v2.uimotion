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

        /// <summary>
        /// 켜져 있으면 알파를 <b>매 프레임 다시 읽어</b> 보존한다. 시작할 때 한 번만 잡아 두면
        /// 겹쳐 도는 Fade가 쓴 알파를 이 노드가 매 프레임 되돌려 페이드가 보이지 않는다.
        /// <c>FadeNode.FadeGraphic</c>이 RGB를 매 프레임 다시 읽는 것과 같은 이유다.
        /// </summary>
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
            bool keepAlpha = KeepAlpha;

            Remember(ctx, delegate
            {
                if (graphic == null)
                {
                    return;
                }

                Color restored = from;
                if (keepAlpha)
                {
                    // 알파는 이 노드가 만진 적이 없다. 되돌리면 그 사이 Fade가 만든
                    // 투명도를 덮어쓴다.
                    restored.a = graphic.color.a;
                }

                graphic.color = restored;
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (graphic == null)
                {
                    return;
                }

                Color next = Color.LerpUnclamped(from, to, e);
                if (keepAlpha)
                {
                    next.a = graphic.color.a;
                }

                graphic.color = next;
            });
        }
    }
}
