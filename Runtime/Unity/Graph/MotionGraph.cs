using System;
using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// GUI로 조립한 UI 연출 하나. 프리셋 에셋이자 <see cref="IMotionGraphView"/>다.
    ///
    /// <b>실행 상태를 일절 갖지 않는다.</b> 같은 에셋을 인벤토리 슬롯 100개가 동시에 쓰는 것이
    /// 정상적인 사용법이므로, 그래프가 상태를 조금이라도 들면 그 순간 전부 깨진다.
    /// 실행 상태는 <c>MotionScope</c>가 소유한다.
    ///
    /// 조회 로직은 이 클래스에 없다 — 순수 코어의 <see cref="MotionGraphIndex"/>가 전부 갖고
    /// 있고 여기는 위임만 한다. 그 덕분에 그래프 로직이 UnityEngine 없이 테스트된다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMotionGraph", menuName = "Juahn/UI Motion/Motion Graph")]
    public sealed partial class MotionGraph : ScriptableObject, IMotionGraphView
    {
        /// <summary>
        /// 노드들. <c>SerializeReference</c>라 다형 노드를 그대로 저장한다.
        ///
        /// 타입이 사라진 노드는 여기에 <b>null 원소</b>로 남는다 — 그래프 전체가 깨지지 않고
        /// 그 노드만 실행 시 건너뛰어진다. 스펙 9절의 "결손 노드" 처리가 이것이다.
        /// </summary>
        [SerializeReference] private List<MotionNodeBase> _nodes = new List<MotionNodeBase>();

        /// <summary>
        /// 실행 흐름 간선들. 평면 목록이라 YAML diff가 줄 단위로 움직인다.
        /// <b>배열 순서가 곧 자식 순서</b>이고, 그것이 <c>Sequence</c>의 실행 순서다.
        /// </summary>
        [SerializeField] private List<NodeLink> _links = new List<NodeLink>();

        [SerializeField] private List<TriggerDeclaration> _triggers = new List<TriggerDeclaration>();

        [SerializeField]
        [Tooltip("UI 연출은 기본적으로 Time.timeScale을 무시한다. 일시정지 중에도 팝업은 열리고 닫혀야 하기 때문이다.")]
        private bool _useUnscaledTime = true;

        /// <summary>
        /// 다음에 부여할 노드 id. <b>재사용하지 않는다</b> — 지운 노드의 id를 다시 쓰면
        /// 남아 있던 간선이 엉뚱한 노드에 다시 붙는다.
        /// </summary>
        [SerializeField] private int _nextNodeId = 1;

        /// <summary>
        /// 파생 조회 구조. 저장되지 않고 필요할 때 계산된다.
        ///
        /// 이것은 읽기 전용 캐시이지 실행 상태가 아니다 — 같은 에셋을 여러 오브젝트가
        /// 공유해도 안전하다.
        /// </summary>
        [NonSerialized] private MotionGraphIndex _index;

        /// <summary>
        /// 인덱스가 쓰는 로그. <b>인덱스보다 오래 산다</b> — 인덱스는 편집할 때마다
        /// 다시 만들어지는데, 로그까지 새로 만들면 "중복 노드 id" 같은 경고가 편집 한 번마다
        /// 콘솔에 다시 찍힌다.
        /// </summary>
        [NonSerialized] private OnceLogger _log;

        /// <summary>이 그래프를 <see cref="Time.unscaledDeltaTime"/>으로 돌릴지.</summary>
        public bool UseUnscaledTime => _useUnscaledTime;

        public IReadOnlyList<MotionNodeBase> Nodes => _nodes;

        public IReadOnlyList<NodeLink> Links => _links;

        public string GraphName => name;

        public IReadOnlyList<TriggerDeclaration> Triggers => Index.Triggers;

        /// <summary>
        /// 이 그래프가 요구하는 슬롯들. <b>저작값이 아니라 파생값이다</b> — 노드들의
        /// <c>[MotionSlot]</c> 필드에서 매번 계산한다. 노드를 지우면 슬롯도 사라진다.
        /// </summary>
        public IReadOnlyList<SlotDeclaration> Slots => Index.Slots;

        public MotionNodeBase GetNode(NodeId id) => Index.GetNode(id);

        public NodeId GetEntry(string triggerName) => Index.GetEntry(triggerName);

        public IReadOnlyList<NodeId> GetChildren(NodeId parent) => Index.GetChildren(parent);

        private MotionGraphIndex Index
        {
            get
            {
                if (_index == null)
                {
                    if (_log == null)
                    {
                        _log = new OnceLogger(new UnityMotionLog(this));
                    }

                    _index = new MotionGraphIndex(name, _nodes, _links, _triggers, _log);
                }

                return _index;
            }
        }

        private void OnEnable()
        {
            // 도메인 리로드 후 파생값을 다시 계산하게 한다.
            _index = null;
        }

        private void OnValidate()
        {
            _index = null;
        }
    }
}
