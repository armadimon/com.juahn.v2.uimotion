using System;
using System.Collections.Generic;
using System.Reflection;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드들이 참조하는 슬롯 이름을 모아 그래프의 슬롯 선언 목록을 만든다.
    ///
    /// <b>슬롯 목록은 저작값이 아니라 파생값이다.</b> 사람이 인스펙터에서 슬롯을 추가하거나
    /// 타입을 고르지 않는다. 노드를 지우면 그 슬롯도 목록에서 사라진다.
    ///
    /// 이 구분이 중요한 이유는 <see cref="SlotDeclaration.RequiredType"/>이
    /// <see cref="Type"/>이라 Unity가 직렬화할 수 없기 때문이다. 파생값이므로 저장할 필요가
    /// 없고 도메인 리로드 후 다시 계산하면 된다. 반대로 이걸 사람이 고르는 저장값으로
    /// 설계하면 에디터를 다시 열 때마다 타입이 null로 리셋되는 버그가 된다.
    ///
    /// 저장되는 것은 슬롯 이름 -> 오브젝트 바인딩뿐이고, 그건 그래프가 아니라
    /// 프리팹의 플레이어가 갖는다.
    /// </summary>
    public static class SlotIntrospector
    {
        private static readonly FieldInfo[] NoFields = new FieldInfo[0];

        // 리플렉션은 비싸다. 노드 타입은 유한하고 변하지 않으므로 타입당 한 번만 훑는다.
        private static readonly Dictionary<Type, FieldInfo[]> Cache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// <paramref name="nodes"/>가 참조하는 슬롯들을 <paramref name="into"/>에 채운다.
        /// <paramref name="into"/>는 먼저 비운다.
        ///
        /// 규칙:
        /// <list type="bullet">
        /// <item>null 노드(결손 노드)는 건너뛴다</item>
        /// <item><see cref="SlotRef.SelfName"/>은 예약이라 목록에 넣지 않는다</item>
        /// <item>이름이 빈 슬롯은 아직 배선되지 않은 것이므로 건너뛴다</item>
        /// <item>같은 이름이 여러 번 나오면 하나로 합치고 <b>먼저 나온 타입이 이긴다</b></item>
        /// </list>
        ///
        /// 순서는 노드 배열 순서, 그 안에서는 베이스 클래스 필드가 먼저다.
        /// 인스펙터의 슬롯 목록이 리로드마다 뒤바뀌지 않으려면 결정적이어야 한다.
        /// </summary>
        public static void Collect(IReadOnlyList<MotionNodeBase> nodes, List<SlotDeclaration> into)
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
                MotionNodeBase node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                FieldInfo[] fields = GetSlotFields(node.GetType());

                for (int f = 0; f < fields.Length; f++)
                {
                    var slot = (SlotRef)fields[f].GetValue(node);

                    if (!slot.IsValid || slot.IsSelf)
                    {
                        continue;
                    }

                    if (!seen.Add(slot.Name))
                    {
                        continue;
                    }

                    into.Add(new SlotDeclaration(slot.Name, ReadRequiredType(fields[f])));
                }
            }
        }

        /// <summary>새 목록을 만들어 채운다. 편의용.</summary>
        public static List<SlotDeclaration> Collect(IReadOnlyList<MotionNodeBase> nodes)
        {
            var into = new List<SlotDeclaration>();
            Collect(nodes, into);
            return into;
        }

        /// <summary>
        /// 노드 타입의 <see cref="SlotRef"/> 필드들. 베이스 클래스가 먼저, 그 안에서는 선언 순서다.
        ///
        /// 에디터가 슬롯 이름을 다시 쓸 때도 이것을 쓴다.
        ///
        /// <b>public 인스턴스 필드만 본다</b> — Unity가 직렬화하는 것이 그것뿐이기 때문이다.
        /// </summary>
        public static FieldInfo[] GetSlotFields(Type nodeType)
        {
            if (nodeType == null)
            {
                return NoFields;
            }

            lock (Cache)
            {
                FieldInfo[] cached;
                if (Cache.TryGetValue(nodeType, out cached))
                {
                    return cached;
                }

                FieldInfo[] built = BuildSlotFields(nodeType);
                Cache[nodeType] = built;
                return built;
            }
        }

        private static FieldInfo[] BuildSlotFields(Type nodeType)
        {
            // GetFields는 상속 계층의 순서를 보장하지 않는다. 계층을 직접 걸어
            // 베이스부터 훑어야 결과가 결정적이다.
            var chain = new List<Type>();
            for (Type t = nodeType; t != null && t != typeof(object); t = t.BaseType)
            {
                chain.Add(t);
            }

            var fields = new List<FieldInfo>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                FieldInfo[] declared = chain[i].GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                for (int j = 0; j < declared.Length; j++)
                {
                    if (declared[j].FieldType == typeof(SlotRef))
                    {
                        fields.Add(declared[j]);
                    }
                }
            }

            return fields.Count == 0 ? NoFields : fields.ToArray();
        }

        private static Type ReadRequiredType(FieldInfo field)
        {
            var attribute = (MotionSlotAttribute)Attribute.GetCustomAttribute(field, typeof(MotionSlotAttribute));
            return attribute == null ? null : attribute.RequiredType;
        }
    }
}
