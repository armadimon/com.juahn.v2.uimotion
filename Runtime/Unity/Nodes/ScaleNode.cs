using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 대상의 크기를 <b>저작한 곡선대로</b> 민다. <c>localScale</c>을 만지는 연출은 이 노드 하나로 낸다.
    ///
    /// <b>곡선 하나가 여러 쓰임을 다 낸다 — 노드를 쪼개지 않는 이유</b>
    /// <list type="bullet">
    /// <item>바운스: 1 → 1.15 → 1로 다녀오는 곡선. 재생만으로 제자리에 돌아온다.</item>
    /// <item>누름: 1 → 0.95로 내려가 <b>끝값에 머무는</b> 곡선.</item>
    /// <item>펄스: 1 → 1.06 → 1 곡선을 반복.</item>
    /// <item>등장 팝·슬램: 0이나 2.2에서 시작해 1로 오는 곡선.</item>
    /// </list>
    /// 이것들을 별개 노드로 두면 같은 <c>localScale</c>을 여럿이 동시에 만지게 되어 서로의
    /// 트윈을 덮어쓴다. <b>한 프로퍼티는 한 노드가 소유한다</b> — 그것이 노드를 조합해
    /// 쓰는 구조가 성립하는 조건이다.
    ///
    /// <b>값 하나가 아니라 곡선으로 받는 이유</b> — "0.95까지 줄어든다"로는 <b>모양</b>을
    /// 정할 수 없다. 툭 눌리는 것과 물렁하게 눌리는 것, 한 번 부풀었다 가라앉는 것은
    /// 목표값이 같아도 완전히 다른 연출이다. 세로축이 <b>제자리 크기 대비 배율</b>이고
    /// 가로축은 <see cref="Duration"/>에 대한 진행도다.
    ///
    /// <b>이징을 따로 두지 않는다.</b> 곡선이 이징까지 겸한다. 밖에서 또 휘면 저작한
    /// 모양이 나오지 않는다.
    ///
    /// <b>기준은 지금 크기가 아니라 제자리 크기다</b>(<see cref="MotionBaseScale"/>).
    /// 지금 크기를 기준으로 삼으면 연출 도중에 다시 발사됐을 때 중간값이 기준이 되어
    /// 재생할수록 크기가 흘러내린다. 조용히 일어나고 원인을 찾을 단서가 없다.
    ///
    /// <b>레이캐스트를 받는 대상을 스케일하지 말 것.</b> 축소되는 순간 눌림 판정 영역도
    /// 함께 줄어들어, 테두리를 눌렀다 떼면 포인터가 영역 밖이라 클릭이 무효화된다.
    /// 시각 전용 자식을 지정한다.
    ///
    /// IdlePaori의 <c>UIScaleModule</c>에서 옮겨 왔다.
    /// </summary>
    [MotionNode(
        Name = "Scale",
        Category = "Transform",
        Summary = "크기를 저작한 곡선대로 민다. 세로축은 제자리 크기 대비 배율이고 가로축은 진행도다.",
        Sample = "Scale")]
    [Serializable]
    public sealed class ScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        [MotionParam(Tooltip = "레이캐스트를 받지 않는 시각 전용 자식을 지정할 것. 판정 영역을 스케일하면 클릭이 무효화된다.")]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(
            Label = "배율 곡선",
            Tooltip = "가로축은 진행도 — 첫 키부터 마지막 키까지가 재생 시간 전체에 펴진다. " +
                      "세로축은 제자리 크기 대비 배율이다. 1로 끝나게 그리면 재생만으로 제자리에 " +
                      "돌아오고, 다른 값으로 끝내면 그 배율에 머문다. 반복으로 쓸 때는 시작과 끝 " +
                      "배율을 같게 그려야 이어지는 지점이 튀지 않는다.")]
        public AnimationCurve Curve = MotionScaleCurves.ButtonBounce();

        [MotionParam(Label = "시간", Tooltip = "곡선을 한 번 훑는 시간(초).", Min = 0f, Max = 5f)]
        public float Duration = 0.27f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 baseScale = MotionBaseScale.Of(target);
            AnimationCurve curve = Curve;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = baseScale;
                }
            });

            // 이징은 곡선이 담당한다. 여기서 또 휘면 저작한 모양이 나오지 않는다.
            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target != null)
                {
                    target.localScale = baseScale * curve.EvaluateNormalized(t);
                }
            });
        }
    }
}
