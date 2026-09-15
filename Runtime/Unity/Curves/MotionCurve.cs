using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 연출 노드가 받는 <see cref="AnimationCurve"/>의 공통 평가 규약 —
    /// <b>가로축은 언제나 0~1로 정규화된다.</b>
    ///
    /// <b>왜 필요한가</b> — 인스펙터에서 곡선을 그리다 보면 키가 0.2~0.7처럼 일부 구간에만
    /// 놓이거나 1을 넘어가기 쉽다. 곡선의 시간을 그대로 진행도로 쓰면 그 바깥은 첫/마지막
    /// 값에 <b>멈춰 있는 시간</b>이 되어, 저작한 모양은 그대로인데 재생이 앞뒤로 끊긴 듯
    /// 보인다. 여기서는 첫 키~마지막 키 구간을 0~1에 펴서 평가하므로 키를 어느 범위에
    /// 그렸든 지정한 시간 전체가 곡선의 모양으로 채워진다.
    ///
    /// 세로축의 뜻(배율·알파·각도)은 노드마다 다르고 여기서는 건드리지 않는다.
    ///
    /// <b>할당이 없다.</b> <c>curve.keys</c>는 배열을 통째로 복사하므로 쓰지 않는다.
    /// 인덱서와 <c>length</c>만 쓴다 — 여기는 반복 연출에서 매 프레임 불리는 경로다.
    ///
    /// 원본 프로젝트의 <c>UIAnimCurve</c>에서 옮겨 왔다.
    /// </summary>
    public static class MotionCurve
    {
        /// <summary>
        /// 진행도(0~1)를 곡선의 첫 키~마지막 키 구간에 대응시켜 평가한다.
        /// 키가 하나 이하면 펼 구간이 없으므로 진행도를 그대로 평가한다(상수 곡선).
        /// </summary>
        public static float EvaluateNormalized(this AnimationCurve curve, float progress)
        {
            // 키가 없는 곡선은 1로 읽는다. Evaluate가 0을 돌려주기 때문이다 —
            // 배율로 쓰는 자리에서 0은 <b>대상이 사라지는 것</b>이고, 곡선을 저작하지
            // 않았을 뿐인데 오브젝트가 없어지면 무엇이 잘못됐는지 알 방법이 없다.
            // 저작하지 않은 곡선은 아무 일도 하지 않아야 한다.
            if (curve == null || curve.length == 0)
            {
                return 1f;
            }

            int count = curve.length;
            if (count < 2)
            {
                return curve.Evaluate(progress);
            }

            float first = curve[0].time;
            float last = curve[count - 1].time;
            return curve.Evaluate(first + (last - first) * progress);
        }
    }
}
