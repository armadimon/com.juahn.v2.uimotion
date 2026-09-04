using System;
using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프 하나를 이 계층 위에서 재생한다. 프리팹에 붙는 유일한 컴포넌트다.
    ///
    /// 그래프는 여러 오브젝트가 공유하는 불변 데이터이고, 이 컴포넌트가 그것을
    /// "누구에게" 적용할지(슬롯 바인딩)와 "지금 무엇이 도는지"(실행 상태)를 갖는다.
    /// </summary>
    [AddComponentMenu("Juahn/UI Motion/Motion Player")]
    [DisallowMultipleComponent]
    public sealed class MotionPlayer : MonoBehaviour
    {
        [SerializeField] private MotionGraph _graph;

        /// <summary>
        /// 슬롯 이름 -> 오브젝트. 목록 자체는 그래프에서 파생되지만 <b>값은 여기 저장된다</b>.
        /// 인스펙터가 <see cref="SyncBindings"/>로 목록을 맞춘다.
        /// </summary>
        [SerializeField] private SlotBinding[] _bindings = new SlotBinding[0];

        [SerializeField]
        [Tooltip("활성화될 때 Start를 자동 발사한다. 호스트가 트리거를 몰아주면 무시된다.")]
        private bool _playOnEnable = true;

        [NonSerialized] private MotionRuntime _runtime;
        [NonSerialized] private OnceLogger _log;
        [NonSerialized] private bool _ownedByHost;

        /// <summary>지금 재생 중인 그래프.</summary>
        public MotionGraph Graph => _graph;

        public IReadOnlyList<SlotBinding> Bindings => _bindings;

        /// <summary>
        /// 트윈 백엔드. 비워 두면 <see cref="BuiltinTweenRunner"/>를 쓴다.
        /// DOTween 어댑터 패키지가 부트스트랩에서 이것을 채운다.
        /// </summary>
        public IMotionTweenRunner TweenRunner { get; set; }

        /// <summary>호스트가 트리거를 몰아주는 중인가.</summary>
        public bool IsTriggerOwnershipClaimed => _ownedByHost;

        /// <summary>
        /// 트리거 발사를 호스트가 전담한다고 선언한다. <see cref="_playOnEnable"/>이 무시된다.
        ///
        /// <b>왜 필요한가</b> — UiService 브릿지가 붙은 채로 PlayOnEnable이 켜져 있으면
        /// <c>Start</c>가 두 번 발사된다. 재발사 정책이 <c>Restart</c>면 우연히 무해하지만
        /// <c>Ignore</c>나 <c>Queue</c>인 그래프에서는 실제 버그가 된다.
        ///
        /// 늦게 불러도 안전하다 — 이미 시작된 것을 걷어낸다.
        /// </summary>
        public void ClaimTriggerOwnership()
        {
            if (_ownedByHost)
            {
                return;
            }

            _ownedByHost = true;
            StopAll();
        }

        /// <summary>
        /// 트리거를 발사한다. <b>비활성 플레이어에서는 아무 일도 일어나지 않는다</b> —
        /// 아래 설명 참조.
        /// </summary>
        public void Fire(string trigger)
        {
            // 비활성 플레이어는 펌프에 등록되어 있지 않다(OnDisable이 해제한다).
            // 그런데도 스코프를 만들면 아무도 굴려 주지 않는 채로 IsPlaying이 true가 되고,
            // 뒤이은 WaitFor가 그 검사를 통과해 대기열에 들어간 뒤 영원히 풀리지 않는다.
            // SetActive(false) 상태로 재사용되는 풀링된 팝업이 영영 닫히지 않는 경로다.
            //
            // 스코프를 아예 만들지 않으면 IsPlaying이 false로 남아 WaitFor가 즉시 콜백한다.
            // 비활성 오브젝트는 애니메이션할 수 없으므로 "전이가 즉시 끝났다"가 옳은 의미다.
            if (!isActiveAndEnabled)
            {
                MotionLogs.WarnOnce(EnsureLog(), "inactive-fire",
                    "player '" + name + "' is inactive; trigger '" + trigger +
                    "' was ignored because nothing would tick it");
                return;
            }

            MotionRuntime runtime = EnsureRuntime();
            if (runtime == null)
            {
                return;
            }

            runtime.Fire(trigger);
        }

        public void Stop(string trigger)
        {
            if (_runtime != null)
            {
                _runtime.Stop(trigger);
            }
        }

        public void StopAll()
        {
            if (_runtime != null)
            {
                _runtime.StopAll();
            }
        }

        public bool IsPlaying(string trigger)
        {
            return _runtime != null && _runtime.IsPlaying(trigger);
        }

        /// <summary>
        /// 트리거가 끝나면 부른다. <b>자연 완료든 취소든 부른다</b> — 대기자를 영영
        /// 붙잡아 두면 팝업이 닫히지 않는다. 지금 재생 중이 아니면 즉시 부른다.
        /// </summary>
        public void WaitFor(string trigger, Action onCompleted)
        {
            if (onCompleted == null)
            {
                return;
            }

            MotionRuntime runtime = EnsureRuntime();
            if (runtime == null)
            {
                onCompleted();
                return;
            }

            runtime.WaitFor(trigger, onCompleted);
        }

        /// <summary>
        /// 그래프를 갈아 끼운다. 슬롯 바인딩은 <b>이름이 같으면 보존된다</b>.
        /// </summary>
        public void SetGraph(MotionGraph graph)
        {
            if (_graph == graph)
            {
                return;
            }

            StopAll();

            _graph = graph;
            _runtime = null;

            SyncBindings();

            if (isActiveAndEnabled)
            {
                EnsureRuntime();
                FireStartIfAutonomous();
            }
        }

        /// <summary>
        /// 바인딩 목록을 현재 그래프의 슬롯 목록에 맞춘다.
        ///
        /// 새 그래프에 없는 슬롯의 바인딩은 <b>조용히 버리지 않고</b> 뒤에 남긴다. 그래프를
        /// 잘못 바꿨다가 되돌렸을 때 손으로 채운 참조가 사라져 있으면 안 되기 때문이다.
        /// 인스펙터가 그것을 "이 그래프에 없는 슬롯"으로 표시한다.
        /// </summary>
        public void SyncBindings()
        {
            var next = new List<SlotBinding>();
            var used = new HashSet<string>(StringComparer.Ordinal);

            IReadOnlyList<SlotDeclaration> slots = _graph == null ? null : _graph.Slots;

            if (slots != null)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    string slotName = slots[i].Name;
                    if (string.IsNullOrEmpty(slotName) || !used.Add(slotName))
                    {
                        continue;
                    }

                    next.Add(new SlotBinding(slotName, FindBinding(slotName)));
                }
            }

            for (int i = 0; i < _bindings.Length; i++)
            {
                SlotBinding orphan = _bindings[i];
                if (string.IsNullOrEmpty(orphan.Name) || orphan.Target == null || !used.Add(orphan.Name))
                {
                    continue;
                }

                next.Add(orphan);
            }

            _bindings = next.ToArray();

            // 돌던 것을 먼저 걷어낸다. 런타임을 그냥 버리면 스코프의 원상 복구가
            // 실행되지 못해 트윈이 어중간한 값에서 굳는다.
            StopAll();
            _runtime = null;
        }

        /// <summary>슬롯에 오브젝트를 꽂는다. 인스펙터와 자동 바인딩이 쓴다.</summary>
        public void Bind(string slotName, UnityEngine.Object target)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return;
            }

            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].Name == slotName)
                {
                    _bindings[i].Target = target;
                    StopAll();
                    _runtime = null;
                    return;
                }
            }

            var grown = new SlotBinding[_bindings.Length + 1];
            Array.Copy(_bindings, grown, _bindings.Length);
            grown[_bindings.Length] = new SlotBinding(slotName, target);
            _bindings = grown;
            StopAll();
            _runtime = null;
        }

        /// <summary>
        /// 펌프가 부른다. 에디터 프리뷰도 이것을 직접 부른다.
        /// </summary>
        public void TickFromPump(float unscaledDelta, float scaledDelta)
        {
            if (_runtime == null)
            {
                return;
            }

            bool unscaled = _graph == null || _graph.UseUnscaledTime;
            _runtime.Tick(unscaled ? unscaledDelta : scaledDelta);
        }

        private void OnEnable()
        {
            // 다시 활성화되면 경고 억제를 푼다. 지난번에 이미 경고한 문제를
            // 이번에는 못 보고 넘어가면 안 된다.
            if (_log != null)
            {
                _log.Reset();
            }

            EnsureRuntime();

            if (_runtime != null)
            {
                _runtime.ResetDiagnostics();
            }

            MotionPump.Register(this);
            FireStartIfAutonomous();
        }

        private void OnDisable()
        {
            // 취소가 원상 복구를 돌린다. 트윈 누수는 0이어야 한다.
            StopAll();
            MotionPump.Unregister(this);
        }

        private void OnDestroy()
        {
            StopAll();
            MotionPump.Unregister(this);
        }

        private void FireStartIfAutonomous()
        {
            if (_playOnEnable && !_ownedByHost)
            {
                Fire(MotionRuntime.StartTrigger);
            }
        }

        private MotionRuntime EnsureRuntime()
        {
            if (_runtime != null)
            {
                return _runtime;
            }

            if (_graph == null)
            {
                return null;
            }

            var slots = new SlotTable(transform, _bindings);
            _runtime = new MotionRuntime(_graph, slots, EnsureLog(), this);
            return _runtime;
        }

        /// <summary>
        /// 이 플레이어의 로그. 런타임이 있든 없든 경고를 억제하며 낼 수 있어야 하므로
        /// 런타임보다 오래 산다 — 비활성 상태의 Fire는 런타임을 만들지 않는다.
        /// </summary>
        private OnceLogger EnsureLog()
        {
            if (_log == null)
            {
                _log = new OnceLogger(new UnityMotionLog(this));
            }

            return _log;
        }

        private UnityEngine.Object FindBinding(string slotName)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                if (_bindings[i].Name == slotName)
                {
                    return _bindings[i].Target;
                }
            }

            return null;
        }
    }
}
