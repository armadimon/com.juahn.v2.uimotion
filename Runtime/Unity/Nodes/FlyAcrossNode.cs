using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Fly Across",
        Category = "Transform",
        Summary = "대상을 한 지점에서 다른 지점으로 곡선을 그리며 날린다. 획득한 재화가 상단 카운터로 빨려 들어가는 연출에 쓴다.",
        Sample = "FlyAcross")]
    [Serializable]
    public sealed class FlyAcrossNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionSlot(typeof(RectTransform))]
        [MotionParam(Label = "출발", Tooltip = "비워 두면 대상의 지금 위치에서 출발한다.")]
        public SlotRef From;

        [MotionSlot(typeof(RectTransform))]
        [MotionParam(Label = "도착")]
        public SlotRef To;

        [MotionParam(Label = "호 높이", Tooltip = "직선에서 얼마나 부풀릴지(월드 단위). 0이면 직선이다.")]
        public float Arc = 80f;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.5f;

        public EaseKind Ease = EaseKind.InOutCubic;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            RectTransform destination = Resolve<RectTransform>(ctx, To);
            if (destination == null)
            {
                // 도착지가 없으면 날 곳이 없다. 슬롯 해석이 이미 경고를 한 번 남겼다.
                return MotionHandle.Skipped;
            }

            // 캔버스가 서로 다를 수 있으므로 월드 좌표로 계산한다. anchoredPosition은
            // 부모가 다르면 비교할 수 없다.
            //
            // '출발'은 비워 두는 것이 정상 사용이라 비었을 때는 아무 말도 하지 않는다.
            // 배선했는데 해석에 실패한 경우만 알리되, 그때도 노드는 건너뛰지 않고 대상의
            // 지금 위치에서 출발하므로 공용 슬롯 경고를 쓰지 않는다 — 그 문구는
            // "the node was skipped"라고 단정한다.
            RectTransform origin = null;
            if (From.IsValid && !TryResolve(ctx, From, out origin))
            {
                WarnFromFallback(ctx);
            }

            Vector3 startPosition = target.position;
            Vector3 fromPosition = origin != null ? origin.position : startPosition;
            Vector3 toPosition = destination.position;
            float arc = Arc;

            // 복구는 로컬값으로 한다. 비행 중에 부모가 움직이면(스크롤 리스트) 저장해 둔
            // 월드 좌표는 더 이상 원래 자리가 아니다. 나머지 트랜스폼 노드도 전부 로컬값을
            // 되돌린다. 트윈이 월드 position을 쓰는 것은 출발지와 도착지의 캔버스가 다를 수
            // 있어서이고, 그것과는 별개다.
            Vector3 restoreLocalPosition = target.localPosition;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localPosition = restoreLocalPosition;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target == null)
                {
                    return;
                }

                Vector3 straight = Vector3.LerpUnclamped(fromPosition, toPosition, e);

                // sin(pi * e)는 양 끝에서 정확히 0이라 출발점과 도착점을 어긋나게 하지 않는다.
                straight.y += arc * Mathf.Sin(Mathf.Clamp01(e) * Mathf.PI);

                target.position = straight;
            });
        }

        /// <summary>
        /// 배선된 '출발'이 해석되지 않았을 때 한 번만 알린다. 노드는 계속 돈다 —
        /// 대상의 지금 위치에서 출발한다.
        /// </summary>
        private void WarnFromFallback(IMotionContext ctx)
        {
            if (ctx == null)
            {
                return;
            }

            string graphName = ctx.Graph == null ? "<no graph>" : ctx.Graph.GraphName;

            MotionLogs.WarnOnce(ctx.Log, "flyacross:" + graphName + ":" + Id.Value + ":from",
                "graph '" + graphName + "' node " + Id + ": slot '" + From +
                "' did not resolve to a RectTransform; the flight starts from the target's current position");
        }
    }
}
