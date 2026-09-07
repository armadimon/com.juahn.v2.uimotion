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

            // 키에 노드 id를 넣는 이유는 MotionSlots와 같다 — 한 그래프의 Fade 두 개가
            // 둘 다 Self를 가리키면 이름만으로는 키가 겹쳐 한쪽 경고가 사라진다.
            MotionLogs.WarnOnce(ctx.Log, "fade:" + graphName + ":" + Id.Value + ":" + Target.Name,
                "graph '" + graphName + "' node " + Id + ": slot '" + Target + "' resolved to '" +
                target.name + "' which has neither CanvasGroup nor Graphic; the node was skipped");
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

        /// <summary>
        /// <see cref="CanvasGroup"/>이 없어 <see cref="Graphic"/>의 알파로 떨어졌다.
        /// 그 대상 아래에 다른 <see cref="Graphic"/>이 더 있으면 알린다.
        ///
        /// <b>왜 이 조건에서만 알리는가</b> — 두 가지가 동시에 걸리기 때문이다.
        ///
        /// 하나는 <b>보이는 결과가 다르다</b>. 이 노드는 넘겨받은 Graphic 하나만 만진다.
        /// 아이콘과 글자를 거느린 패널을 페이드하면 패널 배경만 흐려지고 그 위의 것들은
        /// 또렷하게 남는다. 저작자는 "패널을 페이드했다"고 생각하는데 그렇지 않다.
        ///
        /// 다른 하나는 <b>훨씬 비싸다</b>. <c>Graphic.color</c>는 그 Graphic의 메시를 다시
        /// 만들게 하고 그 비용은 바꾼 개수에 비례한다. 측정값은 120칸 캔버스에서 30개를
        /// 바꿀 때 프레임당 98us로, 같은 수를 <c>CanvasGroup.alpha</c>로 처리한 9.85us의
        /// 여덟 배다(docs/ugui-cost.md).
        ///
        /// <b>Graphic 하나짜리에는 알리지 않는다.</b> 아이콘 하나를 페이드하는 것은 정당한
        /// 쓰임이고 비용도 트랜스폼을 움직이는 것과 같다. 거기까지 경고하면 경고가
        /// 무시되기 시작한다.
        /// </summary>
        private void WarnGraphicFallback(IMotionContext ctx, Graphic graphic)
        {
            if (!HasOtherGraphic(graphic.transform, graphic))
            {
                return;
            }

            string graphName = ctx.Graph == null ? "<no graph>" : ctx.Graph.GraphName;

            MotionLogs.WarnOnce(ctx.Log, "fade-fallback:" + graphName + ":" + Id.Value,
                "graph '" + graphName + "' node " + Id + ": '" + graphic.name +
                "' has no CanvasGroup, so only its own Graphic fades - children stay opaque. " +
                "Adding a CanvasGroup fades the whole subtree and is far cheaper (see docs/ugui-cost.md).");
        }

        /// <summary>
        /// 이 계층 아래에 <paramref name="self"/> 말고 다른 <see cref="Graphic"/>이 있는가.
        ///
        /// <c>GetComponentsInChildren</c>을 쓰지 않는 이유는 배열을 할당하기 때문이다.
        /// 여기는 발사할 때마다 지나가는 자리다 — 버튼 하나가 초당 몇 번씩 눌린다.
        /// </summary>
        private static bool HasOtherGraphic(Transform node, Graphic self)
        {
            int count = node.childCount;

            for (int i = 0; i < count; i++)
            {
                Transform child = node.GetChild(i);

                var found = child.GetComponent<Graphic>();
                if (found != null && found != self)
                {
                    return true;
                }

                if (HasOtherGraphic(child, self))
                {
                    return true;
                }
            }

            return false;
        }

        private IMotionHandle FadeGraphic(IMotionContext ctx, Graphic graphic)
        {
            WarnGraphicFallback(ctx, graphic);

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
