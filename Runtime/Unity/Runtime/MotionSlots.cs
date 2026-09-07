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
        ///
        /// <paramref name="owner"/>는 슬롯을 요구한 노드다. 억제 키와 메시지에 들어간다 —
        /// 미배선 슬롯은 <see cref="SlotRef.Name"/>이 null이라 노드를 빼면 한 노드의 슬롯
        /// 두 개가 같은 타입을 원할 때 키가 겹쳐 한쪽 경고가 통째로 사라진다.
        /// </summary>
        public static T Resolve<T>(IMotionContext ctx, SlotRef slot, NodeId owner = default) where T : class
        {
            string reason;
            T resolved = Lookup<T>(ctx, slot, out reason);

            if (reason != null)
            {
                WarnUnbound(ctx, slot, owner, typeof(T), reason);
            }

            return resolved;
        }

        /// <summary>
        /// 경고 없이 해석한다. 해석 실패가 <b>노드를 건너뛰지 않는</b> 슬롯이 쓴다 —
        /// <c>FlyAcross</c>의 '출발'처럼 실패해도 폴백해서 계속 도는 슬롯에
        /// 공용 경고("the node was skipped")를 내면 배선이 멀쩡한데 고치러 가게 만든다.
        /// 폴백했다는 사실을 알려야 하면 노드가 자기 문구로 경고한다.
        /// </summary>
        public static bool TryResolve<T>(IMotionContext ctx, SlotRef slot, out T result) where T : class
        {
            string reason;
            result = Lookup<T>(ctx, slot, out reason);
            return result != null;
        }

        private static T Lookup<T>(IMotionContext ctx, SlotRef slot, out string reason) where T : class
        {
            reason = null;

            if (ctx == null)
            {
                return null;
            }

            object raw = ctx.ResolveSlot(slot);
            if (raw == null)
            {
                reason = "is not bound";
                return null;
            }

            T coerced = Coerce<T>(raw);
            if (coerced == null)
            {
                reason = "is bound to " + raw.GetType().Name + " which has no";
            }

            return coerced;
        }

        /// <summary>
        /// 바인딩된 것에서 원하는 타입을 얻는다. 얻을 수 없으면 <c>null</c>.
        /// 경고를 내지 않는다.
        ///
        /// <b>에디터의 "재생 전 검사"가 이것을 쓴다.</b> 같은 판정을 인스펙터가 따로
        /// 구현하면 언젠가 갈라지고, 그때 인스펙터가 "괜찮다"고 한 것이 런타임에 경고를
        /// 낸다 — 도구가 거짓말을 하는 것은 검사가 아예 없는 것보다 나쁘다.
        /// 그래서 <see cref="Coerce{T}"/>도 이것에 위임한다.
        /// </summary>
        public static object Coerce(object raw, System.Type wanted)
        {
            if (raw == null || wanted == null)
            {
                return null;
            }

            if (wanted.IsInstanceOfType(raw))
            {
                return raw;
            }

            var component = raw as Component;
            GameObject go = component != null ? component.gameObject : raw as GameObject;

            if (go == null)
            {
                return null;
            }

            // GameObject 자체를 원하는 노드(SetActive 등)도 있다.
            if (wanted.IsInstanceOfType(go))
            {
                return go;
            }

            Component found = go.GetComponent(wanted);

            // 없는 컴포넌트를 물으면 Unity의 "가짜 null"이 돌아온다. 참조 비교로는
            // null이 아니므로 여기서 진짜 null로 바꾼다.
            return found == null ? null : found;
        }

        private static T Coerce<T>(object raw) where T : class
        {
            return Coerce(raw, typeof(T)) as T;
        }

        private static void WarnUnbound(
            IMotionContext ctx, SlotRef slot, NodeId owner, System.Type wanted, string reason)
        {
            string graphName = ctx.Graph == null ? "<no graph>" : ctx.Graph.GraphName;
            string key = "slot:" + graphName + ":" + owner.Value + ":" + slot.Name + ":" + wanted.Name;

            MotionLogs.WarnOnce(ctx.Log, key,
                "graph '" + graphName + "' node " + owner + ": slot '" + slot + "' " + reason + " " +
                wanted.Name + "; the node was skipped");
        }
    }
}
