# UI Motion — 에디터 툴 구현 계획 (계획 3)

> **에이전트 작업자에게:** REQUIRED SUB-SKILL — `superpowers:subagent-driven-development`로 태스크 단위로 실행한다. 스텝은 체크박스(`- [ ]`)다.

**목표:** 계획 1(순수 엔진)과 계획 2(Unity 런타임)가 만든 것을 사람이 실제로 쓸 수 있게 만든다 — 그래프 편집 창, 노드 팔레트, 슬롯 자동 바인딩, Node Doctor, 인에디터 프리뷰, 프리셋 브라우저.

**아키텍처:** 검사 로직(순환·도달 불가·잘못된 배선)은 **순수 코어**에 두고 `dotnet test`로 검증한다. 에디터는 그 결과를 그리기만 한다. 그래프 창은 `UnityEditor.Experimental.GraphView` 위에 올린다 — 뷰 계층일 뿐 우리 데이터를 소유하지 않는다.

**기술 스택:** Unity 6000.x · GraphView · UI Toolkit · netstandard2.1 · C# 9.0

---

## 저장소가 둘이다

| 태스크 | 저장소 |
|---|---|
| 1 ~ 3 | `com.juahn.v2.uimotion` (기존, 런타임) |
| 4 ~ 15 | `com.juahn.v2.uimotion.editor` (**새로 만든다**) |

두 저장소 모두 `/Users/teamsparta/UnityProject/JuahnFrameworkV2/` 아래에 있다. v2 관례대로 패키지마다 독립 git 저장소다.

---

## 이 계획이 확정하는 것

### 그래프 편집 API — `UnityEditor.Experimental.GraphView`를 쓴다

Unity 6000.5에는 그래프 편집 API가 둘 있다. 실측으로 확인한 내용:

| | `UnityEditor.Experimental.GraphView` | `Unity.GraphToolkit.Editor` |
|---|---|---|
| 상태 | 6000.0 ~ 6000.5 전부에 내장. Shader Graph와 VFX Graph가 이 위에 있다 | `com.unity.graphtoolkit` **0.5.0-exp.1** |
| 최소 Unity | 6000.0 | **6000.4** |
| 공개 타입 수 | 77 | 19 |
| 데이터 모델 | **뷰 계층일 뿐. 에셋 포맷을 소유하지 않는다** | `Graph` 기반 클래스가 에셋 포맷을 소유한다 |

**GraphToolkit은 쓸 수 없다.** 두 가지 이유가 각각 단독으로 결정적이다.

첫째, Unity 6000.4를 요구하는데 이 패키지는 `package.json`에 `"unity": "6000.0"`을 선언한다. 그걸 올리면 "어떤 프로젝트에든 단독으로 붙여 쓸 수 있다"(스펙 목표 5)가 깨진다.

둘째, `Graph` 기반 클래스가 직렬화를 소유하므로 이미 만들고 292개 테스트로 검증한 `MotionGraph` + `MotionGraphIndex` 데이터 모델과 정면으로 겹친다. 저작용 그래프와 런타임 에셋을 따로 두고 굽는 구조가 되어 데이터 모델이 둘로 늘어난다.

`Experimental` 네임스페이스가 신경 쓰이지만, Shader Graph가 그 위에 있는 한 Unity가 걷어낼 수 없다. 그리고 뷰 계층이라 나중에 갈아 끼우는 비용이 데이터 계층을 갈아 끼우는 것보다 훨씬 싸다.

### 검사 로직은 코어에 둔다

`MotionGraphValidator`를 순수 코어에 두고 `dotnet test`로 검증한다. 에디터는 결과 목록을 그리기만 한다.

계획 2에서 `MotionGraphIndex`로 같은 것을 해서 그래프 조회 로직 전체를 Unity 없이 검증할 수 있었다. 검사 규칙은 그보다 더 미묘해서(도달 가능성, 순환, 배선 오류) 테스트의 가치가 더 크다.

### 노드 위치는 런타임 에셋에 저장하되 인덱스에서 제외한다

그래프 창은 노드마다 x/y가 필요하다. 별도 에셋으로 빼면 두 파일이 어긋날 수 있으므로 `MotionGraph`에 나란히 저장하되, `MotionGraphIndex`는 그것을 읽지 않는다 — 실행에 아무 영향이 없어야 한다.

---

## 검증 축

| 계층 | 방법 | 어디서 |
|---|---|---|
| `Runtime/Core/**` | `dotnet test` | CI + 로컬 |
| `Runtime/Unity/**` | `Tools~/compile-check/run.sh` | 로컬 |
| `Editor/**` (새 패키지) | `Tools~/compile-check/run.sh` (UnityEditor DLL 참조) | 로컬 |
| `.meta` 누락·GUID 중복 | CI | CI + 로컬 |
| 노드 문서화 계약 | Node Doctor 배치 모드 | 로컬 (Unity 필요) |

**GraphView가 이 하네스에서 컴파일되는 것은 이미 확인했다** — `GraphView` 파생, 포트, 간선, 줌, 매니퓰레이터, `EditorWindow`, UI Toolkit까지 전부 통과한다. 필요한 참조는 여섯이다:
`UnityEngine.CoreModule` · `UnityEngine.UIElementsModule` · `UnityEngine.IMGUIModule` · `UnityEditor.CoreModule` · `UnityEditor.UIElementsModule` · `UnityEditor.GraphViewModule`

## 절대 규칙 (계획 1·2에서 이어짐)

1. **직렬화되는 타입의 필드는 public이고 readonly가 아니다.**
2. **`Runtime/Core`에 UnityEngine이 들어가면 안 된다.**
3. **슬롯 목록은 파생값이다.** 사람이 저작하지 않는다.
4. **LINQ 금지.**
5. **코드·주석·문서에 이모지 금지.** 주석은 한국어로 쓴다.
6. **그래프는 실행 상태를 갖지 않는다.**
7. **`.meta` 파일을 만든다.** 계획 2까지는 만들지 않았고 그것이 결함이었다 — git으로 배포되는 UPM 패키지에서 `.meta`가 빠지면 클론할 때마다 GUID가 새로 생겨 컴포넌트를 붙인 프리팹 참조가 끊긴다. 런타임 패키지는 이미 채웠고 CI가 지킨다. **새 에디터 패키지도 처음부터 채운다.**

---

## 저장소 1 — `com.juahn.v2.uimotion` (Task 1~3)

### Task 1: 코어 — 노드 열거와 순환 검출 버그

**여기에 실제 버그가 있다.** `GraphCycleDetector.CollectSubGraphs`는 노드 id가 1부터 연속이라고 가정하고 `GetNode`가 null을 주면 멈춘다:

```csharp
// 노드 id는 1부터 순서대로다. null이 나오면 끝이다.
for (int i = 1; ; i++)
{
    MotionNodeBase node = graph.GetNode(new NodeId(i));
    if (node == null) break;
```

그런데 계획 2의 `MotionGraph.AddNode`는 `_nextNodeId++`로 id를 주고 **재사용하지 않는다**(지운 노드의 id를 다시 쓰면 남아 있던 간선이 엉뚱한 노드에 붙기 때문이다). 노드 2를 지우면 id가 `{1, 3}`이 되고, 검출기는 2에서 멈춰 **노드 3의 서브그래프를 아예 보지 않는다.** 순환이 있어도 조용히 통과한다.

`IMotionGraphView`에 노드를 열거하는 창구가 없는 것이 원인이다. 그것을 더한다. 검사 도구 전체가 이것을 필요로 한다.

**Files:**
- Modify: `Runtime/Core/Graph/IMotionGraphView.cs`, `Runtime/Core/Graph/MotionGraphIndex.cs`, `Runtime/Core/Exec/GraphCycleDetector.cs`, `Runtime/Core/Authoring/MotionNodeBase.cs`
- Modify: `Runtime/Unity/Graph/MotionGraph.cs`, `Runtime/Unity/Nodes/FloatNode.cs`, `Runtime/Unity/Nodes/BounceNode.cs`
- Modify: `Tests~/dotnet/Fakes/FakeGraph.cs`
- Test: `Tests~/dotnet/GraphEnumerationTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests~/dotnet/GraphEnumerationTests.cs`:

```csharp
using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class GraphEnumerationTests
    {
        private sealed class PlainNode : MotionEffectNode
        {
            public PlainNode(int id)
            {
                Id = new NodeId(id);
            }

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        /// <summary>대상 그래프를 직접 물려 주는 서브그래프 노드.</summary>
        private sealed class DirectSubGraphNode : SubGraphNode
        {
            private IMotionGraphView _target;

            public DirectSubGraphNode(int id, IMotionGraphView target)
            {
                Id = new NodeId(id);
                _target = target;
            }

            public void Retarget(IMotionGraphView target)
            {
                _target = target;
            }

            protected override IMotionGraphView ResolveGraph() => _target;
        }

        private static MotionGraphIndex Index(params MotionNodeBase[] nodes)
        {
            return new MotionGraphIndex("g", nodes, null, null, null);
        }

        [Test]
        public void NodeIds_ListsEveryNode()
        {
            MotionGraphIndex index = Index(new PlainNode(1), new PlainNode(7), new PlainNode(3));

            IReadOnlyList<NodeId> ids = index.NodeIds;

            Assert.That(ids.Count, Is.EqualTo(3));
            Assert.That(ids, Does.Contain(new NodeId(1)));
            Assert.That(ids, Does.Contain(new NodeId(3)));
            Assert.That(ids, Does.Contain(new NodeId(7)));
        }

        [Test]
        public void NodeIds_KeepsAuthoringOrder()
        {
            // 순서가 결정적이어야 검사 결과 목록이 리로드마다 뒤바뀌지 않는다.
            MotionGraphIndex index = Index(new PlainNode(5), new PlainNode(2), new PlainNode(9));

            Assert.That(index.NodeIds[0], Is.EqualTo(new NodeId(5)));
            Assert.That(index.NodeIds[1], Is.EqualTo(new NodeId(2)));
            Assert.That(index.NodeIds[2], Is.EqualTo(new NodeId(9)));
        }

        [Test]
        public void NodeIds_SkipsMissingAndDuplicateNodes()
        {
            MotionGraphIndex index = Index(new PlainNode(1), null, new PlainNode(1));

            Assert.That(index.NodeIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void NodeIds_EmptyGraph_IsEmptyNotNull()
        {
            MotionGraphIndex index = Index();

            Assert.That(index.NodeIds, Is.Not.Null);
            Assert.That(index.NodeIds, Is.Empty);
        }

        // --- 순환 검출 회귀 -------------------------------------------------

        [Test]
        public void CycleDetector_FindsCycleBehindAnIdGap()
        {
            // 저작 API는 id를 재사용하지 않으므로 노드를 지우면 id에 구멍이 생긴다.
            // 옛 검출기는 구멍에서 멈춰 그 뒤의 서브그래프를 보지 않았다.
            var placeholder = new PlainNode(1);
            var sub = new DirectSubGraphNode(3, null);

            MotionGraphIndex root = Index(placeholder, sub);
            sub.Retarget(root);

            Assert.That(GraphCycleDetector.HasCycle(root), Is.True,
                "id 2가 비어 있다고 해서 노드 3을 건너뛰면 안 된다");
        }

        [Test]
        public void CycleDetector_NoCycle_IsFalse()
        {
            var leaf = Index(new PlainNode(1));
            var sub = new DirectSubGraphNode(3, leaf);
            MotionGraphIndex root = Index(new PlainNode(1), sub);

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        [Test]
        public void CycleDetector_DiamondIsNotACycle()
        {
            // 같은 서브그래프를 두 곳에서 쓰는 것은 재사용이지 순환이 아니다.
            var shared = Index(new PlainNode(1));
            MotionGraphIndex root = Index(
                new DirectSubGraphNode(1, shared),
                new DirectSubGraphNode(2, shared));

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        // --- 자식을 막는 노드 -----------------------------------------------

        [Test]
        public void BlocksChildren_DefaultsToFalse()
        {
            Assert.That(new PlainNode(1).BlocksChildren, Is.False);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `NodeIds`와 `BlocksChildren`이 없음.

- [ ] **Step 3: `IMotionGraphView`에 노드 열거를 더한다**

`Runtime/Core/Graph/IMotionGraphView.cs`의 `Slots` 아래에 넣는다:

```csharp
        /// <summary>
        /// 이 그래프의 모든 노드 id. 저작 순서를 유지한다.
        ///
        /// <b>왜 필요한가</b> — 노드 id는 <b>연속이 아니다.</b> 저작 API가 id를 재사용하지
        /// 않으므로(지운 id를 다시 쓰면 남아 있던 간선이 엉뚱한 노드에 붙는다) 노드를 지우면
        /// id에 구멍이 생긴다. <c>GetNode</c>를 1부터 훑다가 null에서 멈추는 코드는
        /// 그 구멍 뒤의 노드를 전부 놓친다.
        /// </summary>
        IReadOnlyList<NodeId> NodeIds { get; }
```

- [ ] **Step 4: `MotionGraphIndex`에 구현한다**

필드에 추가한다:

```csharp
        private static readonly NodeId[] NoNodes = new NodeId[0];

        private readonly NodeId[] _nodeIds;
```

`IndexNodes`가 살아남은 id를 순서대로 모아 돌려주도록 고치고(다른 조회 결과와 같이 배열로 굳힌다), 생성자에서 `_nodeIds = IndexNodes(nodes, log);`로 받는다. 프로퍼티를 더한다:

```csharp
        public IReadOnlyList<NodeId> NodeIds => _nodeIds;
