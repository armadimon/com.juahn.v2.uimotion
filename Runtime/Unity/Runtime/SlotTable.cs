using System;
using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯 이름을 실제 오브젝트로 바꾼다. 플레이어당 하나 만든다.
    /// </summary>
    public sealed class SlotTable : ISlotResolver
    {
        private readonly Transform _self;
        private readonly Dictionary<string, UnityEngine.Object> _bindings =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

        public SlotTable(Transform self, IReadOnlyList<SlotBinding> bindings)
        {
            _self = self;

            if (bindings == null)
            {
                return;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                SlotBinding binding = bindings[i];
                if (string.IsNullOrEmpty(binding.Name))
                {
                    continue;
                }

                _bindings[binding.Name] = binding.Target;
            }
        }

        public object Resolve(SlotRef slot)
        {
            if (!slot.IsValid)
            {
                return null;
            }

            if (slot.IsSelf)
            {
                return _self == null ? null : (object)_self;
            }

            UnityEngine.Object target;
            if (!_bindings.TryGetValue(slot.Name, out target))
            {
                return null;
            }

            // Unity의 "가짜 null"을 여기서 진짜 null로 바꾼다. 그냥 돌려주면 파괴된
            // 오브젝트가 null이 아닌 참조로 나가 노드가 죽은 대상을 만진다.
            return target == null ? null : (object)target;
        }
    }
}
