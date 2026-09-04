using System.Collections.Generic;

namespace Juahn.UiMotion.Tests
{
    /// <summary>테스트가 실행 순서를 확인하는 기록장.</summary>
    public sealed class ExecutionLog
    {
        private readonly List<string> _entries = new List<string>();

        public IReadOnlyList<string> Entries => _entries;

        public void Add(string entry)
        {
            _entries.Add(entry);
        }

        public override string ToString()
        {
            return string.Join(", ", _entries);
        }
    }

    /// <summary>
    /// 시작과 종료를 기록하는 효과 노드. 지속시간이 0이면 즉시 끝난다.
    /// </summary>
    public sealed class RecordingEffect : MotionEffectNode
    {
        private readonly string _name;
        private readonly float _duration;

        public ExecutionLog Log;

        /// <summary>이 노드가 취소될 때 되돌릴 것을 등록할지.</summary>
        public bool RegisterRevert;

        public RecordingEffect(string name, float duration = 0f)
        {
            _name = name;
            _duration = duration;
        }

        public override bool Reverts => RegisterRevert;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (Log != null)
            {
                Log.Add(_name + ":start");
            }

            if (RegisterRevert && ctx != null && ctx.Scope != null)
            {
                ExecutionLog captured = Log;
                string revertName = _name;
                ctx.Scope.Remember(delegate
                {
                    if (captured != null)
                    {
                        captured.Add(revertName + ":revert");
                    }
                });
            }

            if (_duration <= 0f)
            {
                if (Log != null)
                {
                    Log.Add(_name + ":end");
                }

                return MotionHandle.Completed;
            }

            ExecutionLog logRef = Log;
            string nodeName = _name;
            return MotionHandle.FromTimer(_duration, delegate(float t)
            {
                if (t >= 1f && logRef != null)
                {
                    logRef.Add(nodeName + ":end");
                }
            });
        }
    }

    /// <summary>슬롯이 비어 있으면 건너뛰는 노드. 미할당 슬롯 처리를 확인할 때 쓴다.</summary>
    public sealed class SlotDependentEffect : MotionEffectNode
    {
        public SlotRef Target = SlotRef.Self;
        public ExecutionLog Log;
        public string Name = "slotted";

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            object resolved = ctx.ResolveSlot(Target);
            if (resolved == null)
            {
                if (ctx.Log != null)
                {
                    ctx.Log.Warn("slot not bound: " + Target.Name);
                }

                return MotionHandle.Skipped;
            }

            if (Log != null)
            {
                Log.Add(Name + ":start");
            }

            return MotionHandle.Completed;
        }
    }
}
