using System.Collections.Generic;

namespace Juahn.UiMotion.Tests
{
    /// <summary>테스트용 로그 싱크. 메시지를 모아 두고 개수를 센다.</summary>
    public sealed class FakeLog : IMotionLog
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _errors = new List<string>();

        public IReadOnlyList<string> Warnings => _warnings;

        public IReadOnlyList<string> Errors => _errors;

        public void Warn(string message)
        {
            _warnings.Add(message);
        }

        public void Error(string message)
        {
            _errors.Add(message);
        }
    }

    /// <summary>테스트용 슬롯 해석기. 이름을 등록해 두면 그 이름만 해석된다.</summary>
    public sealed class FakeSlotResolver : ISlotResolver
    {
        private readonly Dictionary<string, object> _bindings = new Dictionary<string, object>();

        public void Bind(string slotName, object target)
        {
            _bindings[slotName] = target;
        }

        public object Resolve(SlotRef slot)
        {
            if (!slot.IsValid)
            {
                return null;
            }

            object target;
            return _bindings.TryGetValue(slot.Name, out target) ? target : null;
        }
    }
}
