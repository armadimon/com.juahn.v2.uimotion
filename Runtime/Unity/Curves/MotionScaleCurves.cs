using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// <see cref="ScaleNode"/>가 쓰는 배율 곡선의 프리셋.
    ///
    /// 세로축은 <b>제자리 크기 대비 배율</b>이고 가로축은 진행도다. 곡선 하나가 세 가지
    /// 쓰임을 다 낸다 —
    ///
    /// <list type="bullet">
    /// <item>1로 끝나는 곡선은 재생만으로 제자리에 돌아온다(바운스·펄스·팝).</item>
    /// <item>다른 값으로 끝나는 곡선은 그 배율에 머문다(누름).</item>
    /// <item>반복으로 쓰려면 시작과 끝 배율이 같아야 이어지는 지점이 튀지 않는다.</item>
    /// </list>
    ///
    /// <b>값 하나가 아니라 곡선인 이유</b> — "0.95까지 줄어든다"로는 <b>모양</b>을 정할 수
    /// 없다. 툭 눌리는 것과 물렁하게 눌리는 것, 한 번 부풀었다 가라앉는 것은 목표값이
    /// 같아도 완전히 다른 연출이다.
    ///
    /// 수치는 원본 프로젝트에서 옮겨 왔다. 자세한 출처는 <c>docs/motion-source-values.md</c>.
    /// </summary>
    public static class MotionScaleCurves
    {
        /// <summary>
        /// 곡선을 샘플로 구울 때 쓰는 점 개수.
        ///
        /// 팝과 슬램은 수식으로 정의되므로 키프레임으로 근사해야 한다. 24점이면 눈으로
        /// 구분되지 않고, 인스펙터에서 손볼 수 있을 만큼은 성글다. 촘촘하게 구우면
        /// 곡선 편집기가 키 무더기가 되어 <b>고칠 수 없는 곡선</b>이 된다 — 프리셋의
        /// 목적은 출발점을 주는 것이지 못 박는 것이 아니다.
        /// </summary>
        private const int SampleCount = 24;

        /// <summary>
        /// 버튼의 뽀잉 — 눌렸다가 넘쳐 튀고 안착한다. 0.27초로 쓴다.
        ///
        /// 원본 프로젝트 <c>UIScaleModule</c>의 기본 곡선이다. <c>UIButtonBounceModule</c>이 쓰던
        /// 값(0.95로 눌림 0.085초 → 1.15로 튐 0.1초 → 1로 안착 0.085초)을 진행도로 정규화했다.
        /// 눌리는 예비 동작이 앞에 붙어 있어 그냥 커지기만 하는 것보다 탄력 있게 읽힌다.
        /// </summary>
        public static AnimationCurve ButtonBounce()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.3148f, 0.95f),
                new Keyframe(0.6852f, 1.15f),
                new Keyframe(1f, 1f));
        }

        /// <summary>
        /// 누름만 — 0.95로 내려가 <b>그 배율에 머문다.</b> 0.085초로 쓴다.
        ///
        /// 손가락이 닿아 있는 동안 눌린 채여야 하므로 1로 돌아오지 않는다.
        /// 떼는 순간은 <see cref="ButtonRelease"/>를 다른 트리거에 물린다.
        /// </summary>
        public static AnimationCurve ButtonPress()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0.95f));
        }

        /// <summary>
        /// 뗌만 — 눌린 배율에서 넘쳐 튀었다 제자리로. 0.185초로 쓴다.
        ///
        /// <see cref="ButtonPress"/>가 남긴 0.95에서 시작하므로 시작 키가 1이 아니다.
        /// 곡선은 제자리 크기 대비 배율이라, 어디서 시작하든 저작한 값이 그대로 나온다 —
        /// "지금 크기의 1.15배"로 재는 것과 다르다. 그래서 연타해도 크기가 흘러내리지 않는다.
        /// </summary>
        public static AnimationCurve ButtonRelease()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.95f),
                new Keyframe(0.5405f, 1.15f),
                new Keyframe(1f, 1f));
        }

        /// <summary>
        /// 펄스 — 1에서 1.06까지 부풀었다 돌아온다. 반복으로 쓴다.
        ///
        /// 시작과 끝이 모두 1이라 되풀이해도 이어지는 지점이 튀지 않는다.
        /// 수령 대기처럼 "여기를 봐 달라"는 자리에 쓴다.
        /// </summary>
        public static AnimationCurve Pulse()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 1.06f),
                new Keyframe(1f, 1f));
        }

        /// <summary>
        /// 등장 팝 — 아무것도 없는 데서 넘쳤다 제자리로. 0.18초로 쓴다.
        ///
        /// 원본 프로젝트 뽑기 결과의 <b>평범한 칸</b>이다. 넘치는 세기 1.7은
        /// <see cref="EaseKind.OutBack"/>의 내장 상수와 사실상 같다.
        /// </summary>
        public static AnimationCurve PopIn()
        {
            return Bake(delegate(float t) { return AppearCurves.OutBack(t, 1.7f); });
        }

        /// <summary>
        /// 크게 튀는 등장 팝 — 넘치는 세기 3.2. 0.28초로 쓴다.
        ///
        /// 강조할 칸에 쓴다. 세기가 곧 무게다 — 평범한 칸과 귀한 칸을 가르는 것이 이것 하나다.
        /// </summary>
        public static AnimationCurve PopInStrong()
        {
            return Bake(delegate(float t) { return AppearCurves.OutBack(t, 3.2f); });
        }

        /// <summary>
        /// 슬램 — 1.5배에서 줄어들며 박히고 한 번 눌렸다 돌아온다. 0.28초로 쓴다.
        /// 원본 프로젝트 뽑기 결과의 2등(티어 2).
        /// </summary>
        public static AnimationCurve SlamTier2()
        {
            return Slam(1.5f);
        }

        /// <summary>
        /// 슬램 — 2.2배에서 떨어진다. 0.28초로 쓴다.
        /// 원본 프로젝트 뽑기 결과의 1등(티어 3), 가장 드문 등급.
        /// </summary>
        public static AnimationCurve SlamTier3()
        {
            return Slam(2.2f);
        }

        /// <summary>
        /// 시작 배율을 지정하는 슬램. 낙하 구간 72퍼센트, 눌림 0.12는 원본 프로젝트 값이다.
        /// </summary>
        public static AnimationCurve Slam(float startScale)
        {
            return Bake(delegate(float t) { return AppearCurves.Slam(t, startScale, 0.72f, 0.12f); });
        }

        /// <summary>
        /// 수식을 키프레임으로 굽는다.
        ///
        /// <b>접선을 직접 잡는다.</b> Unity의 자동 접선(<c>SmoothTangents</c>)은 이웃 키만
        /// 보므로 슬램처럼 한쪽으로 치우친 곡선에서 없던 출렁임을 만든다. 여기서는
        /// 원래 수식의 기울기를 그대로 접선으로 준다 — 구운 곡선이 수식과 어긋나지 않는다.
        /// </summary>
        private static AnimationCurve Bake(Func<float, float> shape)
        {
            var keys = new Keyframe[SampleCount];
            const float step = 1f / (SampleCount - 1);

            // 접선을 잴 때 쓰는 간격. 샘플 간격보다 훨씬 작아야 그 점에서의 기울기가 된다.
            const float epsilon = 1e-4f;

            for (int i = 0; i < SampleCount; i++)
            {
                float t = i * step;

                // 양 끝에서는 한쪽으로만 잰다. 나눌 때 실제로 벌린 폭을 써야 한다 —
                // 폭이 절반인데 2 * epsilon으로 나누면 끝점의 기울기가 절반으로 죽고,
                // 팝처럼 t=0에서 가파른 곡선이 눈에 띄게 뭉개진다.
                float leftT = t - epsilon < 0f ? 0f : t - epsilon;
                float rightT = t + epsilon > 1f ? 1f : t + epsilon;
                float span = rightT - leftT;

                float value = shape(t);
                float slope = span > 0f ? (shape(rightT) - shape(leftT)) / span : 0f;

                keys[i] = new Keyframe(t, value, slope, slope);
            }

            return new AnimationCurve(keys);
        }
    }
}
