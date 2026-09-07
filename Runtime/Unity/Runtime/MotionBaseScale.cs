using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 대상의 <b>제자리 크기</b>를 기억한다.
    ///
    /// <b>왜 필요한가</b> — 버튼 연출은 두 트리거로 나뉜다. 누를 때 0.95배로 줄이고, 뗄 때
    /// 1.15배로 튀었다 1배로 돌아온다. 그런데 뗌이 시작될 때 대상의 크기는 이미 0.95배다.
    /// "지금 크기의 1.15배"로 계산하면 1.0925배가 되어 튀는 세기가 매번 달라지고, 빠르게
    /// 연타하면 크기가 조금씩 흘러내려 버튼이 쪼그라든 채 남는다. 조용히 일어나고,
    /// 원인을 찾을 단서가 없다.
    ///
    /// 그래서 기준이 되는 크기를 따로 들고 그 대비로 계산한다. IdlePaori의
    /// <c>UIButtonBounceModule</c>이 <c>Awake</c>에서 <c>_baseScale</c>을 굽는 것과 같은 목적이다.
    ///
    /// <b>언제 굽는가</b> — 처음 물어볼 때다. 컴포넌트 시작 시점에 구우려면 어떤 대상이
    /// 쓰일지 미리 알아야 하는데, 슬롯은 실행 중에 바뀔 수 있다.
    ///
    /// <b>언제 잊는가</b> — <see cref="Forget"/>을 부를 때다. <see cref="MotionPlayer"/>가
    /// 활성화될 때마다 잊는다. 풀에서 꺼내 다시 쓰는 오브젝트가 지난번 연출 도중의
    /// 크기를 제자리 크기로 굽지 않게 하기 위해서다.
    ///
    /// <b>한 가지 함정</b> — 연출이 이미 대상의 크기를 바꿔 놓은 뒤에 처음 물으면 그 크기가
    /// 기준이 된다. 등장 팝처럼 크기를 0으로 찍는 연출과 같은 대상에서 섞어 쓰지 말 것.
    /// 그 조합이 필요하면 등장은 등장대로 끝낸 뒤 버튼 연출을 시작해야 한다.
    /// </summary>
    public static class MotionBaseScale
    {
        // 대상별 기준 크기. 키는 Transform이고, 파괴된 것은 Forget이나 다음 청소에서 빠진다.
        private static readonly Dictionary<Transform, Vector3> Cache = new Dictionary<Transform, Vector3>();

        /// <summary>
        /// 이 대상의 제자리 크기. 아직 모르면 지금 크기를 그것으로 굽는다.
        /// 대상이 없으면 <see cref="Vector3.one"/>.
        /// </summary>
        public static Vector3 Of(Transform target)
        {
            if (target == null)
            {
                return Vector3.one;
            }

            Vector3 known;
            if (Cache.TryGetValue(target, out known))
            {
                return known;
            }

            Vector3 current = target.localScale;
            Cache[target] = current;
            return current;
        }

        /// <summary>이 대상에 대해 기억한 것을 버린다. 다음에 물으면 그때 크기를 다시 굽는다.</summary>
        public static void Forget(Transform target)
        {
            if (target != null)
            {
                Cache.Remove(target);
            }
        }

        /// <summary>
        /// 이 계층 아래의 기억을 전부 버린다. <see cref="MotionPlayer"/>가 활성화될 때 부른다.
        ///
        /// <b>파괴된 대상도 함께 치운다.</b> 사전의 키는 <c>Transform</c>이라 씬이 바뀌어
        /// 대상이 사라져도 항목이 남는다. 그대로 두면 씬을 오갈수록 사전이 커진다 —
        /// 새는 양이 작아 눈에 띄지 않고, 그래서 더 오래 남는다.
        /// </summary>
        public static void ForgetUnder(Transform root)
        {
            if (Cache.Count == 0)
            {
                return;
            }

            List<Transform> drop = null;

            foreach (KeyValuePair<Transform, Vector3> pair in Cache)
            {
                Transform key = pair.Key;

                // Unity의 "가짜 null" — 파괴된 오브젝트는 C#의 null이 아니지만 == null은 참이다.
                bool dead = key == null;
                bool mine = !dead && root != null && key.IsChildOf(root);

                if (!dead && !mine)
                {
                    continue;
                }

                if (drop == null)
                {
                    drop = new List<Transform>();
                }

                drop.Add(key);
            }

            if (drop == null)
            {
                return;
            }

            for (int i = 0; i < drop.Count; i++)
            {
                Cache.Remove(drop[i]);
            }
        }

        /// <summary>전부 잊는다. 도메인 리로드와 테스트가 쓴다.</summary>
        public static void Clear()
        {
            Cache.Clear();
        }
    }
}