```

`IndexNodes`는 `void`에서 `NodeId[]`를 돌려주도록 바꾼다. `nodes`가 null이면 `NoNodes`를 돌려준다. 노드를 `_nodes` 사전에 넣는 바로 그 자리에서 id를 목록에 함께 담으면 "사전에는 있는데 목록에는 없는" 어긋남이 생길 수 없다.

- [ ] **Step 5: `GraphCycleDetector`를 고친다**

`CollectSubGraphs`의 루프를 바꾼다:

```csharp
        private static List<IMotionGraphView> CollectSubGraphs(IMotionGraphView graph)
        {
            var targets = new List<IMotionGraphView>();

            IReadOnlyList<NodeId> ids = graph.NodeIds;
            if (ids == null)
            {
                return targets;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                var sub = graph.GetNode(ids[i]) as SubGraphNode;
                if (sub == null)
                {
                    continue;
                }

                IMotionGraphView target = sub.PeekGraph();
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }
```

- [ ] **Step 6: `MotionNodeBase`에 `BlocksChildren`을 더한다**

`Reverts` 아래에 넣는다:

```csharp
        /// <summary>
        /// 이 노드가 자식으로 이어지는 흐름을 <b>영원히</b> 막는가.
        ///
        /// <c>Float</c>·<c>Bounce</c>처럼 취소될 때까지 끝나지 않는 노드가 true다.
        /// 실행기는 이 값을 읽지 않는다 — 끝나지 않는 핸들이 이미 그 일을 한다.
        /// 이것은 <b>검사 도구</b>가 "이 노드에 자식을 달면 그 자식은 절대 실행되지 않는다"를
        /// 알아내기 위한 표시다. 그런 배선은 오류를 내지 않고 조용히 아무 일도 하지 않으므로
        /// 사람이 스스로 찾기 어렵다.
        /// </summary>
        public virtual bool BlocksChildren => false;
```

- [ ] **Step 7: `Float`과 `Bounce`가 그것을 덮는다**

`Runtime/Unity/Nodes/FloatNode.cs`와 `BounceNode.cs`의 `Reverts` 옆에 각각 추가한다:

```csharp
        public override bool BlocksChildren => true;
```

- [ ] **Step 8: `FakeGraph`와 `MotionGraph`에 `NodeIds`를 채운다**

`Tests~/dotnet/Fakes/FakeGraph.cs`에 추가한다:

```csharp
        public IReadOnlyList<NodeId> NodeIds
        {
            get
            {
                var ids = new List<NodeId>(_nodes.Count);
                for (int i = 0; i < _nodes.Count; i++)
                {
                    if (_nodes[i] != null)
                    {
                        ids.Add(_nodes[i].Id);
                    }
                }

                return ids;
            }
        }
```

`Runtime/Unity/Graph/MotionGraph.cs`의 다른 위임 옆에 추가한다:

```csharp
        public IReadOnlyList<NodeId> NodeIds => Index.NodeIds;
```

- [ ] **Step 9: 통과를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh
```

기대: `Failed: 0`, `purity exit=1`, 컴파일 통과.

- [ ] **Step 10: 순환 검출 수정이 진짜인지 확인한다 (중요)**

테스트가 통과하는 것만으로는 그것이 옛 버그를 잡는지 알 수 없다. `CollectSubGraphs`를 옛 방식(1부터 null까지)으로 되돌리고 `CycleDetector_FindsCycleBehindAnIdGap`이 **실패하는지** 확인한 뒤 되돌린다.

- [ ] **Step 11: `.meta` 파일을 만들고 커밋한다**

새로 만든 `.cs` 파일마다 `.meta`가 필요하다 (`Tests~` 아래는 예외 — Unity가 무시한다). 형식은 기존 `.cs.meta`를 그대로 따르고 GUID만 새로 만든다 (32자리 소문자 16진수).

```bash
git ls-files | grep -v '\.meta$' | grep -v '^\.github/' | grep -v '~/' | while read f; do [ -e "$f.meta" ] || echo "meta 없음: $f"; done
git add -A
git commit -m "fix: 노드 id에 구멍이 있을 때 순환 검출이 실패하던 문제 수정"
```

---

### Task 2: 코어 — MotionGraphValidator

검사 규칙을 순수 코어에 둔다. 에디터는 결과를 그리기만 한다. 규칙이 미묘해서(도달 가능성, 순환, 조용히 아무 일도 안 하는 배선) 테스트의 가치가 크다.

**Files:**
- Create: `Runtime/Core/Diag/MotionIssueLevel.cs`, `MotionGraphIssue.cs`, `MotionGraphValidator.cs`
- Test: `Tests~/dotnet/MotionGraphValidatorTests.cs`

**규칙 일곱 가지**

| 수준 | 규칙 | 왜 |
|---|---|---|
| Error | 간선이나 트리거가 없는 노드를 가리킨다 | 그 가지가 통째로 실행되지 않는다 |
| Error | 간선에 순환이 있다 | `NodeRun`이 무한히 펼쳐지다 깊이 상한에서 잘린다 |
| Error | 서브그래프 참조에 순환이 있다 | 같은 이유 |
| Warning | 끝나지 않는 노드에 자식이 달렸다 | 그 자식은 **절대** 실행되지 않는데 오류가 나지 않는다 |
| Warning | 어느 트리거에서도 도달할 수 없는 노드 | 만들어 두고 배선을 잊은 것이다 |
| Warning | 선언된 트리거의 진입점이 비어 있다 | 발사해도 아무 일도 없다 |
| Info | `Loop` 트리거가 한 번 돌고 끝난다 | 반복을 기대하고 만든 것이 한 번만 돈다. 게임이 매번 다시 발사하는 경우도 있어 Warning으로 올리지 않는다 |

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests~/dotnet/MotionGraphValidatorTests.cs`:

```csharp
using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionGraphValidatorTests
    {
        private sealed class PlainNode : MotionEffectNode
        {
            public PlainNode(int id) { Id = new NodeId(id); }
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class EndlessNode : MotionEffectNode
        {
            public EndlessNode(int id) { Id = new NodeId(id); }
            public override bool BlocksChildren => true;
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Forever(null);
        }

        private sealed class DirectSubGraphNode : SubGraphNode
        {
            private IMotionGraphView _target;
            public DirectSubGraphNode(int id, IMotionGraphView target) { Id = new NodeId(id); _target = target; }
            public void Retarget(IMotionGraphView t) { _target = t; }
            protected override IMotionGraphView ResolveGraph() => _target;
        }

        private static MotionGraphIndex Build(
            MotionNodeBase[] nodes = null, NodeLink[] links = null, TriggerDeclaration[] triggers = null)
        {
            return new MotionGraphIndex("g", nodes, links, triggers, null);
        }

        private static bool Has(IReadOnlyList<MotionGraphIssue> issues, MotionIssueLevel level, string fragment)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == level && issues[i].Message.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void NullGraph_YieldsNothing()
        {
            Assert.That(MotionGraphValidator.Validate(null), Is.Empty);
        }

        [Test]
        public void HealthyGraph_HasNoIssues()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(MotionGraphValidator.Validate(Build(nodes, links, triggers)), Is.Empty);
        }

        [Test]
        public void LinkToMissingNode_IsError()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(99)) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "#99"), Is.True);
        }

        [Test]
        public void TriggerEntryToMissingNode_IsError()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(42)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, null, triggers)),
                MotionIssueLevel.Error, "Start"), Is.True);
        }

        [Test]
        public void LinkCycle_IsError()
        {
            // NodeRun은 자식을 재귀로 펼치므로 간선 순환은 깊이 상한에서 잘린다.
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2) };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(2), new NodeId(1)),
            };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "cycle"), Is.True);
        }

        [Test]
        public void SharedChild_IsNotACycle()
        {
            // 두 부모가 같은 자식을 가리키는 다이아몬드는 순환이 아니다.
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2), new PlainNode(3) };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(1), new NodeId(3)),
                new NodeLink(new NodeId(2), new NodeId(3)),
            };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "cycle"), Is.False);
        }

        [Test]
        public void SubGraphCycle_IsError()
        {
            var sub = new DirectSubGraphNode(2, null);
            var nodes = new MotionNodeBase[] { new PlainNode(1), sub };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            MotionGraphIndex graph = Build(nodes, links, triggers);
            sub.Retarget(graph);

            Assert.That(Has(MotionGraphValidator.Validate(graph),
                MotionIssueLevel.Error, "sub-graph"), Is.True);
        }

        [Test]
        public void EndlessNodeWithChildren_IsWarning()
        {
            // 이 배선은 오류를 내지 않고 조용히 아무 일도 하지 않는다. 사람이 찾기 가장 어렵다.
            var nodes = new MotionNodeBase[] { new EndlessNode(1), new PlainNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Warning, "never run"), Is.True);
        }

        [Test]
        public void UnreachableNode_IsWarning()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            IReadOnlyList<MotionGraphIssue> issues = MotionGraphValidator.Validate(Build(nodes, null, triggers));

            Assert.That(Has(issues, MotionIssueLevel.Warning, "#2"), Is.True);
            Assert.That(Has(issues, MotionIssueLevel.Warning, "#1"), Is.False, "진입 노드는 도달 가능하다");
        }

        [Test]
        public void TriggerWithoutEntry_IsWarning()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var triggers = new[]
            {
                new TriggerDeclaration("Start", new NodeId(1)),
                new TriggerDeclaration("Click", NodeId.None),
            };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, null, triggers)),
                MotionIssueLevel.Warning, "Click"), Is.True);
        }

        [Test]
        public void FiniteLoop_IsWarning()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, null, triggers)),
                MotionIssueLevel.Warning, "Loop"), Is.True);
        }

        [Test]
        public void LoopWithEndlessNode_IsFine()
        {
            var nodes = new MotionNodeBase[] { new EndlessNode(1) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(MotionGraphValidator.Validate(Build(nodes, null, triggers)), Is.Empty);
        }

        [Test]
        public void LoopWithInfiniteRepeat_IsFine()
        {
            var repeat = new RepeatNode { Id = new NodeId(1), Count = RepeatNode.Infinite };
            var nodes = new MotionNodeBase[] { repeat, new PlainNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(MotionGraphValidator.Validate(Build(nodes, links, triggers)), Is.Empty);
        }

        [Test]
        public void HasErrors_DistinguishesLevels()
        {
            var warningOnly = new List<MotionGraphIssue>
            {
                new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None, "w"),
            };
            var withError = new List<MotionGraphIssue>
            {
                new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None, "w"),
                new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None, "e"),
            };

            Assert.That(MotionGraphValidator.HasErrors(warningOnly), Is.False);
            Assert.That(MotionGraphValidator.HasErrors(withError), Is.True);
        }

        [Test]
        public void Validate_ClearsTargetList()
        {
            var into = new List<MotionGraphIssue> { new MotionGraphIssue(MotionIssueLevel.Info, NodeId.None, "stale") };
            MotionGraphValidator.Validate(Build(), into);

            Assert.That(into, Is.Empty);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패.

- [ ] **Step 3: 결과 타입을 만든다**

`Runtime/Core/Diag/MotionIssueLevel.cs`:

```csharp
namespace Juahn.UiMotion
{
    /// <summary>검사 결과의 심각도. 값이 클수록 심각하다 — 정렬에 쓴다.</summary>
    public enum MotionIssueLevel
    {
        /// <summary>알아 두면 좋은 것. 동작에는 문제가 없다.</summary>
        Info = 0,

        /// <summary>의도와 다르게 동작할 가능성이 높다. 실행은 된다.</summary>
        Warning = 1,

        /// <summary>확실히 잘못됐다. 배포 전에 고쳐야 한다.</summary>
        Error = 2,
    }
}
```

`Runtime/Core/Diag/MotionGraphIssue.cs`:

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 검사에서 나온 문제 하나. 그래프 창이 이것을 노드 위에 배지로 그리고
    /// Node Doctor가 표로 나열한다.
    /// </summary>
    public struct MotionGraphIssue
    {
        public MotionIssueLevel Level;

        /// <summary>문제가 붙은 노드. 그래프 전체의 문제면 <see cref="NodeId.None"/>.</summary>
        public NodeId Node;

        public string Message;

        public MotionGraphIssue(MotionIssueLevel level, NodeId node, string message)
        {
            Level = level;
            Node = node;
            Message = message;
        }

        public override string ToString()
        {
            string where = Node.IsValid ? " " + Node : "";
            return Level + where + ": " + Message;
        }
    }
}
```

- [ ] **Step 4: 검사기를 만든다**

`Runtime/Core/Diag/MotionGraphValidator.cs`:

```csharp
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프의 배선을 검사한다. <b>순수 로직이라 Unity 없이 테스트된다.</b>
    /// 에디터는 결과를 그리기만 한다.
    ///
    /// 여기서 잡는 것들의 공통점은 <b>런타임에 오류를 내지 않는다</b>는 것이다.
    /// 끝나지 않는 노드에 달린 자식은 조용히 실행되지 않고, 도달 불가 노드는 조용히
    /// 아무 일도 하지 않는다. 사람이 스스로 찾기 가장 어려운 종류라 도구가 잡아야 한다.
    /// </summary>
    public static class MotionGraphValidator
    {
        public static List<MotionGraphIssue> Validate(IMotionGraphView graph)
        {
            var into = new List<MotionGraphIssue>();
            Validate(graph, into);
            return into;
        }

        /// <summary><paramref name="into"/>는 먼저 비운다.</summary>
        public static void Validate(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            if (graph == null)
            {
                return;
            }

            IReadOnlyList<NodeId> ids = graph.NodeIds;
            if (ids == null)
            {
                return;
            }

            CheckDanglingLinks(graph, ids, into);
            CheckTriggers(graph, into);
            CheckBlockedChildren(graph, ids, into);
            CheckLinkCycles(graph, ids, into);
            CheckReachability(graph, ids, into);
            CheckSubGraphCycles(graph, into);
        }

        public static bool HasErrors(IReadOnlyList<MotionGraphIssue> issues)
        {
            if (issues == null)
            {
                return false;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == MotionIssueLevel.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckDanglingLinks(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                IReadOnlyList<NodeId> children = graph.GetChildren(ids[i]);
                for (int c = 0; c < children.Count; c++)
                {
                    if (graph.GetNode(children[c]) == null)
                    {
                        into.Add(new MotionGraphIssue(MotionIssueLevel.Error, ids[i],
                            "link points at " + children[c] + " which no longer exists"));
                    }
                }
            }
        }

        private static void CheckTriggers(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null)
                {
                    continue;
                }

                if (!decl.Entry.IsValid)
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None,
                        "trigger '" + decl.Name + "' has no entry node; firing it does nothing"));
                    continue;
                }

                if (graph.GetNode(decl.Entry) == null)
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None,
                        "trigger '" + decl.Name + "' points at " + decl.Entry + " which no longer exists"));
                    continue;
                }

                if (decl.Name == MotionRuntime.LoopTrigger && !LoopRepeats(graph, decl.Entry))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, decl.Entry,
                        "trigger 'Loop' runs once and stops; wrap it in Repeat or use a node that " +
                        "keeps going such as Float or Bounce"));
                }
            }
        }

        private static void CheckBlockedChildren(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                MotionNodeBase node = graph.GetNode(ids[i]);
                if (node == null || !node.BlocksChildren)
                {
                    continue;
                }

                if (graph.GetChildren(ids[i]).Count > 0)
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, ids[i],
                        node.GetType().Name + " never finishes, so its children will never run"));
                }
            }
        }

        private static void CheckLinkCycles(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            // NodeRun은 자식을 재귀로 펼치므로 간선 순환은 무한히 자란다.
            // 깊이 상한이 프로세스를 살리지만 그래프는 의도대로 돌지 않는다.
            var state = new Dictionary<int, int>();   // 0 미방문, 1 경로 위, 2 완료

            for (int i = 0; i < ids.Count; i++)
            {
                if (FindCycle(graph, ids[i], state))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Error, ids[i],
                        "the links form a cycle; execution would expand without end"));
                    return;
                }
            }
        }

        private static bool FindCycle(IMotionGraphView graph, NodeId id, Dictionary<int, int> state)
        {
            int mark;
            if (state.TryGetValue(id.Value, out mark))
            {
                if (mark == 1)
                {
                    return true;
                }

                if (mark == 2)
                {
                    return false;
                }
            }

            state[id.Value] = 1;

            IReadOnlyList<NodeId> children = graph.GetChildren(id);
            for (int i = 0; i < children.Count; i++)
            {
                if (FindCycle(graph, children[i], state))
                {
                    return true;
                }
            }

            state[id.Value] = 2;
            return false;
        }

        private static void CheckReachability(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            var reached = new HashSet<int>();
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;

            if (triggers != null)
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    if (triggers[i] != null && triggers[i].Entry.IsValid)
                    {
                        Reach(graph, triggers[i].Entry, reached);
                    }
                }
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!reached.Contains(ids[i].Value))
                {
                    MotionNodeBase node = graph.GetNode(ids[i]);
                    string what = node == null ? "node" : node.GetType().Name;
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, ids[i],
                        what + " " + ids[i] + " cannot be reached from any trigger; it will never run"));
                }
            }
        }

        private static void Reach(IMotionGraphView graph, NodeId id, HashSet<int> reached)
        {
            if (!reached.Add(id.Value))
            {
                return;
            }

            IReadOnlyList<NodeId> children = graph.GetChildren(id);
            for (int i = 0; i < children.Count; i++)
            {
                Reach(graph, children[i], reached);
            }
        }

        private static void CheckSubGraphCycles(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            if (GraphCycleDetector.HasCycle(graph))
            {
                into.Add(new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None,
                    "sub-graph references form a cycle"));
            }
        }

        /// <summary>
        /// 이 가지가 스스로 계속 도는가. 끝나지 않는 노드나 무한 <c>Repeat</c>이 하나라도 있으면 그렇다.
        /// </summary>
        private static bool LoopRepeats(IMotionGraphView graph, NodeId entry)
        {
            var seen = new HashSet<int>();
            return LoopRepeats(graph, entry, seen);
        }

        private static bool LoopRepeats(IMotionGraphView graph, NodeId id, HashSet<int> seen)
        {
            if (!seen.Add(id.Value))
            {
                return false;
            }

            MotionNodeBase node = graph.GetNode(id);
            if (node != null)
            {
                if (node.BlocksChildren)
                {
                    return true;
                }

                var repeat = node as RepeatNode;
                if (repeat != null && repeat.Count < 0)
                {
                    return true;
                }
            }

            IReadOnlyList<NodeId> children = graph.GetChildren(id);
            for (int i = 0; i < children.Count; i++)
            {
                if (LoopRepeats(graph, children[i], seen))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
```

- [ ] **Step 5: 통과를 확인하고 커밋한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh
```

새 `.cs` 파일마다 `.meta`를 만든 뒤:

```bash
git add -A
git commit -m "feat: 그래프 배선 검사기 추가"
```

---

### Task 3: Unity — 노드 위치 저장

그래프 창은 노드마다 x/y가 필요하다. 별도 에셋으로 빼면 두 파일이 어긋날 수 있으므로 `MotionGraph`에 나란히 저장하되, **`MotionGraphIndex`는 이것을 읽지 않는다** — 위치가 실행에 영향을 주면 안 된다.

**Files:**
- Create: `Runtime/Unity/Graph/NodeLayout.cs`
- Modify: `Runtime/Unity/Graph/MotionGraph.cs`, `Runtime/Unity/Graph/MotionGraphAuthoring.cs`

- [ ] **Step 1: NodeLayout을 만든다**

`Runtime/Unity/Graph/NodeLayout.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프 창에서 노드가 놓인 자리. <b>순전히 에디터용 데이터다</b> —
    /// <see cref="MotionGraphIndex"/>는 이것을 읽지 않고, 실행에 아무 영향이 없다.
    ///
    /// 별도 에셋으로 빼지 않는 이유는 두 파일이 어긋날 수 있기 때문이다.
    /// 노드를 지웠는데 위치 파일만 남거나 그 반대가 되면 창이 이상해진다.
    /// </summary>
    [Serializable]
    public struct NodeLayout
    {
        public NodeId Node;
        public Vector2 Position;

        public NodeLayout(NodeId node, Vector2 position)
        {
            Node = node;
            Position = position;
        }
    }
}
```

- [ ] **Step 2: MotionGraph에 저장 자리를 만든다**

`Runtime/Unity/Graph/MotionGraph.cs`의 `_nextNodeId` 아래에 추가한다:

```csharp
        /// <summary>
        /// 그래프 창의 노드 위치. 실행과 무관하다 — 인덱스는 이것을 읽지 않는다.
        /// </summary>
        [SerializeField] private List<NodeLayout> _layout = new List<NodeLayout>();
```

그리고 조회 메서드를 더한다:

```csharp
        /// <summary>그래프 창에서의 노드 위치. 저장된 것이 없으면 원점이다.</summary>
        public Vector2 GetNodePosition(NodeId id)
        {
            for (int i = 0; i < _layout.Count; i++)
            {
                if (_layout[i].Node == id)
                {
                    return _layout[i].Position;
                }
            }

            return Vector2.zero;
        }
```

- [ ] **Step 3: 저작 API에 위치 쓰기를 더한다**

`Runtime/Unity/Graph/MotionGraphAuthoring.cs`에 추가한다. **위치는 파생 인덱스에 들어가지 않으므로 `Invalidate()`를 부르지 않는다** — 노드를 옮길 때마다 인덱스를 다시 만들면 큰 그래프에서 끌기가 버벅인다.

```csharp
        /// <summary>
        /// 그래프 창에서의 노드 위치를 저장한다.
        ///
        /// <b>인덱스를 무효화하지 않는다.</b> 위치는 실행에 아무 영향이 없고, 노드를 끌 때마다
        /// 인덱스를 다시 만들면 노드가 많은 그래프에서 끌기가 눈에 띄게 버벅인다.
        /// 이것이 이 클래스에서 <c>Invalidate()</c>를 부르지 않는 유일한 메서드다.
        /// </summary>
        public void SetNodePosition(NodeId id, Vector2 position)
        {
            if (!id.IsValid)
            {
                return;
            }

            for (int i = 0; i < _layout.Count; i++)
            {
                if (_layout[i].Node == id)
                {
                    _layout[i] = new NodeLayout(id, position);
                    return;
                }
            }

            _layout.Add(new NodeLayout(id, position));
        }
```

- [ ] **Step 4: 노드를 지울 때 위치도 지운다**

`RemoveNode`에서 간선을 정리하는 루프 옆에 추가한다:

```csharp
            for (int i = _layout.Count - 1; i >= 0; i--)
            {
                if (_layout[i].Node == id)
                {
                    _layout.RemoveAt(i);
                }
            }
```

- [ ] **Step 5: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

새 `.cs` 파일에 `.meta`를 만든 뒤:

```bash
git add -A
git commit -m "feat: 그래프 창을 위한 노드 위치 저장 추가"
```

---

## 저장소 2 — `com.juahn.v2.uimotion.editor` (Task 4~15)

**이 저장소는 아직 없다. Task 4가 만든다.** 위치: `/Users/teamsparta/UnityProject/JuahnFrameworkV2/com.juahn.v2.uimotion.editor`

### Task 4: 에디터 패키지 뼈대와 컴파일 게이트

먼저 안전망을 만든다. 이것 없이 에디터 코드를 쓰면 Unity를 열기 전까지 오타조차 발견되지 않는다.

**Files:** (전부 새 저장소)
- `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`, `.gitignore`
- `Editor/juahn.v2.UiMotion.Editor.asmdef`
- `Editor/MotionEditorPaths.cs`
- `Tools~/compile-check/UiMotion.Editor.Compile.csproj`, `run.sh`
- `.github/workflows/ci.yml`
- 위 전부의 `.meta` (`.github`와 `Tools~` 제외)

- [ ] **Step 1: 저장소를 만든다**

```bash
cd /Users/teamsparta/UnityProject/JuahnFrameworkV2
mkdir com.juahn.v2.uimotion.editor
cd com.juahn.v2.uimotion.editor
git init
git checkout -b main
```

- [ ] **Step 2: `package.json`**

```json
{
  "name": "com.juahn.v2.uimotion.editor",
  "displayName": "UI Motion Tools",
  "author": "armadimon",
  "version": "0.1.0",
  "unity": "6000.0",
  "license": "MIT",
  "type": "library",
  "description": "Authoring tools for UI Motion: a node graph window, a node palette, slot auto-binding on MotionPlayer, an in-editor preview driver, a preset browser, and Node Doctor which checks that every published node carries a description and a working sample.",
  "dependencies": {
    "com.juahn.v2.uimotion": "0.1.0"
  }
}
```

런타임 패키지를 의존성으로 **선언한다**. `com.juahn.v2.vcontainer`가 의존성을 비워 둔 것은 대상이 소비자가 따로 설치하는 유료 에셋(VContainer)이기 때문이다. 여기는 우리 패키지라 선언하는 것이 맞다.

- [ ] **Step 3: `.gitignore`**

런타임 패키지의 것을 그대로 쓰되 `Tests~` 예외는 빼고 컴파일 게이트 예외만 남긴다:

```
# Unity UPM package .gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
*.csproj
*.unityproj
*.sln
*.suo
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
.DS_Store
.vs/
.idea/
.vscode/
node_modules/
*.tmp

# dotnet
[Bb]in/
**/TestResults/

# 컴파일 게이트 프로젝트는 커밋한다.
!Tools~/compile-check/*.csproj
```

- [ ] **Step 4: asmdef**

`Editor/juahn.v2.UiMotion.Editor.asmdef`:

```json
{
    "name": "juahn.v2.UiMotion.Editor",
    "rootNamespace": "Juahn.UiMotion.Editor",
    "references": [
        "juahn.v2.UiMotion",
        "juahn.v2.UiMotion.Core"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 5: 첫 에디터 파일**

게이트가 검증할 대상이 하나는 있어야 한다. 마침 여기저기서 쓸 상수 모음이 필요하다.

`Editor/MotionEditorPaths.cs`:

```csharp
namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 툴 전체가 공유하는 이름과 경로.
    ///
    /// 메뉴 경로를 문자열 리터럴로 흩어 두면 하나를 고칠 때 나머지가 남아
    /// 메뉴가 두 군데로 갈라진다.
    /// </summary>
    public static class MotionEditorPaths
    {
        public const string MenuRoot = "Window/UI Motion/";
        public const string AssetMenuRoot = "Assets/UI Motion/";

        public const string GraphWindowTitle = "UI Motion";
        public const string DoctorWindowTitle = "Node Doctor";
        public const string BrowserWindowTitle = "Motion Presets";

        /// <summary>
        /// 이 패키지가 들고 다니는 예시 그래프의 위치.
        ///
        /// <b><c>Samples~</c>가 아니다.</b> Unity는 <c>~</c>로 끝나는 폴더를 아예 임포트하지
        /// 않으므로 그 안의 에셋은 <c>AssetDatabase</c>로 읽을 수 없다. 그러면 팔레트에서
        /// 노드에 호버했을 때 예시를 그 자리에서 재생한다는 스펙 7.1의 요구를 지킬 수 없다.
        ///
        /// 프로젝트가 자기 노드를 추가하면 그 예시는 프로젝트 어딘가에 있다. 그래서
        /// 예시를 찾을 때는 이 경로가 아니라 <b>이름으로 프로젝트 전체를 검색한다</b>
        /// (<see cref="MotionNodeDoctor.FindSample"/>). 이 상수는 이 패키지가 자기 예시를
        /// 어디에 만들지 정할 때만 쓴다.
        /// </summary>
        public const string SamplesFolder = "Editor/Samples/Nodes";

        public const string GraphAssetExtension = "asset";
    }
}
```

- [ ] **Step 6: 컴파일 게이트**

`Tools~/compile-check/UiMotion.Editor.Compile.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    에디터 계층을 Unity 에디터 없이 컴파일한다.

    런타임 패키지의 소스도 함께 링크한다 — 에디터 어셈블리가 그것을 참조하기 때문이다.
    Unity의 매니지드 DLL은 재배포할 수 없으므로 이 검사는 CI가 아니라 로컬 게이트다.

    UnityManaged / RuntimePackage 경로는 run.sh가 넘긴다.
  -->

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>UiMotion.Editor.CompileCheck</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0649;CS0169;CS0414</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="$(RuntimePackage)/Runtime/Core/**/*.cs" LinkBase="Core" />
    <Compile Include="$(RuntimePackage)/Runtime/Unity/**/*.cs" LinkBase="Runtime" />
    <Compile Include="../../Editor/**/*.cs" LinkBase="Editor" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Unity.Scripting">
      <HintPath>$(UnityManaged)/Unity.Scripting.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(UnityManaged)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIModule">
      <HintPath>$(UnityManaged)/UnityEngine.UIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.AnimationModule">
      <HintPath>$(UnityManaged)/UnityEngine.AnimationModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>$(UnityManaged)/UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(UnityManaged)/UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIElementsModule">
      <HintPath>$(UnityManaged)/UnityEngine.UIElementsModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEditor.CoreModule">
      <HintPath>$(UnityManaged)/UnityEditor.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEditor.UIElementsModule">
      <HintPath>$(UnityManaged)/UnityEditor.UIElementsModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEditor.GraphViewModule">
      <HintPath>$(UnityManaged)/UnityEditor.GraphViewModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UI">
      <HintPath>$(UnityUgui)</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

`Tools~/compile-check/run.sh` — 런타임 패키지의 것을 베끼되 런타임 패키지 경로를 하나 더 찾는다:

```bash
#!/usr/bin/env bash
#
# 에디터 계층 컴파일 게이트.
#
# Unity 에디터를 열지 않고 런타임 패키지 + 이 패키지의 Editor 전체를 컴파일한다.
# 에디터 파일을 건드렸으면 커밋 전에 이것을 돌린다.
#
#   ./Tools~/compile-check/run.sh
#   UNITY_ROOT=... RUNTIME_PACKAGE=... ./Tools~/compile-check/run.sh
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "${HERE}/../.." && pwd)"

# --- 런타임 패키지를 찾는다 --------------------------------------------
if [ -z "${RUNTIME_PACKAGE:-}" ]; then
  RUNTIME_PACKAGE="$(cd "${PACKAGE_ROOT}/../com.juahn.v2.uimotion" 2>/dev/null && pwd || true)"
fi

if [ -z "${RUNTIME_PACKAGE}" ] || [ ! -d "${RUNTIME_PACKAGE}/Runtime/Core" ]; then
  echo "런타임 패키지를 찾지 못했습니다. RUNTIME_PACKAGE를 지정하세요." >&2
  echo "  예: RUNTIME_PACKAGE=/path/to/com.juahn.v2.uimotion $0" >&2
  exit 2
fi

# --- Unity 설치를 찾는다 ------------------------------------------------
if [ -z "${UNITY_ROOT:-}" ]; then
  UNITY_ROOT="$(ls -d /Applications/Unity/Hub/Editor/*/ 2>/dev/null | sort -V | tail -1 || true)"
fi

if [ -z "${UNITY_ROOT}" ] || [ ! -d "${UNITY_ROOT}" ]; then
  echo "Unity 설치를 찾지 못했습니다. UNITY_ROOT를 지정하세요." >&2
  exit 2
fi

UNITY_MANAGED="${UNITY_ROOT}/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine"
if [ ! -d "${UNITY_MANAGED}" ]; then
  echo "매니지드 어셈블리 폴더가 없습니다: ${UNITY_MANAGED}" >&2
  echo "이 스크립트는 macOS 레이아웃을 가정합니다." >&2
  exit 2
fi

if [ -z "${UNITY_UGUI:-}" ]; then
  UNITY_UGUI="$(ls "${UNITY_ROOT}"/Unity.app/Contents/Resources/PackageManager/ProjectTemplates/libcache/*/ScriptAssemblies/UnityEngine.UI.dll 2>/dev/null | head -1 || true)"
fi

if [ -z "${UNITY_UGUI}" ] || [ ! -f "${UNITY_UGUI}" ]; then
  echo "UnityEngine.UI.dll을 찾지 못했습니다. UNITY_UGUI로 지정하세요." >&2
  exit 2
fi

echo "Unity:   ${UNITY_ROOT}"
echo "런타임:  ${RUNTIME_PACKAGE}"
echo

dotnet build "${HERE}/UiMotion.Editor.Compile.csproj" \
  -p:UnityManaged="${UNITY_MANAGED}" \
  -p:UnityUgui="${UNITY_UGUI}" \
  -p:RuntimePackage="${RUNTIME_PACKAGE}" \
  -v quiet --nologo

echo
echo "에디터 계층 컴파일 통과."
```

```bash
chmod +x Tools~/compile-check/run.sh
```

- [ ] **Step 7: CI**

`.github/workflows/ci.yml` — 런타임 패키지의 것에서 `dotnet test`와 코어 순수성 검사를 빼고(여기엔 코어가 없다) `.meta` 검사는 그대로 가져온다:

```yaml
name: package-validate

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Validate package.json is valid JSON
        run: |
          node -e "JSON.parse(require('fs').readFileSync('package.json','utf8'))"
          echo "package.json is valid JSON"

      - name: Check required package fields
        run: |
          node -e "
          const p = JSON.parse(require('fs').readFileSync('package.json','utf8'));
          for (const k of ['name','version','unity','license']) {
            if (!p[k]) { console.error('missing field: ' + k); process.exit(1); }
          }
          if (!p.name.startsWith('com.juahn.v2.')) { console.error('name must start with com.juahn.v2.'); process.exit(1); }
          if (!p.dependencies || !p.dependencies['com.juahn.v2.uimotion']) {
            console.error('must depend on com.juahn.v2.uimotion'); process.exit(1);
          }
          console.log('package fields OK: ' + p.name + '@' + p.version);
          "

      - name: Check assembly definition present and editor only
        run: |
          if ! find Editor -name '*.asmdef' | grep -q . ; then
            echo "no Editor asmdef found"; exit 1
          fi
          node -e "
          const fs = require('fs');
          const path = require('child_process').execSync(\"find Editor -name '*.asmdef'\").toString().trim().split('\n')[0];
          const a = JSON.parse(fs.readFileSync(path,'utf8'));
          if (!a.includePlatforms || a.includePlatforms.indexOf('Editor') < 0) {
            console.error('asmdef must be Editor-only'); process.exit(1);
          }
          console.log('asmdef OK: ' + a.name);
          "

      - name: Guard against runtime code in this package
        run: |
          # 이 패키지는 에디터 전용이다. Runtime 폴더가 생기면 규약이 깨진 것이다.
          if [ -d Runtime ]; then echo "이 패키지에는 Runtime 폴더가 있으면 안 됩니다"; exit 1; fi
          echo "editor-only OK"

      - name: Check every imported file has a .meta
        run: |
          missing=0
          for f in $(git ls-files | grep -v '\.meta$' | grep -v '^\.github/' | grep -v '~/'); do
            if [ ! -e "$f.meta" ]; then echo "meta 없음: $f"; missing=1; fi
          done
          if [ "$missing" = "1" ]; then exit 1; fi
          echo "meta 파일 전부 있음"

      - name: Check for duplicate meta GUIDs
        run: |
          dups=$(git ls-files '*.meta' | xargs grep -h '^guid:' | sort | uniq -d)
          if [ -n "$dups" ]; then echo "GUID 중복: $dups"; exit 1; fi
          echo "GUID 중복 없음"

      - name: Note editor layer verification
        run: |
          # Editor는 UnityEditor 어셈블리가 필요한데 그것은 재배포할 수 없다.
          # 컴파일 검증은 로컬 게이트다: ./Tools~/compile-check/run.sh
          if [ ! -x Tools~/compile-check/run.sh ]; then
            echo "Tools~/compile-check/run.sh가 없거나 실행 권한이 없습니다"; exit 1
          fi
          echo "에디터 컴파일 게이트가 제자리에 있습니다 (로컬 실행)"
```

- [ ] **Step 8: `.meta` 파일을 만든다**

`.github`와 `Tools~` 아래를 제외한 모든 파일과 폴더에 만든다. 형식은 런타임 패키지의 것과 같다. GUID는 32자리 소문자 16진수로 새로 만든다.

| 대상 | 임포터 |
|---|---|
| 폴더 | `folderAsset: yes` + `DefaultImporter` |
| `.cs` | `MonoImporter` (`serializedVersion: 2`, `defaultReferences: []`, `executionOrder: 0`, `icon: {instanceID: 0}`) |
| `.asmdef` | `AssemblyDefinitionImporter` |
| `package.json` | `PackageManifestImporter` |
| `.md`, `.gitignore` | `TextScriptImporter` |

- [ ] **Step 9: 게이트가 통과하고 실제로 오류를 잡는지 확인한다**

```bash
./Tools~/compile-check/run.sh
```

기대: `에디터 계층 컴파일 통과.`

일부러 깨뜨려 본다 — 통과만 확인하면 게이트가 아무것도 컴파일하지 않아도 모른다:

```bash
echo 'using UnityEditor; class Broken { void X() { EditorWindow w = "nope"; } }' > Editor/_Broken.cs
./Tools~/compile-check/run.sh; echo "exit=$?"
rm Editor/_Broken.cs
./Tools~/compile-check/run.sh
```

기대: `error CS0029`와 `exit=1`, 되돌린 뒤 통과.

게이트가 런타임 패키지까지 실제로 훑는지도 확인한다 — `MotionEditorPaths`만 컴파일하고 있으면 아무 의미가 없다:

```bash
grep -c 'Compile Include' Tools~/compile-check/UiMotion.Editor.Compile.csproj
```

기대: 3 (Core, Runtime, Editor).

- [ ] **Step 10: 커밋**

```bash
git add -A
git commit -m "build: 에디터 패키지 뼈대와 컴파일 게이트 추가"
```

---

### 여기서부터의 코드 밀도에 대해

Task 5부터는 **논리가 있는 부분은 코드를 전부 적고, 순수한 그리기 코드는 구조와 요구사항만 적는다.** 근거는 검증 방식이다 — 논리는 틀리면 조용히 잘못 동작하는데 테스트가 없으므로 계획이 정확해야 하고, 레이아웃과 스타일은 틀리면 눈에 바로 보이므로 구현자가 판단하는 편이 낫다.

구현자에게: **그리기 코드는 당신의 판단으로 만든다.** 다만 아래 요구사항은 전부 만족해야 한다.

---

### Task 5: 노드 카탈로그

팔레트, Node Doctor, 그래프 창의 노드 검색이 전부 이것을 쓴다.

**Files:** `Editor/Catalog/MotionNodeEntry.cs`, `Editor/Catalog/MotionNodeCatalog.cs`

- [ ] **Step 1: 항목 타입**

`Editor/Catalog/MotionNodeEntry.cs`:

```csharp
using System;

namespace Juahn.UiMotion.Editor
{
    /// <summary>카탈로그에 오른 노드 하나.</summary>
    public sealed class MotionNodeEntry
    {
        public Type Type;

        /// <summary>팔레트에 뜨는 이름. 어트리뷰트가 없으면 타입 이름에서 만든다.</summary>
        public string Name;

        public string Category;
        public string Summary;
        public string Sample;

        /// <summary><c>[MotionNode]</c>가 달려 있는가.</summary>
        public bool HasAttribute;

        /// <summary>
        /// <c>[Serializable]</c>이 달려 있는가. 없으면 <c>SerializeReference</c>가 저장하지 못해
        /// 그래프를 다시 열었을 때 <b>노드가 사라진다</b>. 조용히 일어나므로 반드시 잡아야 한다.
        /// </summary>
        public bool IsSerializable;

        /// <summary>설명과 예시를 모두 갖췄는가.</summary>
        public bool HasDocs;

        /// <summary>배포해도 되는가. 팔레트의 "미검증" 섹션 여부를 정한다.</summary>
        public bool IsVerified => HasAttribute && HasDocs && IsSerializable;

        public bool IsFlow;
    }
}
```

- [ ] **Step 2: 카탈로그**

`Editor/Catalog/MotionNodeCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 프로젝트의 모든 노드 타입을 모은다. 도메인 리로드마다 다시 훑는다.
    ///
    /// <c>TypeCache</c>를 쓰는 이유는 Unity가 이미 만들어 둔 색인이라
    /// 어셈블리를 직접 훑는 것보다 훨씬 빠르기 때문이다.
    /// </summary>
    [InitializeOnLoad]
    public static class MotionNodeCatalog
    {
        private static readonly List<MotionNodeEntry> Entries = new List<MotionNodeEntry>();
        private static readonly List<string> CategoryNames = new List<string>();
        private static readonly Dictionary<Type, MotionNodeEntry> ByType = new Dictionary<Type, MotionNodeEntry>();

        public const string UncategorizedName = "Uncategorized";

        static MotionNodeCatalog()
        {
            Refresh();
        }

        public static IReadOnlyList<MotionNodeEntry> All => Entries;

        /// <summary>카테고리 이름. 알파벳 순이고 <see cref="UncategorizedName"/>이 항상 마지막이다.</summary>
        public static IReadOnlyList<string> Categories => CategoryNames;

        public static MotionNodeEntry Find(Type nodeType)
        {
            if (nodeType == null)
            {
                return null;
            }

            MotionNodeEntry entry;
            return ByType.TryGetValue(nodeType, out entry) ? entry : null;
        }

        /// <summary>새 인스턴스를 만든다. 만들 수 없으면 null.</summary>
        public static MotionNodeBase Create(MotionNodeEntry entry)
        {
            if (entry == null || entry.Type == null)
            {
                return null;
            }

            return Activator.CreateInstance(entry.Type) as MotionNodeBase;
        }

        public static void Refresh()
        {
            Entries.Clear();
            ByType.Clear();
            CategoryNames.Clear();

            var categories = new HashSet<string>(StringComparer.Ordinal);
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<MotionNodeBase>();

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];

                // 추상 베이스(MotionEffectNode, UnityEffectNode, SubGraphNode 등)는 노드가 아니다.
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                // SerializeReference도 그래프 창도 매개변수 없는 생성자를 요구한다.
                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                MotionNodeEntry entry = Describe(type);
                Entries.Add(entry);
                ByType[type] = entry;
                categories.Add(entry.Category);
            }

            Entries.Sort(CompareEntries);

            foreach (string category in categories)
            {
                CategoryNames.Add(category);
            }

            CategoryNames.Sort(CompareCategories);
        }

        private static MotionNodeEntry Describe(Type type)
        {
            var attribute = (MotionNodeAttribute)Attribute.GetCustomAttribute(type, typeof(MotionNodeAttribute));

            var entry = new MotionNodeEntry
            {
                Type = type,
                HasAttribute = attribute != null,
                IsSerializable = Attribute.IsDefined(type, typeof(SerializableAttribute)),
                IsFlow = typeof(MotionFlowNode).IsAssignableFrom(type),
            };

            if (attribute == null)
            {
                entry.Name = Humanize(type.Name);
                entry.Category = UncategorizedName;
                entry.HasDocs = false;
                return entry;
            }

            entry.Name = string.IsNullOrWhiteSpace(attribute.Name) ? Humanize(type.Name) : attribute.Name;
            entry.Category = string.IsNullOrWhiteSpace(attribute.Category) ? UncategorizedName : attribute.Category;
            entry.Summary = attribute.Summary;
            entry.Sample = attribute.Sample;
            entry.HasDocs = attribute.IsVerified;
            return entry;
        }

        /// <summary>"PunchScaleNode" -> "Punch Scale". 어트리뷰트가 없는 노드의 표시용이다.</summary>
        private static string Humanize(string typeName)
        {
            if (typeName.EndsWith("Node", StringComparison.Ordinal) && typeName.Length > 4)
            {
                typeName = typeName.Substring(0, typeName.Length - 4);
            }

            var builder = new System.Text.StringBuilder(typeName.Length + 4);
            for (int i = 0; i < typeName.Length; i++)
            {
                if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(typeName[i]);
            }

            return builder.ToString();
        }

        private static int CompareEntries(MotionNodeEntry a, MotionNodeEntry b)
        {
            int byCategory = CompareCategories(a.Category, b.Category);
            return byCategory != 0 ? byCategory : string.CompareOrdinal(a.Name, b.Name);
        }

        private static int CompareCategories(string a, string b)
        {
            // 분류되지 않은 것은 언제나 맨 뒤로. 대개 아직 정리되지 않은 실험용이다.
            bool aLast = a == UncategorizedName;
            bool bLast = b == UncategorizedName;

            if (aLast != bLast)
            {
                return aLast ? 1 : -1;
            }

            return string.CompareOrdinal(a, b);
        }
    }
}
```

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
```

`.meta`를 만든 뒤 `git commit -m "feat: 노드 카탈로그 추가"`.

---

### Task 6: MotionPlayer 인스펙터와 자동 바인딩

이 태스크가 시스템을 실제로 쓸 수 있게 만드는 지점이다. 지금은 슬롯을 인스펙터에서 손으로 채워야 하는데, 슬롯 목록이 파생값이라 기본 인스펙터로는 무엇을 채워야 하는지도 보이지 않는다.

**Files:** `Editor/Inspector/MotionPlayerEditor.cs`, `Editor/Inspector/SlotAutoBinder.cs`

- [ ] **Step 1: 자동 바인딩 로직**

논리가 있는 부분이므로 코드를 전부 적는다.

`Editor/Inspector/SlotAutoBinder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 슬롯 이름과 같은 이름의 자식을 계층에서 찾아 채운다.
    ///
    /// <b>버튼을 눌렀을 때만 돈다.</b> 런타임 경로 탐색을 쓰지 않는 이유는, 나중에 오브젝트
    /// 이름이 바뀌었을 때 연출이 조용히 사라지는 대신 인스펙터에서 눈에 보이게 하기 위해서다.
    /// 결과가 인스펙터에 그대로 박히므로 diff에도 남는다.
    /// </summary>
    public static class SlotAutoBinder
    {
        public struct Result
        {
            public int Bound;
            public int AlreadyBound;
            public int NotFound;
        }

        /// <summary>
        /// <paramref name="overwrite"/>가 false면 이미 채워진 슬롯은 건드리지 않는다.
        /// 손으로 예외를 잡아 둔 것을 버튼 한 번에 날리면 안 되기 때문이다.
        /// </summary>
        public static Result Bind(MotionPlayer player, bool overwrite)
        {
            var result = new Result();

            if (player == null || player.Graph == null)
            {
                return result;
            }

            IReadOnlyList<SlotDeclaration> slots = player.Graph.Slots;
            var lookup = new Dictionary<string, Transform>();
            Collect(player.transform, lookup);

            for (int i = 0; i < slots.Count; i++)
            {
                SlotDeclaration slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Name))
                {
                    continue;
                }

                if (!overwrite && FindExisting(player, slot.Name) != null)
                {
                    result.AlreadyBound++;
                    continue;
                }

                Transform found;
                if (!lookup.TryGetValue(slot.Name, out found))
                {
                    result.NotFound++;
                    continue;
                }

                Object target = Coerce(found, slot.RequiredType);
                if (target == null)
                {
                    result.NotFound++;
                    continue;
                }

                player.Bind(slot.Name, target);
                result.Bound++;
            }

            return result;
        }

        private static Object FindExisting(MotionPlayer player, string slotName)
        {
            IReadOnlyList<SlotBinding> bindings = player.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Name == slotName && bindings[i].Target != null)
                {
                    return bindings[i].Target;
                }
            }

            return null;
        }

        /// <summary>
        /// 계층을 훑어 이름 -> Transform 표를 만든다.
        ///
        /// <b>같은 이름이 둘 이상이면 먼저 만난 것이 이긴다</b>(너비 우선이라 얕은 쪽이 먼저다).
        /// 사람이 그 이름으로 슬롯을 만들었다면 대개 가까운 쪽을 뜻한다. 애매한 경우는
        /// 인스펙터가 결과를 보여 주므로 사람이 확인할 수 있다.
        /// </summary>
        private static void Collect(Transform root, Dictionary<string, Transform> into)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();

                if (!into.ContainsKey(current.name))
                {
                    into[current.name] = current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }
        }

        /// <summary>
        /// 요구 타입으로 바꾼다. 요구 타입이 없으면(어트리뷰트를 안 단 슬롯) Transform 그대로.
        /// </summary>
        private static Object Coerce(Transform found, System.Type requiredType)
        {
            if (requiredType == null)
            {
                return found;
            }

            if (requiredType == typeof(GameObject))
            {
                return found.gameObject;
            }

            if (requiredType.IsInstanceOfType(found))
            {
                return found;
            }

            return found.GetComponent(requiredType);
        }
    }
}
```

- [ ] **Step 2: 인스펙터**

`Editor/Inspector/MotionPlayerEditor.cs` — `[CustomEditor(typeof(MotionPlayer))]`. 그리기 코드는 구현자가 만든다. 요구사항:

1. **그래프 필드.** 값이 바뀌면 `SyncBindings()`를 부른다. 안 부르면 슬롯 목록이 낡은 채 남는다.
2. **슬롯 목록.** `player.Graph.Slots`를 훑어 각 슬롯마다 `EditorGUILayout.ObjectField`를 그린다. `SlotDeclaration.RequiredType`으로 타입을 거르고, 그 타입이 null이면 `Object`를 받는다. 비어 있는 슬롯은 눈에 띄게 표시한다 — 이것이 "연출이 조용히 안 나오는" 가장 흔한 원인이다.
3. **고아 바인딩.** 현재 그래프의 슬롯 목록에 없는 바인딩을 별도 섹션에 "이 그래프에 없는 슬롯"으로 표시한다. **조용히 지우지 않는다.** 그래프를 잘못 바꿨다가 되돌렸을 때 손으로 채운 참조가 사라져 있으면 안 된다.
4. **자동 바인딩 버튼 둘.** "비어 있는 것만 채우기"(`overwrite: false`)와 "전부 다시 채우기"(`overwrite: true`). 결과를 `Result`의 숫자로 알려 준다.
5. **트리거 시험 재생.** 그래프가 선언한 트리거마다 버튼. 플레이 모드면 `player.Fire(name)`을, 아니면 `MotionPreviewDriver`(Task 9)에 넘긴다. 그래프가 없으면 이 구역을 통째로 감춘다.
6. **검사 결과 요약.** `MotionGraphValidator.Validate(player.Graph)`의 Error/Warning 개수를 한 줄로 보여 주고, 있으면 그래프 에셋을 선택할 수 있게 한다.

**반드시 지킬 것**

- 값을 바꾸기 전에 `Undo.RecordObject(player, "...")`를 부른다. 되돌리기가 안 되면 자동 바인딩 버튼이 위험한 버튼이 된다.
- 바꾼 뒤 `EditorUtility.SetDirty(player)`를, 프리팹이면 `PrefabUtility.RecordPrefabInstancePropertyModifications(player)`도 부른다. 안 부르면 씬을 저장해도 값이 남지 않는다.
- `player.Bind`와 `SyncBindings`는 `SerializedProperty`를 거치지 않고 필드를 직접 바꾸므로, 부른 뒤 `serializedObject.Update()`로 다시 읽어야 인스펙터가 옛 값을 덮어쓰지 않는다.

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

---

### Task 7: 그래프 에셋 인스펙터

그래프 에셋을 눌렀을 때 기본 인스펙터는 `[SerializeReference]` 배열을 날것으로 보여 줘 아무 쓸모가 없다. 요약과 검사 결과로 바꾼다.

**Files:** `Editor/Inspector/MotionGraphInspector.cs`, `Editor/Inspector/MotionIssueDrawer.cs`

- [ ] **Step 1: 검사 결과 그리기 헬퍼**

인스펙터, Node Doctor, 그래프 창이 전부 같은 방식으로 문제를 그린다. 한 곳에 둔다.

`Editor/Inspector/MotionIssueDrawer.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>검사 결과를 그리는 공통 코드.</summary>
    public static class MotionIssueDrawer
    {
        public static MessageType ToMessageType(MotionIssueLevel level)
        {
            switch (level)
            {
                case MotionIssueLevel.Error:
                    return MessageType.Error;

                case MotionIssueLevel.Warning:
                    return MessageType.Warning;

                default:
                    return MessageType.Info;
            }
        }

        /// <summary>
        /// 문제 목록을 심각한 것부터 그린다.
        ///
        /// <paramref name="limit"/>은 인스펙터가 수백 줄로 늘어나는 것을 막는다 —
        /// 배선이 크게 망가진 그래프는 문제가 노드 수만큼 나온다.
        /// </summary>
        public static void Draw(IReadOnlyList<MotionGraphIssue> issues, int limit = 12)
        {
            if (issues == null || issues.Count == 0)
            {
                EditorGUILayout.HelpBox("문제가 없습니다.", MessageType.Info);
                return;
            }

            var sorted = new List<MotionGraphIssue>(issues);
            sorted.Sort(CompareBySeverity);

            int shown = sorted.Count < limit ? sorted.Count : limit;
            for (int i = 0; i < shown; i++)
            {
                EditorGUILayout.HelpBox(sorted[i].ToString(), ToMessageType(sorted[i].Level));
            }

            if (sorted.Count > shown)
            {
                EditorGUILayout.LabelField("그 밖에 " + (sorted.Count - shown) + "건 더 있습니다.");
            }
        }

        /// <summary>Error, Warning, Info 순. 같은 수준이면 노드 순.</summary>
        private static int CompareBySeverity(MotionGraphIssue a, MotionGraphIssue b)
        {
            if (a.Level != b.Level)
            {
                return b.Level.CompareTo(a.Level);
            }

            return a.Node.Value.CompareTo(b.Node.Value);
        }

        /// <summary>"오류 2, 경고 5" 같은 한 줄 요약. 문제가 없으면 빈 문자열.</summary>
        public static string Summarize(IReadOnlyList<MotionGraphIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                return "";
            }

            int errors = 0;
            int warnings = 0;
            int infos = 0;

            for (int i = 0; i < issues.Count; i++)
            {
                switch (issues[i].Level)
                {
                    case MotionIssueLevel.Error:
                        errors++;
                        break;

                    case MotionIssueLevel.Warning:
                        warnings++;
                        break;

                    default:
                        infos++;
                        break;
                }
            }

            var parts = new List<string>();
            if (errors > 0)
            {
                parts.Add("오류 " + errors);
            }

            if (warnings > 0)
            {
                parts.Add("경고 " + warnings);
            }

            if (infos > 0)
            {
                parts.Add("정보 " + infos);
            }

            return string.Join(", ", parts.ToArray());
        }
    }
}
```

- [ ] **Step 2: 인스펙터**

`Editor/Inspector/MotionGraphInspector.cs` — `[CustomEditor(typeof(MotionGraph))]`. 요구사항:

1. **요약 한 줄** — 노드 수, 간선 수, 트리거 수, 슬롯 수.
2. **트리거 목록** — 이름, 재발사 정책, 진입 노드. 진입점이 없는 트리거는 눈에 띄게 표시한다.
3. **슬롯 목록** — 이름과 요구 타입. **"이 목록은 노드에서 계산됩니다. 직접 고칠 수 없습니다."** 를 명시한다. 이것이 사람이 가장 많이 헷갈릴 지점이다.
4. **`UseUnscaledTime` 토글** — `SerializedProperty`로 그린다.
5. **검사 결과** — `MotionGraphValidator.Validate`를 부르고 `MotionIssueDrawer.Draw`로 그린다. **매 `OnInspectorGUI`마다 다시 검사하지 않는다** — 인스펙터는 초당 여러 번 다시 그려진다. 검사 결과를 캐시하고 `EditorApplication.timeSinceStartup` 기준으로 최소 0.25초 간격, 또는 `OnEnable`과 명시적 "다시 검사" 버튼에서만 갱신한다.
6. **"그래프 창에서 열기" 버튼** — Task 12의 창을 연다. 그 태스크 전까지는 만들지 않는다.

**반드시 지킬 것**

- `_nodes`는 `[SerializeReference]` 배열이라 기본 그리기가 매우 느리고 쓸모없다. `DrawDefaultInspector()`를 부르지 않는다.

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

---

### Task 8: Node Doctor

스펙 7.3의 계약을 강제하는 도구다. **배포된 노드는 100% 문서화됨을 보장한다** — 설명이 있고, 예시 그래프가 있고, 그 예시가 실제로 존재한다.

**Files:** `Editor/Doctor/MotionNodeDoctor.cs`, `Editor/Doctor/MotionNodeDoctorWindow.cs`

- [ ] **Step 1: 검사 로직 (논리이므로 코드를 전부 적는다)**

`Editor/Doctor/MotionNodeDoctor.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 노드의 문서화 계약을 검사한다.
    ///
    /// 설명을 별도 문서가 아니라 어트리뷰트에, 예시를 스크린샷이 아니라 실행 가능한 그래프
    /// 에셋에 두는 이유가 여기 있다 — 둘 다 기계가 검사할 수 있어서 썩지 않는다.
    ///
    /// 미검증 노드도 <b>그래프에서 쓸 수는 있다</b>. 리팩터 도중의 실험용 노드를 막지 않기
    /// 위해서다. 대신 이 검사를 배포 파이프라인에서 돌려 하나라도 있으면 실패시킨다.
    /// </summary>
    public static class MotionNodeDoctor
    {
        public struct Row
        {
            public MotionNodeEntry Entry;

            /// <summary><c>[MotionNode].Sample</c>이 가리키는 에셋을 실제로 열 수 있는가.</summary>
            public bool SampleExists;

            /// <summary>같은 이름의 예시가 둘 이상이면 어느 것이 쓰일지 알 수 없다.</summary>
            public bool SampleIsAmbiguous;

            public string SamplePath;

            public bool IsHealthy => Entry.IsVerified && SampleExists && !SampleIsAmbiguous;
        }

        /// <summary>모든 노드를 검사한다. 카탈로그 순서를 유지한다.</summary>
        public static List<Row> Inspect()
        {
            var rows = new List<Row>();
            IReadOnlyList<MotionNodeEntry> entries = MotionNodeCatalog.All;

            for (int i = 0; i < entries.Count; i++)
            {
                MotionNodeEntry entry = entries[i];

                int matches;
                string path = FindSample(entry.Sample, out matches);

                rows.Add(new Row
                {
                    Entry = entry,
                    SamplePath = path,
                    SampleExists = path != null,
                    SampleIsAmbiguous = matches > 1,
                });
            }

            return rows;
        }

        /// <summary>
        /// 이름으로 예시 그래프를 찾는다. 프로젝트 전체(패키지 포함)를 뒤진다.
        ///
        /// <b>왜 정해진 경로가 아니라 이름 검색인가</b> — 프로젝트가 자기 노드를 추가하면
        /// 그 예시는 그 프로젝트의 <c>Assets</c> 어딘가에 있지 이 패키지 안에 있지 않다.
        /// 규약을 경로가 아니라 이름으로 두면 어디에 두든 동작한다.
        ///
        /// <paramref name="matches"/>가 1보다 크면 같은 이름이 여러 개라 어느 것이 쓰일지
        /// 알 수 없다는 뜻이다. 검사에서 잡아야 한다.
        /// </summary>
        public static string FindSample(string sampleName, out int matches)
        {
            matches = 0;

            if (string.IsNullOrWhiteSpace(sampleName))
            {
                return null;
            }

            // t: 필터가 타입을 좁히고 이름은 부분 일치이므로 정확 일치를 다시 거른다.
            string[] guids = AssetDatabase.FindAssets("t:MotionGraph " + sampleName);
            string found = null;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileNameWithoutExtension(path) != sampleName)
                {
                    continue;
                }

                matches++;
                if (found == null)
                {
                    found = path;
                }
            }

            return found;
        }

        /// <summary>예시 그래프를 연다. 없으면 null.</summary>
        public static MotionGraph LoadSample(string sampleName)
        {
            int matches;
            string path = FindSample(sampleName, out matches);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<MotionGraph>(path);
        }

        /// <summary>
        /// 배치 실행 진입점. 문제가 하나라도 있으면 종료 코드 1로 죽는다.
        ///
        ///   Unity -batchmode -quit -projectPath &lt;path&gt; \
        ///     -executeMethod Juahn.UiMotion.Editor.MotionNodeDoctor.RunBatch
        ///
        /// <c>-quit</c>이 있어도 <c>EditorApplication.Exit</c>을 직접 부르는 이유는
        /// 종료 코드를 우리가 정해야 CI가 실패를 알아채기 때문이다.
        /// </summary>
        public static void RunBatch()
        {
            MotionNodeCatalog.Refresh();
            List<Row> rows = Inspect();

            int broken = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row.IsHealthy)
                {
                    continue;
                }

                broken++;
                Debug.LogError("[Node Doctor] " + row.Entry.Type.FullName + ": " + Explain(row));
            }

            Debug.Log("[Node Doctor] 노드 " + rows.Count + "개 중 " + (rows.Count - broken) + "개 통과.");
            EditorApplication.Exit(broken == 0 ? 0 : 1);
        }

        /// <summary>무엇이 빠졌는지 사람이 읽을 문장으로.</summary>
        public static string Explain(Row row)
        {
            var problems = new List<string>();

            if (!row.Entry.HasAttribute)
            {
                problems.Add("[MotionNode] 어트리뷰트가 없습니다");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(row.Entry.Summary))
                {
                    problems.Add("Summary가 비어 있습니다");
                }

                if (string.IsNullOrWhiteSpace(row.Entry.Sample))
                {
                    problems.Add("Sample이 비어 있습니다");
                }
                else if (!row.SampleExists)
                {
                    problems.Add("'" + row.Entry.Sample + "'라는 이름의 예시 그래프를 찾을 수 없습니다");
                }
                else if (row.SampleIsAmbiguous)
                {
                    problems.Add("'" + row.Entry.Sample + "' 이름의 예시가 여러 개라 어느 것이 쓰일지 알 수 없습니다");
                }
            }

            if (!row.Entry.IsSerializable)
            {
                // 이것이 가장 위험하다. 그래프를 저장하고 다시 열면 노드가 사라진다.
                problems.Add("[Serializable]이 없어 그래프에 저장되지 않습니다");
            }

            return string.Join(" / ", problems.ToArray());
        }
    }
}
```

- [ ] **Step 2: 창**

`Editor/Doctor/MotionNodeDoctorWindow.cs` — `EditorWindow`. 그리기는 구현자가 만든다. 요구사항:

1. 메뉴 `MotionEditorPaths.MenuRoot + MotionEditorPaths.DoctorWindowTitle`로 연다.
2. 표: 노드 이름 · 카테고리 · 타입 · 설명 있음 · 예시 있음 · 직렬화 가능 · 상태.
3. 통과하지 못한 것을 **위로** 정렬한다. 통과한 것만 있으면 그렇다고 한 줄로 말한다.
4. 행을 누르면 `Explain`의 문장을 보여 준다. 예시 에셋이 있으면 그것을 선택(`Selection.activeObject`)한다.
5. "다시 검사" 버튼 — `MotionNodeCatalog.Refresh()` 후 다시 그린다.
6. 상단에 "노드 N개 중 M개 통과" 요약.

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

---

### Task 9: 인에디터 프리뷰

플레이 모드에 들어가지 않고 그래프를 실제 프리팹 위에서 재생한다. 이것이 없으면 연출 하나를 확인할 때마다 플레이를 눌러야 한다.

**Files:** `Editor/Preview/MotionPreviewDriver.cs`

계획 2에서 `MotionPump`가 `Application.isPlaying`이 아니면 아무것도 하지 않게 만들어 두었고, `MotionPlayer.TickFromPump(unscaled, scaled)`를 public으로 열어 두었다. 이 태스크가 그 자리를 채운다.

- [ ] **Step 1: 프리뷰 드라이버 (논리이므로 코드를 전부 적는다)**

`Editor/Preview/MotionPreviewDriver.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 플레이 모드 밖에서 <see cref="MotionPlayer"/>를 굴린다.
    ///
    /// <b>왜 별도 드라이버가 필요한가</b> — <c>MotionPump</c>는 플레이 중이 아니면 아무것도
    /// 하지 않는다. 씬에 숨은 오브젝트를 남기지 않기 위해서다. 에디터에는 그런 오브젝트를
    /// 만들 수 없으므로 <c>EditorApplication.update</c>가 대신 시간을 넣는다.
    ///
    /// <b>프리팹으로 새어 나가는 것을 막는다.</b> 프리뷰는 대상을 실제로 움직이므로, 멈출 때
    /// 반드시 원래대로 돌려놓아야 한다. <c>MotionPlayer.StopAll()</c>이 스코프를 취소하고
    /// 취소가 등록된 복구를 전부 돌리므로 그것에 기댄다 — 계획 1·2에서 그 보장을 위해
    /// 원상 복구 규약을 만들었다.
    /// </summary>
    [InitializeOnLoad]
    public static class MotionPreviewDriver
    {
        private static readonly List<MotionPlayer> Players = new List<MotionPlayer>();
        private static double _lastTime;

        static MotionPreviewDriver()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
        }

        public static bool IsPreviewing => Players.Count > 0;

        public static bool IsPreviewingPlayer(MotionPlayer player)
        {
            return player != null && Players.Contains(player);
        }

        /// <summary>
        /// 트리거를 발사하고 그 플레이어를 프리뷰 목록에 넣는다.
        /// 플레이 모드에서는 아무것도 하지 않는다 — 그때는 진짜 펌프가 돈다.
        /// </summary>
        public static void Fire(MotionPlayer player, string trigger)
        {
            if (player == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!Players.Contains(player))
            {
                Players.Add(player);
            }

            if (Players.Count == 1)
            {
                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }

            player.Fire(trigger);
        }

        /// <summary>이 플레이어의 프리뷰를 멈추고 대상을 원래대로 돌린다.</summary>
        public static void Stop(MotionPlayer player)
        {
            if (player == null)
            {
                return;
            }

            // 취소가 등록된 원상 복구를 전부 돌린다. 이것이 프리뷰가 프리팹에
            // 새어 나가지 않는 유일한 이유다.
            player.StopAll();

            Players.Remove(player);

            if (Players.Count == 0)
            {
                EditorApplication.update -= Tick;
            }
        }

        public static void StopAll()
        {
            for (int i = Players.Count - 1; i >= 0; i--)
            {
                MotionPlayer player = Players[i];
                if (player != null)
                {
                    player.StopAll();
                }
            }

            Players.Clear();
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastTime);
            _lastTime = now;

            // 에디터의 update는 창이 가려지면 몇 초씩 건너뛴다. 그대로 넣으면
            // 연출이 통째로 끝나 버려 프리뷰가 아무것도 보여 주지 못한다.
            if (delta > 0.1f)
            {
                delta = 0.1f;
            }

            for (int i = Players.Count - 1; i >= 0; i--)
            {
                MotionPlayer player = Players[i];

                if (player == null)
                {
                    Players.RemoveAt(i);
                    continue;
                }

                // 프리뷰는 timeScale과 무관하다. 둘 다 같은 값을 준다.
                player.TickFromPump(delta, delta);
            }

            if (Players.Count == 0)
            {
                EditorApplication.update -= Tick;
                return;
            }

            // 씬 뷰가 스스로 다시 그리지 않으므로 직접 요청한다.
            SceneView.RepaintAll();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // 플레이 모드에 들어가거나 나올 때 프리뷰가 남아 있으면 대상이
            // 중간 상태로 굳은 채 저장될 수 있다.
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                StopAll();
            }
        }
    }
}
```

- [ ] **Step 2: MotionPlayer 인스펙터에 프리뷰를 연결한다**

Task 6의 트리거 시험 재생 버튼이 플레이 모드가 아니면 `MotionPreviewDriver.Fire(player, name)`을 부르게 한다. 프리뷰 중이면 "멈추기" 버튼을 함께 보여 주고 `MotionPreviewDriver.Stop(player)`을 부른다.

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

---

### Task 10: 예시 그래프 생성

계획 2가 만든 13개 노드는 전부 `[MotionNode(Sample = "...")]`를 선언했지만 **그 에셋은 아직 없다.** Node Doctor를 지금 돌리면 13개 전부 실패한다. 그것을 채운다.

이 태스크가 스펙 7.1의 "작동 예시를 스크린샷이 아니라 실행 가능한 그래프 에셋에 둔다"를 실현한다.

**Files:** `Editor/Samples/MotionSampleGenerator.cs`, `Editor/Samples/Nodes/*.asset` (생성됨)

- [ ] **Step 1: 생성기 (논리이므로 코드를 전부 적는다)**

`Editor/Samples/MotionSampleGenerator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 노드마다 "이 노드 하나가 하는 일"을 보여 주는 최소 그래프를 만든다.
    ///
    /// 손으로 13개를 만들고 유지하는 대신 코드가 만든다. 노드가 늘어나면 여기 한 줄을
    /// 더하면 되고, 파라미터 기본값이 바뀌어도 다시 생성하면 예시가 따라온다.
    ///
    /// <b>덮어쓰지 않는다.</b> 사람이 예시를 손봤을 수 있으므로 이미 있는 것은 건너뛴다.
    /// 다시 만들고 싶으면 에셋을 지우고 다시 돌린다.
    /// </summary>
    public static class MotionSampleGenerator
    {
        [MenuItem(MotionEditorPaths.MenuRoot + "Generate Missing Samples")]
        public static void GenerateMissing()
        {
            int made = Generate(false);
            Debug.Log("[UI Motion] 예시 그래프 " + made + "개를 만들었습니다.");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 카탈로그의 모든 노드에 대해 예시를 만든다.
        /// 만든 개수를 돌려준다.
        /// </summary>
        public static int Generate(bool overwrite)
        {
            string folder = ResolveSamplesFolder();
            if (folder == null)
            {
                Debug.LogError("[UI Motion] 예시를 놓을 폴더를 찾지 못했습니다.");
                return 0;
            }

            EnsureFolder(folder);

            int made = 0;
            IReadOnlyList<MotionNodeEntry> entries = MotionNodeCatalog.All;

            for (int i = 0; i < entries.Count; i++)
            {
                MotionNodeEntry entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry.Sample))
                {
                    continue;
                }

                int matches;
                if (!overwrite && MotionNodeDoctor.FindSample(entry.Sample, out matches) != null)
                {
                    continue;
                }

                MotionGraph graph = BuildSample(entry);
                if (graph == null)
                {
                    continue;
                }

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + entry.Sample + "." + MotionEditorPaths.GraphAssetExtension);

                AssetDatabase.CreateAsset(graph, path);
                made++;
            }

            AssetDatabase.SaveAssets();
            return made;
        }

        /// <summary>
        /// 노드 하나짜리 예시. <c>Start</c> 트리거가 그 노드를 가리킨다.
        ///
        /// 유지 연출 노드(<c>Float</c>·<c>Bounce</c>)는 <c>Loop</c>에 문다 — <c>Start</c>에
        /// 물면 검사기가 "Loop가 한 번 돌고 끝난다"가 아니라 다른 문제를 내고, 무엇보다
        /// 그 노드를 실제로 쓰는 방식이 아니다. 예시는 올바른 사용법을 보여야 한다.
        /// </summary>
        private static MotionGraph BuildSample(MotionNodeEntry entry)
        {
            MotionNodeBase node = MotionNodeCatalog.Create(entry);
            if (node == null)
            {
                return null;
            }

            var graph = ScriptableObject.CreateInstance<MotionGraph>();
            NodeId id = graph.AddNode(node);

            string trigger = node.BlocksChildren ? MotionRuntime.LoopTrigger : MotionRuntime.StartTrigger;
            graph.SetTrigger(trigger, id);
            graph.SetNodePosition(id, new Vector2(120f, 80f));

            return graph;
        }

        /// <summary>
        /// 이 패키지 안의 예시 폴더를 프로젝트 상대 경로로 돌려준다.
        ///
        /// 패키지가 <c>Packages/</c>에 임베드돼 있든 캐시에서 왔든 동작해야 하므로,
        /// 이 스크립트 자신의 에셋 경로에서 거슬러 올라가 패키지 루트를 찾는다.
        /// </summary>
        private static string ResolveSamplesFolder()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript MotionSampleGenerator");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith("/MotionSampleGenerator.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                // .../Editor/Samples/MotionSampleGenerator.cs -> .../
                int editorIndex = path.LastIndexOf("/Editor/", StringComparison.Ordinal);
                if (editorIndex < 0)
                {
                    continue;
                }

                return path.Substring(0, editorIndex + 1) + MotionEditorPaths.SamplesFolder;
            }

            return null;
        }

        /// <summary>
        /// 중간 폴더까지 만든다. <c>AssetDatabase.CreateFolder</c>는 부모가 없으면 실패한다.
        /// </summary>
        private static void EnsureFolder(string projectRelativePath)
        {
            if (AssetDatabase.IsValidFolder(projectRelativePath))
            {
                return;
            }

            string[] parts = projectRelativePath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
```

- [ ] **Step 2: 예시를 만들고 Node Doctor로 확인한다**

Unity에서 프로젝트를 열고 `Window > UI Motion > Generate Missing Samples`를 누른 뒤 Node Doctor를 연다.

기대: 13개 노드 전부 통과. 통과하지 못하는 것이 있으면 그 이유가 곧 계획 2가 남긴 빚이다.

**이 확인은 Unity를 열어야 한다.** 컴파일 게이트로는 대신할 수 없다. 배치로도 돌릴 수 있다:

```bash
"/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit -projectPath <프로젝트> \
  -executeMethod Juahn.UiMotion.Editor.MotionNodeDoctor.RunBatch
echo "exit=$?"
```

- [ ] **Step 3: `.meta`를 만들고 커밋한다**

생성된 `.asset`에는 Unity가 `.meta`를 만들어 준다. 커밋에 함께 넣는다.

---

### Task 11: 그래프 창 — 뼈대와 왕복

가장 큰 태스크라 셋으로 나눈다. 이 태스크는 **읽고 쓰는 것이 정확히 왕복하는지**만 다룬다. 편집 기능은 다음 태스크다.

**Files:** `Editor/Graph/MotionGraphWindow.cs`, `Editor/Graph/MotionGraphViewImpl.cs`, `Editor/Graph/MotionNodeView.cs`

- [ ] **Step 1: 노드 뷰**

`Editor/Graph/MotionNodeView.cs` — `UnityEditor.Experimental.GraphView.Node` 파생.

들고 있어야 할 것:
- `public NodeId Id;`
- `public MotionNodeBase Model;`
- `public Port Input;` — `Capacity.Multi`, `Direction.Input`
- `public Port Output;` — `Capacity.Multi`, `Direction.Output`

요구사항:
1. 포트 타입은 **흐름 하나뿐**이다. `InstantiatePort(Orientation.Horizontal, dir, Port.Capacity.Multi, typeof(MotionNodeBase))`로 만들고 `portName`은 비운다. 값 배선은 스펙 범위 밖이라 타입을 나누지 않는다.
2. 제목은 카탈로그의 `Name`. 없으면 타입 이름.
3. **미검증 노드는 눈에 띄게 표시한다** — 제목 옆 배지나 색. 팔레트에서만 격리하면 이미 그래프에 들어간 미검증 노드를 알 수 없다.
4. **끝나지 않는 노드(`BlocksChildren`)는 출력 포트를 만들지 않는다.** 자식을 달 수 없게 만드는 것이 경고보다 낫다 — 애초에 배선이 불가능해진다.
5. 검사 결과 배지를 붙일 자리를 둔다(`public void SetIssues(IReadOnlyList<MotionGraphIssue> mine)`).

- [ ] **Step 2: 그래프 뷰 (논리이므로 코드를 전부 적는다)**

`Editor/Graph/MotionGraphViewImpl.cs`의 로드/세이브가 이 태스크의 핵심이다.

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Juahn.UiMotion.Editor
{
    /// <summary>
    /// 그래프 에셋을 그리고 편집한다.
    ///
    /// <b>뷰는 상태를 갖지 않는다.</b> 진실은 언제나 <see cref="MotionGraph"/> 에셋에 있고
    /// 뷰는 그것을 비추기만 한다. 편집은 저작 API를 거쳐 에셋을 바꾸고, 그 다음 다시 읽는다.
    /// 뷰에 따로 모델을 두면 둘이 어긋나는 순간 무엇이 맞는지 알 수 없게 된다.
    /// </summary>
    public sealed class MotionGraphViewImpl : GraphView
    {
        private readonly Dictionary<int, MotionNodeView> _views = new Dictionary<int, MotionNodeView>();

        private MotionGraph _graph;
        private bool _loading;

        public MotionGraphViewImpl()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            var background = new GridBackground();
            Insert(0, background);
            background.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
        }

        public MotionGraph Graph => _graph;

        /// <summary>에셋을 읽어 뷰를 처음부터 다시 만든다.</summary>
        public void Load(MotionGraph graph)
        {
            _loading = true;

            try
            {
                DeleteElements(graphElements);
                _views.Clear();
                _graph = graph;

                if (_graph == null)
                {
                    return;
                }

                IReadOnlyList<NodeId> ids = _graph.NodeIds;

                for (int i = 0; i < ids.Count; i++)
                {
                    CreateView(ids[i]);
                }

                // 노드를 전부 만든 뒤에 간선을 잇는다. 순서를 섞으면 아직 없는
                // 노드를 가리키는 간선에서 죽는다.
                for (int i = 0; i < ids.Count; i++)
                {
                    ConnectChildren(ids[i]);
                }

                RefreshIssues();
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>검사를 다시 돌려 노드 배지를 갱신한다.</summary>
        public void RefreshIssues()
        {
            if (_graph == null)
            {
                return;
            }

            List<MotionGraphIssue> issues = MotionGraphValidator.Validate(_graph);
            var byNode = new Dictionary<int, List<MotionGraphIssue>>();

            for (int i = 0; i < issues.Count; i++)
            {
                if (!issues[i].Node.IsValid)
                {
                    continue;
                }

                List<MotionGraphIssue> list;
                if (!byNode.TryGetValue(issues[i].Node.Value, out list))
                {
                    list = new List<MotionGraphIssue>();
                    byNode[issues[i].Node.Value] = list;
                }

                list.Add(issues[i]);
            }

            foreach (KeyValuePair<int, MotionNodeView> pair in _views)
            {
                List<MotionGraphIssue> mine;
                byNode.TryGetValue(pair.Key, out mine);
                pair.Value.SetIssues(mine);
            }
        }

        private void CreateView(NodeId id)
        {
            MotionNodeBase model = _graph.GetNode(id);

            var view = new MotionNodeView(id, model);
            view.SetPosition(new Rect(_graph.GetNodePosition(id), Vector2.zero));

            AddElement(view);
            _views[id.Value] = view;
        }

        private void ConnectChildren(NodeId parent)
        {
            MotionNodeView parentView;
            if (!_views.TryGetValue(parent.Value, out parentView) || parentView.Output == null)
            {
                return;
            }

            IReadOnlyList<NodeId> children = _graph.GetChildren(parent);

            for (int i = 0; i < children.Count; i++)
            {
                MotionNodeView childView;
                if (!_views.TryGetValue(children[i].Value, out childView) || childView.Input == null)
                {
                    // 결손 노드를 가리키는 간선. 검사기가 이미 오류로 잡았다.
                    continue;
                }

                Edge edge = parentView.Output.ConnectTo(childView.Input);
                AddElement(edge);
            }
        }

        /// <summary>
        /// 흐름 포트만 있으므로 방향과 소유 노드만 본다.
        /// 자기 자신에게 잇는 것은 막는다 — 순환의 가장 흔한 형태다.
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter)
        {
            var compatible = new List<Port>();

            ports.ForEach(delegate(Port candidate)
            {
                if (candidate == start || candidate.node == start.node)
                {
                    return;
                }

                if (candidate.direction == start.direction)
                {
                    return;
                }

                compatible.Add(candidate);
            });

            return compatible;
        }

        /// <summary>
        /// 뷰에서 일어난 변경을 에셋에 반영한다.
        ///
        /// <b>모든 변경이 <c>Undo</c>를 거친다.</b> 그래프 편집은 되돌리기가 안 되면
        /// 쓸 수 없는 도구가 된다.
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // Load가 요소를 지우고 다시 만드는 동안에도 이 콜백이 불린다.
            // 그때 에셋을 건드리면 방금 읽은 것을 도로 지운다.
            if (_loading || _graph == null)
            {
                return change;
            }

            Undo.RecordObject(_graph, "Edit Motion Graph");

            ApplyMoves(change.movedElements);
            ApplyRemovals(change.elementsToRemove);
            ApplyNewEdges(change.edgesToCreate);

            EditorUtility.SetDirty(_graph);
            RefreshIssues();

            return change;
        }

        private void ApplyMoves(List<GraphElement> moved)
        {
            if (moved == null)
            {
                return;
            }

            for (int i = 0; i < moved.Count; i++)
            {
                var view = moved[i] as MotionNodeView;
                if (view != null)
                {
                    _graph.SetNodePosition(view.Id, view.GetPosition().position);
                }
            }
        }

        private void ApplyRemovals(List<GraphElement> removed)
        {
            if (removed == null)
            {
                return;
            }

            for (int i = 0; i < removed.Count; i++)
            {
                var edge = removed[i] as Edge;
                if (edge != null)
                {
                    var from = edge.output.node as MotionNodeView;
                    var to = edge.input.node as MotionNodeView;
                    if (from != null && to != null)
                    {
                        _graph.Unlink(from.Id, to.Id);
                    }

                    continue;
                }

                var view = removed[i] as MotionNodeView;
                if (view != null)
                {
                    // RemoveNode가 이 노드에 닿는 간선과 트리거 진입점까지 정리한다.
                    _graph.RemoveNode(view.Id);
                    _views.Remove(view.Id.Value);
                }
            }
        }

        private void ApplyNewEdges(List<Edge> created)
        {
            if (created == null)
            {
                return;
            }

            for (int i = 0; i < created.Count; i++)
            {
                var from = created[i].output.node as MotionNodeView;
                var to = created[i].input.node as MotionNodeView;

                if (from != null && to != null)
                {
                    _graph.Link(from.Id, to.Id);
                }
            }
        }

        /// <summary>노드를 새로 넣는다. 저작 API가 id를 부여한다.</summary>
        public MotionNodeView AddNode(MotionNodeEntry entry, Vector2 position)
        {
            if (_graph == null || entry == null)
            {
                return null;
            }

            MotionNodeBase model = MotionNodeCatalog.Create(entry);
            if (model == null)
            {
                return null;
            }

            Undo.RecordObject(_graph, "Add Motion Node");

            NodeId id = _graph.AddNode(model);
            _graph.SetNodePosition(id, position);

            EditorUtility.SetDirty(_graph);

            CreateView(id);
            RefreshIssues();

            return _views[id.Value];
        }
    }
}
```

- [ ] **Step 3: 창**

`Editor/Graph/MotionGraphWindow.cs` — `EditorWindow`. 요구사항:

1. `[MenuItem(MotionEditorPaths.MenuRoot + "Graph")]`로 연다.
2. `[OnOpenAsset]`으로 `MotionGraph` 에셋을 더블클릭하면 열린다.
3. `CreateGUI`에서 `MotionGraphViewImpl`을 만들어 `rootVisualElement`에 넣고 `style.flexGrow = 1`.
4. 상단 툴바: 편집 중인 에셋 이름, 저장 버튼(`AssetDatabase.SaveAssets`), 검사 결과 요약(`MotionIssueDrawer.Summarize`).
5. `Undo.undoRedoPerformed`를 구독해 `Load(graph)`로 다시 읽는다. **되돌리기가 뷰에 반영되지 않으면 화면과 에셋이 어긋난다.**
6. 도메인 리로드 후에도 열려 있던 에셋을 다시 연다 — 에셋의 GUID를 `[SerializeField] string`으로 창에 저장해 두고 `OnEnable`에서 복원한다. `MotionGraph` 참조를 직접 들면 리로드 때 사라진다.

- [ ] **Step 4: 왕복을 손으로 확인한다**

컴파일 게이트로는 이 태스크를 검증할 수 없다. Unity에서 확인한다:

1. 그래프 에셋을 만들고 창에서 연다.
2. 노드를 몇 개 넣고 이어 붙이고 옮긴다.
3. 저장하고 창을 닫는다.
4. **다시 연다.** 노드 위치, 간선, 간선 순서가 전부 그대로여야 한다.
5. 되돌리기를 여러 번 누른다. 뷰가 따라와야 한다.
6. 노드를 지운다. 그 노드에 닿던 간선이 사라지고 트리거 진입점이 비워져야 한다.

- [ ] **Step 5: 게이트를 돌리고 커밋한다**

---

### Task 12: 그래프 창 — 팔레트와 간선 순서

**Files:** `Editor/Graph/MotionNodeSearchProvider.cs`, `Editor/Graph/MotionNodePalette.cs`, `MotionGraphViewImpl.cs` 수정

- [ ] **Step 1: 노드 검색 창**

`Editor/Graph/MotionNodeSearchProvider.cs` — `ScriptableObject, ISearchWindowProvider`. GraphView의 빈 곳에서 스페이스나 우클릭을 누르면 뜨는 검색 창이다.

요구사항:
1. `MotionNodeCatalog.All`을 카테고리별로 묶어 트리로 만든다.
2. **미검증 노드는 "미검증" 그룹으로 격리한다.** 스펙 8절의 계약이다. 쓰는 것 자체는 막지 않는다.
3. 항목을 고르면 마우스 위치를 그래프 좌표로 바꿔 `MotionGraphViewImpl.AddNode(entry, position)`을 부른다. 화면 좌표를 그대로 쓰면 스크롤하거나 줌한 상태에서 엉뚱한 곳에 생긴다:

```csharp
        private Vector2 ToGraphPosition(Vector2 screenPosition, EditorWindow window, GraphView view)
        {
            Vector2 inWindow = screenPosition - window.position.position;
            Vector2 inView = window.rootVisualElement.ChangeCoordinatesTo(
                window.rootVisualElement.parent, inWindow);
            return view.contentViewContainer.WorldToLocal(inView);
        }
```

4. 항목에 요약(`entry.Summary`)을 툴팁으로 붙인다.

- [ ] **Step 2: 팔레트 패널과 예시 재생**

`Editor/Graph/MotionNodePalette.cs` — 그래프 창 왼쪽에 붙는 `VisualElement`.

요구사항:
1. 카테고리별 목록. 미검증은 접힌 별도 섹션.
2. 항목을 누르면 요약과 예시 존재 여부를 보여 준다.
3. **예시 재생** — `MotionNodeDoctor.LoadSample(entry.Sample)`로 그래프를 열고, 그것을 "예시로 열기" 버튼으로 그래프 창에 띄운다. 스펙 7.1은 "그 자리에서 재생"이라고 하지만, 재생하려면 실제 씬 오브젝트와 슬롯 바인딩이 필요하다. **재생 대상이 없으면 재생할 수 없다는 것을 인정하고 "예시 그래프 열기"로 대신한다.** 씬에 임시 오브젝트를 만들어 재생하는 것은 프리팹으로 새어 나갈 위험이 크다.
4. 항목을 그래프로 드래그해 넣을 수 있으면 좋지만 필수는 아니다.

- [ ] **Step 3: 간선 순서 편집**

**이것이 이 태스크에서 가장 중요하다.** 같은 부모에서 나가는 간선의 순서가 곧 `Sequence`의 실행 순서인데, GraphView는 간선에 순서 개념이 없다. 사람이 순서를 볼 수도 바꿀 수도 없으면 `Sequence` 노드를 쓸 수 없다.

요구사항:
1. `MotionNodeView`의 출력 포트 옆에 자식 순서를 **번호로 표시한다** (1, 2, 3...). 간선 위에 라벨을 띄우거나 노드 안에 자식 목록을 그린다.
2. 노드를 선택하면 인스펙터 패널(Task 13)에 자식 순서 목록이 뜨고 위/아래로 옮길 수 있다. `MotionGraph.MoveLink(fromIndex, toIndex)`를 쓴다.
3. **`MoveLink`는 전역 간선 배열의 인덱스를 받는다.** 화면의 "이 부모의 두 번째 자식"을 그 인덱스로 바꾸는 변환이 필요하다. `MotionGraph.Links`를 훑어 해당 부모의 간선들이 배열의 몇 번째인지 모은 뒤 그 위치끼리 맞바꾼다:

```csharp
        /// <summary>
        /// 부모에서 나가는 간선들이 전역 배열의 몇 번째인지 모은다.
        /// 화면의 "두 번째 자식"과 저장 구조의 인덱스를 잇는 다리다.
        /// </summary>
        private static List<int> IndicesOf(MotionGraph graph, NodeId parent)
        {
            var indices = new List<int>();
            IReadOnlyList<NodeLink> links = graph.Links;

            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].From == parent)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }
```

두 자식의 순서를 바꾸려면 `IndicesOf`에서 얻은 두 위치를 `MoveLink`로 옮긴다. **한 번의 `MoveLink`가 배열을 재배치하므로 두 번째 인덱스를 다시 계산해야 한다** — 미리 뽑아 둔 값을 그대로 쓰면 엉뚱한 간선을 옮긴다.

- [ ] **Step 4: 손으로 확인한다**

`Sequence` 노드에 자식 셋을 달고 순서를 바꾼 뒤 저장하고 다시 연다. 화면의 번호와 실제 실행 순서가 같은지 프리뷰로 확인한다.

- [ ] **Step 5: 게이트를 돌리고 커밋한다**

---

### Task 13: 그래프 창 — 노드 인스펙터와 트리거 편집

**Files:** `Editor/Graph/MotionNodeInspector.cs`, `Editor/Graph/MotionTriggerPanel.cs`

- [ ] **Step 1: 노드 파라미터 그리기**

노드는 `MotionGraph._nodes`(`[SerializeReference]`) 안에 있으므로 `SerializedProperty`로 접근할 수 있다. **리플렉션으로 직접 그리지 말고 `SerializedProperty`를 쓴다** — 그래야 되돌리기와 프리팹 오버라이드가 공짜로 따라온다.

`MotionGraph`의 노드 배열 프로퍼티 경로는 `_nodes`이고, 배열 원소를 `NodeId`로 찾아야 한다:

```csharp
        /// <summary>
        /// <c>_nodes</c> 배열에서 이 id의 원소를 찾는다.
        ///
        /// 배열 인덱스와 <see cref="NodeId"/>는 다르다 — id는 재사용되지 않고 배열은
        /// 지운 자리를 메우므로 둘이 어긋난다. 원소의 <c>Id.Value</c>를 직접 비교한다.
        /// </summary>
        private static SerializedProperty FindNodeProperty(SerializedObject graph, NodeId id)
        {
            SerializedProperty nodes = graph.FindProperty("_nodes");
            if (nodes == null || !nodes.isArray)
            {
                return null;
            }

            for (int i = 0; i < nodes.arraySize; i++)
            {
                SerializedProperty element = nodes.GetArrayElementAtIndex(i);
                SerializedProperty idValue = element.FindPropertyRelative("Id").FindPropertyRelative("Value");

                if (idValue != null && idValue.intValue == id.Value)
                {
                    return element;
                }
            }

            return null;
        }
```

요구사항:
1. 노드를 선택하면 그 노드의 필드를 그린다. `[MotionParam]`의 `Label`·`Tooltip`을 쓰고, `HasRange`면 슬라이더로 그린다.
2. `SlotRef` 필드는 **텍스트 입력이 아니라 드롭다운**으로 그린다 — 이 그래프가 이미 쓰는 슬롯 이름 목록 + "새 슬롯..." + `Self`. 오타 하나가 조용히 동작하지 않는 연출을 만드는 것을 막는다.
3. `Id` 필드는 감춘다. 사람이 고칠 것이 아니다.
4. 노드 요약(`entry.Summary`)을 위에 보여 준다. 미검증이면 그 사실을 함께.

- [ ] **Step 2: 트리거 패널**

`Editor/Graph/MotionTriggerPanel.cs`.

요구사항:
1. 그래프가 선언한 트리거 목록: 이름, 재발사 정책, 진입 노드.
2. 트리거 추가/삭제. `Start`·`Loop`·`End`는 예약 이름이므로 한 번 누르면 만들어지는 버튼을 따로 둔다.
3. 진입 노드 설정 — 그래프 창에서 노드를 선택하고 "진입점으로" 버튼. `MotionGraph.SetTrigger(name, id, policy)`.
4. 진입 노드가 없는 트리거는 눈에 띄게 표시한다.
5. **트리거 노드와의 관계를 명확히 한다.** 코어에는 `TriggerNode`도 있고 `TriggerDeclaration`도 있다. 이 패널이 다루는 것은 선언이다. 노드 쪽은 그래프 안에서 진입점을 표시하는 용도다 — 구현 전에 `Runtime/Core/Nodes/TriggerNode.cs`를 읽고 둘의 역할을 확인한 뒤 UI에서 헷갈리지 않게 이름을 정한다.

- [ ] **Step 3: 손으로 확인하고 커밋한다**

---

### Task 14: 프리셋 브라우저

**Files:** `Editor/Browser/MotionPresetBrowser.cs`

요구사항:
1. `AssetDatabase.FindAssets("t:MotionGraph")`로 프로젝트의 모든 그래프를 모은다.
2. 목록: 이름, 경로, 노드 수, 트리거 목록, 검사 요약(`MotionIssueDrawer.Summarize`).
3. 검색 필드 — 이름과 트리거 이름으로 거른다.
4. 항목을 누르면 에셋을 선택하고, 더블클릭하면 그래프 창에서 연다.
5. **"선택한 오브젝트에 적용"** — 계층에서 고른 오브젝트에 `MotionPlayer`를 붙이고(없으면) 이 그래프를 꽂은 뒤 `SyncBindings()`와 자동 바인딩을 돌린다. **이것이 사용자가 요청한 "구현된 움직임을 어떤 곳에서든 한 번에 적용"의 실현 지점이다.** `Undo.AddComponent`와 `Undo.RecordObject`를 쓴다.
6. 목록은 캐시하고 "새로 고침" 버튼으로 갱신한다. `FindAssets`는 프로젝트가 크면 느리다.

- [ ] 게이트를 돌리고 커밋한다.

---

### Task 15: 마무리 — 문서와 CI

**Files:** 두 저장소의 `README.md`, `CHANGELOG.md`, 에디터 패키지의 `.github/workflows/ci.yml`

- [ ] **Step 1: 에디터 패키지 README**

담을 것:
- 설치 (런타임 패키지가 먼저 필요하다)
- 창 셋을 여는 법과 각각이 무엇을 하는지
- 그래프를 만들고 프리팹에 적용하는 최단 경로 (프리셋 브라우저의 "선택한 오브젝트에 적용")
- 노드를 새로 만들 때 Node Doctor를 통과시키는 법 — `[MotionNode]`의 네 필드와 예시 그래프
- 검증 명령: `./Tools~/compile-check/run.sh`와 Node Doctor 배치

- [ ] **Step 2: 런타임 패키지 README에 툴 패키지를 안내한다**

지금 README는 그래프를 손으로 만드는 법이 없다. "그래프를 GUI로 만들려면 `com.juahn.v2.uimotion.editor`를 함께 설치한다"를 넣는다.

- [ ] **Step 3: 스펙의 열린 질문 3번을 닫는다**

`docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`의 11절 3번을 다음으로 바꾼다:

```markdown
3. ~~에디터 프리뷰의 대상과 유출 방지~~ — **정해짐(계획 3).** 프리뷰는 씬 인스턴스와
   프리팹 스테이지를 가리지 않고 **선택된 `MotionPlayer` 그 자체**를 대상으로 한다.
   유출 방지는 새 장치를 만들지 않고 이미 있는 원상 복구 규약에 기댄다 —
   `MotionPreviewDriver.Stop`이 `MotionPlayer.StopAll()`을 부르고, 취소가 등록된 복구를
   역순으로 전부 돌린다. 도메인 리로드와 플레이 모드 전환 직전에도 같은 정리가 돈다.
   임시 오브젝트를 만들지 않으므로 씬에 남는 것이 없다.
```

또한 7.1절의 "팔레트에서 노드에 호버하면 그 예시 그래프가 그 자리에서 재생된다"를 실제 구현에 맞게 고친다 — 재생에는 대상 오브젝트와 슬롯 바인딩이 필요하므로 팔레트는 **예시 그래프를 여는 것**까지 한다.

- [ ] **Step 4: 에디터 CI에 Node Doctor 배치를 문서화한다**

Unity 라이선스가 필요해 CI에서 돌릴 수 없다. `ci.yml`에 그 사실과 로컬 실행 명령을 남긴다 — 런타임 패키지에서 컴파일 게이트에 대해 한 것과 같은 방식이다.

- [ ] **Step 5: 두 저장소의 게이트를 전부 돌린다**

```bash
# 런타임 패키지
cd com.juahn.v2.uimotion
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh

# 에디터 패키지
cd ../com.juahn.v2.uimotion.editor
./Tools~/compile-check/run.sh
```

`.meta` 검사도 두 저장소 모두에서 돌린다.

- [ ] **Step 6: 커밋**

---

## 남은 것 (이 계획의 범위 밖)

- **계획 4 — 어댑터 두 개.**
  - `com.juahn.v2.uimotion.uiservice` — `MotionGraphFeature : PresenterFeatureBase, ITransitionFeature`.
    생명주기 매핑은 실측으로 확인해 스펙 6절에 이미 고쳐 두었다. 핵심 세 가지:
    `OnPresenterInitialized`에서 `ClaimTriggerOwnership()`, `OnPresenterOpened`에서 `Fire("Start")`,
    `OnPresenterClosing`에서 `Fire("End")`. 완료원은 프리젠터가 `await`하기 전에 만들어야 한다.
  - `com.juahn.v2.uimotion.dotween` — `DoTweenRunner : IMotionTweenRunner`.
    `versionDefines`로 DOTween이 없으면 컴파일에서 제외한다.

- **성능 후속.** 계획 2의 실측대로 유지 연출은 프레임당 할당이 0이고 유한 트윈의 고빈도
  재발사만 문제가 된다. 측정된 문제가 생기면 `MotionScope`·`NodeRun`·핸들 풀링을 얹는다.
  실행 상태를 전부 스코프가 소유하는 구조라 풀링을 넣기 좋은 형태다.
