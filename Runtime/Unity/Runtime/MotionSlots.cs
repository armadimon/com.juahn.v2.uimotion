using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯을 노드가 원하는 타입으로 바꾼다.
    ///
    /// 코어의 <see cref="ISlotResolver"/>는 <c>object</c>만 돌려준다 — 코어가 Unity 타입을
    /// 모르기 위해서다. 그 <c>object</c>를 <c>RectTransform</c>이나 <c>CanvasGroup</c>으로
    /// 바꾸는 일이 여기서 일어난다.
    ///
    /// 바인딩된 것이 컴포넌트든 게임오브젝트든 <c>GetComponent</c>로 찾아 준다. 인스펙터에
    /// 무엇을 끌어다 놓든 의도대로 동작하게 하기 위해서다.
    /// </summary>
    public static class MotionSlots
    {
        /// <summary>
        /// 해석에 실패하면 null을 돌려주고 <b>인스턴스당 한 번만</b> 경고한다.
        /// 방치형 게임에서 매 프레임 경고가 나오면 진짜 문제를 찾을 수 없다.
        /// </summary>
        public static T Resolve<T>(IMotionContext ctx, SlotRef slot) where T : class
        {
            if (ctx == null)
            {
                return null;
            }

            object raw = ctx.ResolveSlot(slot);
            if (raw == null)
            {
                WarnUnbound(ctx, slot, typeof(T), "is not bound");
                return null;
            }

            T coerced = Coerce<T>(raw);
            if (coerced == null)
            {
                WarnUnbound(ctx, slot, typeof(T), "is bound to " + raw.GetType().Name + " which has no");
            }

            return coerced;
        }

        private static T Coerce<T>(object raw) where T : class
        {
            var direct = raw as T;
            if (direct != null)
            {
                return direct;
            }

            var component = raw as Component;
            GameObject go = component != null ? component.gameObject : raw as GameObject;

            if (go == null)
            {
                return null;
            }

            // GameObject 자체를 원하는 노드(SetActive 등)도 있다.
            var wanted = go as T;
            if (wanted != null)
            {
                return wanted;
            }

            return go.GetComponent(typeof(T)) as T;
        }

        private static void WarnUnbound(IMotionContext ctx, SlotRef slot, System.Type wanted, string reason)
        {
            string graphName = ctx.Graph == null ? "<no graph>" : ctx.Graph.GraphName;
            string key = "slot:" + graphName + ":" + slot.Name + ":" + wanted.Name;

            MotionLogs.WarnOnce(ctx.Log, key,
                "graph '" + graphName + "': slot '" + slot + "' " + reason + " " + wanted.Name +
                "; the node was skipped");
        }
    }
}
