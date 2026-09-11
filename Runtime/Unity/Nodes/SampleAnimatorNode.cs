using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [Serializable, MotionNode(Name = "Sample Animator", Category = "Object", Summary = "MotionValue 진행값으로 Animator를 샘플링한다. 단계 시간은 게임이 소유한다.")]
    public sealed class SampleAnimatorNode : UnityEffectNode
    {
        [MotionSlot(typeof(Animator))] public SlotRef Target = SlotRef.Self;
        [MotionSlot(typeof(MotionValue))] public SlotRef Progress = new SlotRef("Progress");
        public string State;
        public int Layer;
        public override bool BlocksChildren => true;
        public override bool Reverts => true;
        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            var animator = Resolve<Animator>(ctx, Target); var value = Resolve<MotionValue>(ctx, Progress);
            if (animator == null || value == null || string.IsNullOrEmpty(State)) return MotionHandle.Skipped;
            var previous = animator.GetCurrentAnimatorStateInfo(Layer); var speed = animator.speed;
            Remember(ctx, () =>
            {
                if (animator == null) return;
                if (previous.fullPathHash != 0) { animator.Play(previous.fullPathHash, Layer, previous.normalizedTime); animator.Update(0f); }
                animator.speed = speed;
            });
            animator.speed = 0f;
            return new SampleHandle(animator, value, Animator.StringToHash(State), Layer);
        }
        private sealed class SampleHandle : IMotionHandle
        {
            private readonly Animator _animator;
            private readonly MotionValue _value;
            private readonly int _state, _layer;
            public bool IsDone { get; private set; }
            public SampleHandle(Animator animator, MotionValue value, int state, int layer)
            { _animator = animator; _value = value; _state = state; _layer = layer; Tick(0f); }
            public void Tick(float deltaSeconds)
            {
                if (IsDone) return;
                if (_animator == null || _value == null) { IsDone = true; return; }
                _animator.Play(_state, _layer, Mathf.Clamp01(_value.Value)); _animator.Update(0f);
            }
            public void Cancel() => IsDone = true;
        }
    }
}
