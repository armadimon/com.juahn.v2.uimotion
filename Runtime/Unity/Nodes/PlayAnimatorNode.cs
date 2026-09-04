using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Play Animator",
        Category = "Object",
        Summary = "Animator의 상태를 재생한다. 그래프로 표현하기 어려운 손으로 만든 애니메이션을 연출 중간에 끼울 때 쓴다. 취소돼도 Animator는 되돌리지 않는다.",
        Sample = "PlayAnimator")]
    [Serializable]
    public sealed class PlayAnimatorNode : UnityEffectNode
    {
        [MotionSlot(typeof(Animator))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "상태 이름")]
        public string State = "";

        [MotionParam(Label = "레이어", Min = 0f, Max = 8f)]
        public int Layer;

        [MotionParam(Label = "끝날 때까지 기다리기", Tooltip = "끄면 재생만 시키고 즉시 다음으로 넘어간다.")]
        public bool WaitForCompletion;

        [MotionParam(Label = "최대 대기", Tooltip = "이 시간이 지나면 기다리기를 포기한다. 상태 이름을 틀렸을 때 팝업이 영영 닫히지 않는 것을 막는다.", Min = 0.1f, Max = 30f)]
        public float Timeout = 5f;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Animator animator = Resolve<Animator>(ctx, Target);
            if (animator == null || string.IsNullOrEmpty(State))
            {
                return MotionHandle.Skipped;
            }

            int stateHash = Animator.StringToHash(State);
            animator.Play(stateHash, Layer, 0f);

            if (!WaitForCompletion)
            {
                return MotionHandle.Completed;
            }

            return new AnimatorWaitHandle(animator, Layer, stateHash, Mathf.Max(0.1f, Timeout));
        }

        /// <summary>
        /// 요청한 상태가 한 바퀴 돌 때까지 기다린다.
        ///
        /// <b>왜 상태 해시를 확인하는가</b> — <c>Animator.Play</c>는 다음 Animator 평가
        /// 시점까지 반영되지 않는다. 그 전에 <c>GetCurrentAnimatorStateInfo</c>를 읽으면
        /// <b>이전</b> 상태가 나오는데, 그것이 이미 끝나 있으면 <c>normalizedTime</c>이
        /// 1을 넘어 첫 틱에 완료로 판정된다. 기다리기가 통째로 건너뛰어지는 것이다.
        /// 그래서 요청한 상태에 실제로 들어간 뒤부터만 진행률을 본다.
        ///
        /// <b>왜 타임아웃이 있는가</b> — 상태 이름이 틀리면 <c>Animator.Play</c>는 조용히
        /// 아무것도 하지 않는다. 위의 해시 검사와 합쳐지면 그 상태에 영영 들어가지 못하므로
        /// 타임아웃이 유일한 탈출구다. 없으면 <c>End</c> 연출을 기다리는 팝업이 화면에 박제된다.
        ///
        /// <b>반복 상태</b> — Loop Time이 켜진 상태는 계속 돌지만 여기서는 첫 바퀴가 끝나면
        /// 완료로 본다. 그러지 않으면 영영 끝나지 않는다.
        /// </summary>
        private sealed class AnimatorWaitHandle : IMotionHandle
        {
            private readonly Animator _animator;
            private readonly int _layer;
            private readonly int _stateHash;
            private readonly float _timeout;
            private float _waited;
            private bool _entered;
            private bool _done;

            public AnimatorWaitHandle(Animator animator, int layer, int stateHash, float timeout)
            {
                _animator = animator;
                _layer = layer;
                _stateHash = stateHash;
                _timeout = timeout;
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                if (_animator == null)
                {
                    _done = true;
                    return;
                }

                if (deltaSeconds > 0f)
                {
                    _waited += deltaSeconds;
                }

                if (_waited >= _timeout)
                {
                    _done = true;
                    return;
                }

                if (_animator.IsInTransition(_layer))
                {
                    return;
                }

                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(_layer);

                // 이름으로 재생했으면 shortNameHash가, 경로로 재생했으면 fullPathHash가 맞는다.
                if (!_entered)
                {
                    if (info.shortNameHash != _stateHash && info.fullPathHash != _stateHash)
                    {
                        return;
                    }

                    _entered = true;
                }

                if (info.normalizedTime >= 1f)
                {
                    _done = true;
                }
            }

            public void Cancel()
            {
                _done = true;
            }
        }
    }
}
