using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>버튼 연출의 어느 쪽인가.</summary>
    public enum ButtonBouncePhase
    {
        /// <summary>누르는 순간 — 살짝 줄어들어 그 크기로 머문다.</summary>
        Press = 0,

        /// <summary>떼는 순간 — 제자리보다 크게 튀었다 돌아온다.</summary>
        Release = 1,
    }

    /// <summary>
    /// 버튼의 "뽀잉" — 누를 때 줄고, 뗄 때 넘쳤다 돌아온다.
    ///
    /// 두 트리거로 나뉜다. <c>Press</c> 트리거에 <see cref="ButtonBouncePhase.Press"/> 노드를,
    /// <c>Release</c> 트리거에 <see cref="ButtonBouncePhase.Release"/> 노드를 잇는다.
    ///
    /// <b>왜 <see cref="ScaleNode"/> 둘로 하지 않는가</b> — 뗌이 시작될 때 대상은 이미
    /// 0.95배다. "지금 크기의 1.15배"는 1.0925배가 되어 튀는 세기가 눌린 정도에 따라
    /// 달라지고, 연타하면 크기가 조금씩 흘러내린다. 이 노드는 <see cref="MotionBaseScale"/>이
    /// 기억한 제자리 크기를 기준으로 계산하므로 몇 번을 눌러도 같은 연출이 나온다.
    ///
    /// <b>눌림 판정 영역을 스케일하지 말 것.</b> 버튼의 레이캐스트 대상을 줄이면 축소되는
    /// 순간 눌림 영역도 함께 줄어든다. 그러면 손가락이 가만히 있어도 포인터가 영역 밖으로
    /// 나간 것이 되어 클릭이 무효화된다. <see cref="Target"/>에는 레이캐스트를 받지 않는
    /// 시각 전용 자식을 지정한다.
    ///
    /// 수치는 IdlePaori의 <c>UIButtonBounceModule</c>에서 옮겨 왔다.
    /// </summary>
    [MotionNode(
        Name = "Button Bounce",
        Category = "Transform",
        Summary = "버튼을 누를 때 줄고 뗄 때 넘쳤다 돌아온다. 제자리 크기를 기준으로 재므로 연타해도 흘러내리지 않는다.",
        Sample = "ButtonBounce")]
    [Serializable]
    public sealed class ButtonBounceNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        [MotionParam(Tooltip = "레이캐스트를 받지 않는 시각 전용 자식을 지정할 것. 판정 영역을 스케일하면 클릭이 무효화된다.")]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "단계", Tooltip = "누름은 Press 트리거에, 뗌은 Release 트리거에 잇는다.")]
        public ButtonBouncePhase Phase = ButtonBouncePhase.Press;

        [MotionParam(Label = "눌린 배율", Min = 0.5f, Max = 1f)]
        public float PressedScale = 0.95f;

        [MotionParam(Label = "누름 시간", Min = 0f, Max = 1f)]
        public float PressDuration = 0.085f;

        [MotionParam(Label = "튀는 배율", Min = 1f, Max = 2f)]
        public float OvershootScale = 1.15f;

        [MotionParam(Label = "튀는 시간", Min = 0f, Max = 1f)]
        public float OvershootDuration = 0.1f;

        [MotionParam(Label = "돌아오는 시간", Min = 0f, Max = 1f)]
        public float SettleDuration = 0.085f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 baseScale = MotionBaseScale.Of(target);

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = baseScale;
                }
            });

            return Phase == ButtonBouncePhase.Press
                ? PlayPress(ctx, target, baseScale)
                : PlayRelease(ctx, target, baseScale);
        }

        /// <summary>
        /// 누름 — 지금 크기에서 눌린 크기로. 끝나도 되돌리지 않는다.
        /// 뗄 때까지 눌린 채로 있어야 손가락이 닿아 있다는 것이 보인다.
        /// </summary>
        private IMotionHandle PlayPress(IMotionContext ctx, Transform target, Vector3 baseScale)
        {
            Vector3 from = target.localScale;
            Vector3 to = baseScale * PressedScale;

            return Run(ctx, PressDuration, EaseKind.OutQuad, delegate(float e)
            {
                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(from, to, e);
                }
            });
        }

        /// <summary>
        /// 뗌 — 두 구간이다. 넘치는 크기까지 갔다가 제자리로 돌아온다.
        ///
        /// 두 트윈을 잇지 않고 하나로 도는 이유는 <b>중간에 끊겼을 때</b>다. 두 개면 첫
        /// 트윈이 끝나고 둘째가 시작되기 전에 취소될 수 있고, 그 틈에서는 대상이 1.15배인
        /// 채로 남는다. 하나면 그런 틈이 없다.
        /// </summary>
        private IMotionHandle PlayRelease(IMotionContext ctx, Transform target, Vector3 baseScale)
        {
            Vector3 from = target.localScale;
            Vector3 peak = baseScale * OvershootScale;

            float rise = OvershootDuration;
            float fall = SettleDuration;
            float total = rise + fall;

            if (total <= 0f)
            {
                target.localScale = baseScale;
                return MotionHandle.Completed;
            }

            // 두 구간의 경계. 시간이 아니라 진행도로 들고 있어야 러너가 어떤 방식으로
            // 시간을 재든 같은 자리에서 갈린다.
            float split = rise / total;

            return Run(ctx, total, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                if (t < split)
                {
                    float u = split > 0f ? t / split : 1f;
                    target.localScale = Vector3.LerpUnclamped(from, peak, EaseLibrary.Evaluate(EaseKind.OutQuad, u));
                    return;
                }

                float v = split < 1f ? (t - split) / (1f - split) : 1f;
                target.localScale = Vector3.LerpUnclamped(peak, baseScale, EaseLibrary.Evaluate(EaseKind.OutQuad, v));
            });
        }
    }
}
