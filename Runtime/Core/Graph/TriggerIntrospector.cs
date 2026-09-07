using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드들에서 트리거 선언을 계산한다.
    ///
    /// <b>트리거 목록은 저작값이 아니라 파생값이다.</b> <see cref="SlotIntrospector"/>가
    /// 슬롯에 대해 하는 일과 같다. 사람은 캔버스에 <see cref="TriggerNode"/>를 놓을 뿐이고
    /// 목록은 거기서 나온다. 노드를 지우면 트리거도 사라진다.
    ///
    /// 진입점이 노드 자신이라는 점이 핵심이다. 예전처럼 "이름과 진입 노드 id"를 따로 들면
    /// 노드를 지웠을 때 목록에 죽은 id가 남는다.
    /// </summary>
    public static class TriggerIntrospector
    {
        /// <summary>
        /// <paramref name="into"/>는 먼저 비운다.
        ///
        /// 규칙:
        /// <list type="bullet">
        /// <item>null 노드(결손 노드)와 id 없는 노드는 건너뛴다</item>
        /// <item>이름이 비었으면 건너뛰고 경고한다 — 발사할 방법이 없는 트리거다</item>
        /// <item>이름의 앞뒤 공백은 잘라낸다. 안 그러면 <c>Fire("Start")</c>가 조용히 빗나간다</item>
        /// <item>같은 이름이 둘이면 <b>먼저 나온 것이 이긴다</b>. 뒤엣것은 경고와 함께 버린다</item>
        /// </list>
        ///
        /// 순서는 노드 배열 순서다. 결정적이어야 목록이 리로드마다 흔들리지 않는다.
        /// </summary>
        public static void Collect(
            IReadOnlyList<MotionNodeBase> nodes, List<TriggerDeclaration> into, IMotionLog log)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            if (nodes == null)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < nodes.Count; i++)
            {
                var trigger = nodes[i] as TriggerNode;
                if (trigger == null || !trigger.Id.IsValid)
                {
                    continue;
                }

                string name = trigger.TriggerName == null ? null : trigger.TriggerName.Trim();

                if (string.IsNullOrEmpty(name))
                {
                    Warn(log, "trigger:noname:" + trigger.Id.Value,
                        "trigger node " + trigger.Id + " has no name; it can never be fired");
                    continue;
                }

                if (!seen.Add(name))
                {
                    Warn(log, "trigger:dup:" + name,
                        "duplicate trigger '" + name + "' at " + trigger.Id + "; the first one wins");
                    continue;
                }

                into.Add(new TriggerDeclaration(name, trigger.Id, trigger.Policy));
            }
        }

        /// <summary>새 목록을 만들어 채운다. 편의용.</summary>
        public static List<TriggerDeclaration> Collect(IReadOnlyList<MotionNodeBase> nodes, IMotionLog log)
        {
            var into = new List<TriggerDeclaration>();
            Collect(nodes, into, log);
            return into;
        }

        /// <summary>
        /// 억제되는 경고. <b>키가 다시 계산해도 같아야 한다</b> — 트리거 목록은 그래프를
        /// 편집할 때마다 새로 계산되고 로그(<c>OnceLogger</c>)는 그보다 오래 산다.
        /// 억제되지 않으면 이름 없는 트리거 하나가 <c>OnValidate</c>마다 콘솔에 다시 찍힌다.
        /// </summary>
        private static void Warn(IMotionLog log, string key, string message)
        {
            MotionLogs.WarnOnce(log, key, message);
        }
    }
}
