using System;
using UnityEngine;
using UnityEngine.UI;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Fade",
        Category = "Graphics",
        Summary = "대상을 서서히 나타내거나 사라지게 한다. CanvasGroup을 우선 쓰고, 없으면 Graphic의 알파를 쓴다.",
        Sample = "Fade")]
    [Serializable]
    public sealed class FadeNode : UnityEffectNode
    {
        /// <summary>
        /// 요구 타입이 <c>Component</c>인 이유 — CanvasGroup과 Graphic 둘 다 받는다.
        /// 화면 전체를 페이드할 때는 CanvasGroup이, 아이콘 하나면 Image가 자연스럽다.
        /// </summary>
        [MotionSlot(typeof(Component))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 알파", Min = 0f, Max = 1f)]
        public float To = 1f;

        [MotionParam(Label = "시작 알파에서", Tooltip = "켜면 From에서 시작한다. 끄면 지금 알파에서 시작한다.")]
        public bool UseFrom;

        [MotionParam(Label = "시작 알파", Min = 0f, Max = 1f)]
        public float From;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.2f;

        public EaseKind Ease = EaseKind.OutQuad;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            // 슬롯 해석은 한 번만 한다. CanvasGroup으로 먼저 해석하고 실패하면 Graphic으로
            // 다시 해석하면, 첫 시도가 "노드를 건너뛰었다"는 거짓 경고를 남긴다 —
            // 실제로는 Graphic 경로로 정상 동작하는데도 그렇다.
            Component target = Resolve<Component>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            CanvasGroup group = target as CanvasGroup;
            if (group == null)
            {
                group = target.GetComponent<CanvasGroup>();
            }

            if (group != null)
            {
                return FadeGroup(ctx, group);
            }

            Graphic graphic = target as Graphic;
            if (graphic == null)
            {
                graphic = target.GetComponent<Graphic>();
            }

            if (graphic != null)
            {
                return FadeGraphic(ctx, graphic);
            }

            WarnNoFadeTarget(ctx, target);
            return MotionHandle.Skipped;
        }

        private void WarnNoFadeTarget(IMotionContext ctx, Component target)
        {
            string graphName = ctx.Graph == null ? "<no graph>" : ctx.Graph.GraphName;
            MotionLogs.WarnOnce(ctx.Log, "fade:" + graphName + ":" + Target.Name,
                "graph '" + graphName + "': slot '" + Target + "' resolved to '" + target.name +
                "' which has neither CanvasGroup nor Graphic; the node was skipped");
        }

        private IMotionHandle FadeGroup(IMotionContext ctx, CanvasGroup group)
        {
            float original = group.alpha;
            float from = UseFrom ? From : original;
            float to = To;

            Remember(ctx, delegate
            {
                if (group != null)
                {
                    group.alpha = original;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (group != null)
                {
                    group.alpha = Mathf.Clamp01(Mathf.LerpUnclamped(from, to, e));
                }
            });
        }

        private IMotionHandle FadeGraphic(IMotionContext ctx, Graphic graphic)
        {
            Color originalColor = graphic.color;
            float from = UseFrom ? From : originalColor.a;
            float to = To;

            Remember(ctx, delegate
            {
                if (graphic != null)
                {
                    graphic.color = originalColor;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (graphic == null)
                {
                    return;
                }

                Color next = graphic.color;
                next.a = Mathf.Clamp01(Mathf.LerpUnclamped(from, to, e));
                graphic.color = next;
            });
        }
    }
}
