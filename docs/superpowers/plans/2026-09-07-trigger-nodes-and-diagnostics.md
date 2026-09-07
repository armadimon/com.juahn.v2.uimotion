# 트리거를 노드로, 그리고 경고 내역 (계획 5)

> **에이전트 작업자에게:** REQUIRED SUB-SKILL — `superpowers:subagent-driven-development`로 태스크 단위로 실행한다.

**목표 둘.**

1. 트리거를 그래프 안의 **노드**로 만들어 캔버스에서 바로 보이게 한다.
2. 실행 중에 나온 경고를 **목록으로 모아** 어디서 났는지 보고 고칠 수 있게 한다.

---

## 왜 트리거를 노드로 옮기는가

지금 트리거의 진실이 **두 곳에 있다.**

| 어디 | 무엇 |
|---|---|
| `MotionGraph._triggers` | `TriggerDeclaration` 목록 — 이름 · 정책 · 진입 노드 id |
| `TriggerNode` | 그래프 안에 놓는 표식 노드. `TriggerName`만 들고 아무 일도 하지 않는다 |

`MotionTriggerPanel`이 한쪽을 다른 쪽에 맞춰 준다. 즉 **어긋날 수 있는 구조이고, 실제로 어긋나면 무엇이 맞는지 알 방법이 없다.** 그래프 창에서는 "이 노드가 Start의 진입점"이라는 사실이 캔버스에 보이지 않고 옆 패널에만 있다.

**해법은 이 코드베이스가 슬롯에 이미 쓰는 방식이다** — 파생시킨다.

> 슬롯 목록은 저작하는 값이 아니라 파생되는 값이다. 사람이 인스펙터에서 슬롯을 추가하거나 타입을 고르지 않는다. 그래프가 자기 노드들을 훑어 목록을 만든다. (스펙 5.1)

트리거도 똑같이 만든다. `TriggerNode`가 유일한 진실이 되고, `TriggerDeclaration` 목록은 그것에서 계산된다. 진실이 하나면 어긋날 수 없다.

### 무엇이 달라지는가

| | 지금 | 뒤 |
|---|---|---|
| 트리거를 만든다 | 옆 패널에서 이름을 치고 노드를 골라 "진입점으로" | 캔버스에 `Trigger` 노드를 놓고 이름을 고른다 |
| 진입점이 보인다 | 옆 패널에서만 | 캔버스에서 노드로. 나가는 간선이 곧 실행 흐름이다 |
| 재발사 정책 | `TriggerDeclaration.Policy` | `TriggerNode.Policy` |
| 진실의 개수 | 둘 | 하나 |

### 실행 의미는 바뀌지 않는다

`TriggerNode.OnPlay`는 지금도 `MotionHandle.Completed`를 돌려주고 자식으로 흘려보낸다. 진입점이 그 노드 자신이 되어도 실행 결과는 같다 — 아무것도 하지 않는 노드가 하나 앞에 붙을 뿐이다.

`MotionRuntime.Fire("Start")`, `SubGraphNode.EntryTrigger`, 브릿지의 `Fire`/`WaitFor`는 전부 이름으로만 다루므로 손대지 않는다.

## 왜 경고 내역이 필요한가

경고가 나는 자리가 여덟이고 전부 Unity 콘솔로만 나간다.

```
Unity/Nodes/FlyAcrossNode.cs      출발 슬롯 해석 실패
Unity/Nodes/FadeNode.cs           CanvasGroup도 Graphic도 없음
Unity/Runtime/MotionPlayer.cs     비활성 상태에서 Fire
Unity/Runtime/MotionSlots.cs      슬롯 미바인딩 / 타입 불일치
Core/Nodes/SubGraphNode.cs        서브그래프 없음 / 깊이 초과
Core/Graph/MotionGraphIndex.cs    중복 노드 id / 중복 트리거
Core/Exec/MotionRuntime.cs        없는 트리거 발사
Core/Exec/NodeRun.cs              노드 사슬 깊이 초과 (오류)
```

전부 **인스턴스당 한 번만** 나온다(`OnceLogger`). 스팸을 막는 데는 옳지만, 그래서 **나중에 목록으로 다시 볼 방법이 없다.** 콘솔을 지웠거나 다른 로그에 밀려 올라가면 그걸로 끝이다.

그리고 이 경고들의 상당수는 **재생하기 전에 이미 알 수 있는 것들이다.** 슬롯이 비었는지, 꽂은 오브젝트가 노드가 요구하는 타입을 갖는지는 정적으로 검사된다.

그래서 둘을 만든다.

- **진단 창** — 실행 중에 나온 경고를 쌓아 두고 어느 플레이어·그래프·노드에서 났는지 보여 준다.
- **재생 전 검사** — `MotionPlayer` 인스펙터가 슬롯 바인딩을 미리 대조한다.

---

## 절대 규칙 (계획 1~4에서 이어짐)

1. 직렬화되는 타입의 필드는 public이고 readonly가 아니다.
2. `Runtime/Core`에 UnityEngine이 들어가면 안 된다.
3. LINQ 금지. C# 9.0.
4. 코드·주석·문서에 이모지 금지. 주석은 한국어로 쓴다.
5. 그래프는 실행 상태를 갖지 않는다.
6. **`.meta` 파일을 만든다.**
7. 커밋마다 `CHANGELOG.md`를 갱신한다.

## 검증 축

| 계층 | 방법 |
|---|---|
| `Runtime/Core/**` | `dotnet test` (현재 332개) |
| `Runtime/Unity/**` | `com.juahn.v2.uimotion/Tools~/compile-check/run.sh` |
| `Editor/**` | `com.juahn.v2.uimotion.editor/Tools~/compile-check/run.sh` |
| 마이그레이션 | Unity에서 손으로 (Task 4가 목록을 남긴다) |

---

## 저장소 1 — `com.juahn.v2.uimotion` (Task 1~4)

### Task 1: 코어 — TriggerNode에 정책을 싣고 TriggerIntrospector를 만든다

**Files:**
- Modify: `Runtime/Core/Nodes/TriggerNode.cs`
- Create: `Runtime/Core/Graph/TriggerIntrospector.cs`
- Test: `Tests~/dotnet/TriggerIntrospectorTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests~/dotnet/TriggerIntrospectorTests.cs`:

```csharp
using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class TriggerIntrospectorTests
    {
        private sealed class PlainNode : MotionEffectNode
        {
            public PlainNode(int id) { Id = new NodeId(id); }
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private static TriggerNode Trigger(int id, string name,
            TriggerPolicy policy = TriggerPolicy.Restart)
        {
            return new TriggerNode { Id = new NodeId(id), TriggerName = name, Policy = policy };
        }

        private static List<TriggerDeclaration> Collect(params MotionNodeBase[] nodes)
        {
            var into = new List<TriggerDeclaration>();
            TriggerIntrospector.Collect(nodes, into, null);
            return into;
        }

        [Test]
        public void NullNodes_YieldsEmpty()
        {
            var into = new List<TriggerDeclaration>();
            TriggerIntrospector.Collect(null, into, null);
            Assert.That(into, Is.Empty);
        }

        [Test]
        public void TriggerNode_BecomesItsOwnEntry()
        {
            // 진입점이 노드 자신이다. 그래서 "어느 노드가 진입점인가"가 캔버스에서 보인다.
            List<TriggerDeclaration> triggers = Collect(Trigger(7, "Start"));

            Assert.That(triggers.Count, Is.EqualTo(1));
            Assert.That(triggers[0].Name, Is.EqualTo("Start"));
            Assert.That(triggers[0].Entry, Is.EqualTo(new NodeId(7)));
            Assert.That(triggers[0].Policy, Is.EqualTo(TriggerPolicy.Restart));
        }

        [Test]
        public void PolicyComesFromTheNode()
        {
            List<TriggerDeclaration> triggers = Collect(Trigger(1, "Click", TriggerPolicy.Ignore));

            Assert.That(triggers[0].Policy, Is.EqualTo(TriggerPolicy.Ignore));
        }

        [Test]
        public void NonTriggerNodes_AreIgnored()
        {
            Assert.That(Collect(new PlainNode(1), new PlainNode(2)), Is.Empty);
        }

        [Test]
        public void NullNodeInArray_IsSkipped()
        {
            // 결손 노드. 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
            List<TriggerDeclaration> triggers = Collect(null, Trigger(2, "Start"), null);

            Assert.That(triggers.Count, Is.EqualTo(1));
        }

        [Test]
        public void NamelessTrigger_IsSkippedAndWarned()
        {
            var log = new FakeLog();
            var into = new List<TriggerDeclaration>();

            TriggerIntrospector.Collect(new MotionNodeBase[] { Trigger(1, "  ") }, into, log);

            Assert.That(into, Is.Empty);
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void NodeWithoutId_IsSkipped()
        {
            var orphan = new TriggerNode { TriggerName = "Start" };

            Assert.That(Collect(orphan), Is.Empty);
        }

        [Test]
        public void DuplicateName_FirstWins_AndWarns()
        {
            var log = new FakeLog();
            var into = new List<TriggerDeclaration>();

            TriggerIntrospector.Collect(
                new MotionNodeBase[] { Trigger(1, "Start"), Trigger(2, "Start") }, into, log);

            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Entry, Is.EqualTo(new NodeId(1)));
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void AuthoringOrder_IsPreserved()
        {
            // 순서가 결정적이어야 인스펙터와 패널의 목록이 리로드마다 흔들리지 않는다.
            List<TriggerDeclaration> triggers = Collect(
                Trigger(5, "End"), Trigger(2, "Start"), Trigger(9, "Loop"));

            Assert.That(triggers[0].Name, Is.EqualTo("End"));
            Assert.That(triggers[1].Name, Is.EqualTo("Start"));
            Assert.That(triggers[2].Name, Is.EqualTo("Loop"));
        }

        [Test]
        public void Collect_ClearsTargetList()
        {
            var into = new List<TriggerDeclaration> { new TriggerDeclaration("stale", NodeId.None) };
            TriggerIntrospector.Collect(new MotionNodeBase[] { Trigger(1, "Start") }, into, null);

            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Name, Is.EqualTo("Start"));
        }

        [Test]
        public void TriggerNameIsTrimmed()
        {
            // 사람이 이름을 칠 때 앞뒤 공백이 들어가면 Fire("Start")가 조용히 빗나간다.
            List<TriggerDeclaration> triggers = Collect(Trigger(1, " Start "));

            Assert.That(triggers[0].Name, Is.EqualTo("Start"));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `TriggerIntrospector`가 없고 `TriggerNode`에 `Policy`가 없음.

- [ ] **Step 3: TriggerNode에 정책을 싣는다**

`Runtime/Core/Nodes/TriggerNode.cs`를 다음으로 바꾼다:

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거의 진입점. <b>이 노드가 트리거 선언의 유일한 진실이다.</b>
    ///
    /// 그래프의 트리거 목록은 저작하는 값이 아니라 이 노드들에서 계산되는 파생값이다 —
    /// 슬롯 목록이 노드의 <c>[MotionSlot]</c> 필드에서 계산되는 것과 같다. 진실이 하나면
    /// 어긋날 수 없고, "이 노드가 Start의 진입점"이라는 사실이 캔버스에 그대로 보인다.
    ///
    /// 실행에는 관여하지 않는다 — 즉시 완료하고 자식으로 흘려보낸다.
    /// </summary>
    [MotionNode(Name = "Trigger", Category = "Flow",
        Summary = "트리거의 진입점. 이 노드의 이름으로 Fire를 부르면 아래로 이어진 연출이 돈다.",
        Sample = "Trigger")]
    [Serializable]
    public sealed class TriggerNode : MotionFlowNode
    {
        /// <summary>
        /// <c>Fire</c>에 넘길 이름. <c>Start</c> · <c>Loop</c> · <c>End</c>는 예약 이름으로
        /// 위상 규약이 붙는다 (<see cref="MotionRuntime"/> 참조). 그 밖의 이름은 자유다.
        /// </summary>
        public string TriggerName;

        /// <summary>
        /// 이미 재생 중인데 다시 발사했을 때의 처리.
        ///
        /// 예전에는 그래프의 트리거 목록에 있었다. 노드로 옮긴 이유는 진실을 하나로
        /// 모으기 위해서다 — 두 곳에 있으면 어긋났을 때 무엇이 맞는지 알 수 없다.
        /// </summary>
        public TriggerPolicy Policy = TriggerPolicy.Restart;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            return MotionHandle.Completed;
        }
    }
}
```

- [ ] **Step 4: TriggerIntrospector를 만든다**

`Runtime/Core/Graph/TriggerIntrospector.cs`:

```csharp
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
                    Warn(log, "trigger node " + trigger.Id + " has no name; it can never be fired");
                    continue;
                }

                if (!seen.Add(name))
                {
                    Warn(log, "duplicate trigger '" + name + "' at " + trigger.Id + "; the first one wins");
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

        private static void Warn(IMotionLog log, string message)
        {
            if (log != null)
            {
                log.Warn(message);
            }
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

`.meta`를 만든 뒤 `git commit -m "feat: 트리거 선언을 노드에서 계산하는 TriggerIntrospector 추가"`.

**아직 아무것도 바뀌지 않았다** — `MotionGraphIndex`는 여전히 넘겨받은 목록을 쓴다. 다음 태스크가 바꾼다.

---

### Task 2: 코어 — 인덱스가 노드에서 트리거를 파생시킨다

**Files:**
- Modify: `Runtime/Core/Graph/MotionGraphIndex.cs`
- Modify: `Tests~/dotnet/{MotionGraphIndexTests,MotionGraphValidatorTests,GraphEnumerationTests}.cs`

`FakeGraph`는 `IMotionGraphView`를 직접 구현하고 자기 목록을 들므로 **바꾸지 않는다.** 인덱스를 직접 만드는 테스트 셋만 영향을 받는다.

- [ ] **Step 1: 생성자를 바꾼다**

`MotionGraphIndex`의 생성자에서 `IReadOnlyList<TriggerDeclaration> triggers`를 빼고, 대신 **마이그레이션이 필요한 옛 목록**을 받는다:

```csharp
        public MotionGraphIndex(
            string graphName,
            IReadOnlyList<MotionNodeBase> nodes,
            IReadOnlyList<NodeLink> links,
            IMotionLog log)
            : this(graphName, nodes, links, null, log)
        {
        }

        /// <param name="legacyTriggers">
        /// 트리거를 노드로 옮기기 전에 저장된 목록. <b>읽지 않는다</b> — 비어 있지 않으면
        /// 그 그래프가 아직 마이그레이션되지 않았다는 뜻이므로 시끄럽게 경고만 한다.
        ///
        /// 조용히 무시하면 마이그레이션을 잊은 그래프가 트리거를 통째로 잃은 채
        /// 아무 말 없이 돌아간다. 팝업이 열리지 않는데 오류가 하나도 없는 상태가 된다.
        /// </param>
        public MotionGraphIndex(
            string graphName,
            IReadOnlyList<MotionNodeBase> nodes,
            IReadOnlyList<NodeLink> links,
            IReadOnlyList<TriggerDeclaration> legacyTriggers,
            IMotionLog log)
        {
            GraphName = string.IsNullOrEmpty(graphName) ? "<unnamed graph>" : graphName;

            _nodeIds = IndexNodes(nodes, log);
            IndexLinks(links);

            var triggers = new List<TriggerDeclaration>();
            TriggerIntrospector.Collect(nodes, triggers, log);
            _triggers = triggers.ToArray();

            for (int i = 0; i < _triggers.Length; i++)
            {
                _entries[_triggers[i].Name] = _triggers[i].Entry;
            }

            WarnAboutLegacyTriggers(legacyTriggers, log);

            var slots = new List<SlotDeclaration>();
            SlotIntrospector.Collect(nodes, slots);
            _slots = slots.ToArray();
        }

        /// <summary>
        /// 옛 목록이 남아 있으면 크게 경고한다. 마이그레이션을 잊은 그래프는
        /// 트리거를 잃은 채 조용히 도는 것이 가장 나쁜 결과다.
        /// </summary>
        private void WarnAboutLegacyTriggers(IReadOnlyList<TriggerDeclaration> legacy, IMotionLog log)
        {
            if (legacy == null || legacy.Count == 0 || log == null)
            {
                return;
            }

            log.Error("graph '" + GraphName + "' still stores " + legacy.Count +
                " trigger declaration(s) from the old format. run " +
                "Window > UI Motion > Migrate Graphs to convert them into Trigger nodes. " +
                "until then this graph has no triggers.");
        }
```

기존 `IndexTriggers` 메서드는 삭제한다. `NoTriggers` 상수도 쓰이지 않으면 삭제한다.

- [ ] **Step 2: 테스트 셋을 새 구조로 고친다**

세 파일에서 `new MotionGraphIndex(name, nodes, links, triggers, log)` 호출이 트리거 배열을 넘긴다. 그것을 **`nodes` 배열 안의 `TriggerNode`로 옮긴다.**

예: `MotionGraphValidatorTests`의 헬퍼를 다음처럼 바꾼다.

```csharp
        private static TriggerNode Trigger(int id, string name)
        {
            return new TriggerNode { Id = new NodeId(id), TriggerName = name };
        }

        private static MotionGraphIndex Build(
            MotionNodeBase[] nodes = null, NodeLink[] links = null, IMotionLog log = null)
        {
            return new MotionGraphIndex("g", nodes, links, log);
        }
```

그리고 `triggers: new[] { new TriggerDeclaration("Start", new NodeId(1)) }` 같은 것을 `nodes` 배열에 `Trigger(1, "Start")`를 넣고 그 노드에서 간선을 잇는 형태로 바꾼다.

**이 변환은 기계적이지 않다.** 예전에는 트리거가 임의의 노드를 진입점으로 가리켰지만 이제는 진입점이 트리거 노드 자신이다. 그래서 테스트의 그래프 모양이 실제로 달라진다:

```
예전:  Start -> (진입) PlainNode(1) -> PlainNode(2)
이후:  TriggerNode(1, "Start") -> PlainNode(2) -> PlainNode(3)
```

각 테스트가 **무엇을 확인하려던 것인지** 읽고 그 의도를 지키도록 옮긴다. 기대값을 맞추려고 단언을 약하게 만들지 않는다. 옮기기 애매한 테스트가 있으면 보고한다.

다음 테스트들은 의미가 달라지므로 특히 주의한다:

| 테스트 | 무엇이 달라지나 |
|---|---|
| `TriggerEntryToMissingNode_IsError` | 진입점이 노드 자신이라 "없는 노드를 가리키는 트리거"가 성립하지 않는다. **삭제하거나** "이름 없는 트리거 노드" 검사로 바꾼다 |
| `TriggerWithoutEntry_IsWarning` | 같은 이유. Task 3에서 새 규칙으로 대체된다 |
| `DuplicateTrigger_FirstWins_AndLogs` | 트리거 노드 둘로 만든다. `TriggerIntrospector`가 이미 덮으므로 여기서는 인덱스 통합만 확인한다 |
| `UnreachableNode_IsWarning` | 트리거 노드가 진입점이므로 도달 가능 집합의 뿌리가 바뀐다 |

- [ ] **Step 3: 통과를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

**개수가 줄어드는 것은 정상이다** (의미를 잃은 테스트를 지웠다면). 무엇을 왜 지웠는지 보고한다.

- [ ] **Step 4: 커밋**

```bash
git commit -m "refactor: 트리거 목록을 노드에서 파생시킨다"
```

---

### Task 3: 코어 — 검사기의 트리거 규칙을 새 구조에 맞춘다

진입점이 노드 자신이 되면서 예전 규칙 둘이 성립하지 않고, 대신 새로 잡아야 할 것이 생긴다.

**Files:**
- Modify: `Runtime/Core/Diag/MotionGraphValidator.cs`
- Modify: `Tests~/dotnet/MotionGraphValidatorTests.cs`

| 예전 규칙 | 뒤 |
|---|---|
| 트리거가 없는 노드를 가리킨다 (Error) | **불가능해짐.** 삭제 |
| 트리거의 진입점이 비어 있다 (Warning) | **불가능해짐.** 삭제 |
| — | **새로:** 트리거 노드에 이름이 없다 (Warning) |
| — | **새로:** 트리거 노드로 간선이 들어온다 (Warning) |
| — | **새로:** 그래프에 트리거 노드가 하나도 없다 (Warning) |
| `Loop`가 한 번 돌고 끝난다 (Info) | 그대로 |

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`MotionGraphValidatorTests`에 추가한다:

```csharp
        [Test]
        public void NamelessTriggerNode_IsWarning()
        {
            var nodes = new MotionNodeBase[] { new TriggerNode { Id = new NodeId(1) } };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes)),
                MotionIssueLevel.Warning, "no name"), Is.True);
        }

        [Test]
        public void LinkIntoTriggerNode_IsWarning()
        {
            // 트리거 노드는 진입점이다. 거기로 들어오는 간선은 아무 의미가 없고,
            // 만든 사람은 "이 연출 뒤에 저 트리거가 이어진다"고 착각한 것이다.
            // 실제로 이어지지 않으므로 조용히 아무 일도 일어나지 않는다.
            var nodes = new MotionNodeBase[]
            {
                Trigger(1, "Start"), new PlainNode(2), Trigger(3, "Loop"),
            };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(2), new NodeId(3)),
            };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links)),
                MotionIssueLevel.Warning, "entry point"), Is.True);
        }

        [Test]
        public void GraphWithoutAnyTrigger_IsWarning()
        {
            // 발사할 방법이 없는 그래프다. 붙여 두면 아무 일도 일어나지 않는다.
            var nodes = new MotionNodeBase[] { new PlainNode(1) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes)),
                MotionIssueLevel.Warning, "no trigger"), Is.True);
        }

        [Test]
        public void GraphWithATrigger_DoesNotWarnAboutMissingTriggers()
        {
            var nodes = new MotionNodeBase[] { Trigger(1, "Start") };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes)),
                MotionIssueLevel.Warning, "no trigger"), Is.False);
        }

        [Test]
        public void EmptyGraph_DoesNotWarnAboutMissingTriggers()
        {
            // 아직 아무것도 만들지 않은 그래프에까지 잔소리하면 새 그래프를 만들 때마다
            // 경고가 뜬다. 노드가 하나라도 있을 때만 묻는다.
            Assert.That(Has(MotionGraphValidator.Validate(Build()),
                MotionIssueLevel.Warning, "no trigger"), Is.False);
        }
```

- [ ] **Step 2: 실패를 확인하고 구현한다**

`CheckTriggers`를 다음으로 바꾼다. 옛 규칙 둘을 지우고 새 규칙 셋을 넣는다.

```csharp
        private static void CheckTriggers(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            bool sawTriggerNode = false;

            for (int i = 0; i < ids.Count; i++)
            {
                var trigger = graph.GetNode(ids[i]) as TriggerNode;
                if (trigger == null)
                {
                    continue;
                }

                sawTriggerNode = true;

                if (string.IsNullOrWhiteSpace(trigger.TriggerName))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, ids[i],
                        "this trigger node has no name, so nothing can fire it"));
                }
            }

            // 트리거 노드로 들어오는 간선. 만든 사람은 "이 연출 뒤에 저 트리거가 이어진다"고
            // 읽지만 실제로는 이어지지 않는다. 오류도 나지 않아 찾기 어렵다.
            for (int i = 0; i < ids.Count; i++)
            {
                IReadOnlyList<NodeId> children = graph.GetChildren(ids[i]);
                for (int c = 0; c < children.Count; c++)
                {
                    if (graph.GetNode(children[c]) is TriggerNode)
                    {
                        into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, children[c],
                            "a link points into this trigger node, but a trigger is an entry point " +
                            "and is only reached by firing it by name"));
                    }
                }
            }

            if (!sawTriggerNode && ids.Count > 0)
            {
                into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None,
                    "this graph has no trigger node, so nothing can play it. add one from the palette."));
            }

            CheckLoopRepeats(graph, into);
        }

        /// <summary>
        /// <c>Loop</c>가 한 번 돌고 멈추는지. 규칙 자체는 예전과 같고 진입점을 찾는
        /// 방법만 바뀌었다.
        /// </summary>
        private static void CheckLoopRepeats(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null || decl.Name != MotionRuntime.LoopTrigger)
                {
                    continue;
                }

                if (!LoopRepeats(graph, decl.Entry))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Info, decl.Entry,
                        "trigger 'Loop' runs once and stops. if the game does not re-fire it every " +
                        "time, wrap it in Repeat or use a node that keeps going such as Float or Bounce"));
                }
            }
        }
```

**도달 가능성 검사는 손대지 않는다.** 트리거 노드가 진입점이므로 `CheckReachability`가 그대로 동작한다 — 오히려 더 정확해진다.

- [ ] **Step 3: 옛 규칙을 검증하던 테스트를 정리한다**

`TriggerEntryToMissingNode_IsError`와 `TriggerWithoutEntry_IsWarning`은 성립하지 않는 상황을 검사한다. 지운다. **무엇을 왜 지웠는지 보고한다.**

- [ ] **Step 4: 통과를 확인하고 커밋한다**

---

### Task 4: Unity — 저작 API와 마이그레이션

**Files:**
- Modify: `Runtime/Unity/Graph/MotionGraph.cs`, `Runtime/Unity/Graph/MotionGraphAuthoring.cs`

- [ ] **Step 1: 저장 필드를 레거시로 표시한다**

`MotionGraph.cs`에서:

```csharp
        /// <summary>
        /// 트리거를 노드로 옮기기 전의 저장 형식. <b>더 이상 읽지 않는다.</b>
        ///
        /// 지우지 않고 남겨 두는 이유는 옛 에셋에서 마이그레이션할 정보가 여기 있기
        /// 때문이다. 마이그레이션이 끝나면 비워진다. 비어 있지 않은 그래프는
        /// <see cref="MotionGraphIndex"/>가 오류로 알린다 — 조용히 트리거를 잃는 것이
        /// 가장 나쁜 결과다.
        /// </summary>
        [SerializeField] private List<TriggerDeclaration> _triggers = new List<TriggerDeclaration>();
```

인덱스 생성을 바꾼다:

```csharp
                    _index = new MotionGraphIndex(name, _nodes, _links, _triggers, _log);
```

(시그니처는 같지만 이제 네 번째 인자가 "레거시"라는 의미다.)

- [ ] **Step 2: 저작 API를 노드 기반으로 바꾼다**

`MotionGraphAuthoring.cs`에서:

- `SetTrigger(string, NodeId, TriggerPolicy)`와 `RemoveTrigger(string)`을 **삭제한다.** 이제 트리거는 노드를 넣고 빼는 것이다.
- `RemoveNode`에서 `_triggers`의 진입점을 비우던 루프를 **삭제한다.** 트리거 노드를 지우면 트리거가 사라지는 것이 맞다.
- 다음 둘을 더한다:

```csharp
        /// <summary>
        /// 트리거 노드를 만들어 넣는다. 같은 이름이 이미 있으면 그 노드의 id를 돌려주고
        /// 새로 만들지 않는다 — 이름이 겹치면 뒤엣것이 무시되므로 만들어 봐야 혼란만 준다.
        /// </summary>
        public NodeId AddTrigger(string triggerName, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return NodeId.None;
            }

            string trimmed = triggerName.Trim();

            NodeId existing = FindTrigger(trimmed);
            if (existing.IsValid)
            {
                return existing;
            }

            return AddNode(new TriggerNode { TriggerName = trimmed, Policy = policy });
        }

        /// <summary>이 이름의 트리거 노드를 찾는다. 없으면 <see cref="NodeId.None"/>.</summary>
        public NodeId FindTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return NodeId.None;
            }

            string trimmed = triggerName.Trim();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var trigger = _nodes[i] as TriggerNode;
                if (trigger == null || trigger.TriggerName == null)
                {
                    continue;
                }

                if (trigger.TriggerName.Trim() == trimmed)
                {
                    return trigger.Id;
                }
            }

            return NodeId.None;
        }

        /// <summary>
        /// 옛 형식의 트리거 목록을 트리거 노드로 옮긴다. 옮긴 개수를 돌려준다.
        ///
        /// 옛 진입 노드는 새 트리거 노드의 <b>자식이 된다</b> — 예전에는 진입점이 그
        /// 노드였고 이제는 트리거 노드가 그 앞에 서기 때문이다. 실행 결과는 같다.
        ///
        /// 두 번 불러도 안전하다. 옮긴 것은 목록에서 지운다.
        /// </summary>
        public int MigrateLegacyTriggers()
        {
            if (_triggers.Count == 0)
            {
                return 0;
            }

            int moved = 0;

            for (int i = _triggers.Count - 1; i >= 0; i--)
            {
                TriggerDeclaration decl = _triggers[i];
                _triggers.RemoveAt(i);

                if (decl == null || string.IsNullOrWhiteSpace(decl.Name))
                {
                    continue;
                }

                NodeId trigger = AddTrigger(decl.Name, decl.Policy);
                if (!trigger.IsValid)
                {
                    continue;
                }

                // 옛 진입 노드를 새 트리거 노드 아래에 붙인다. 그 노드가 이미 트리거
                // 노드였다면(예전 표식 방식) 이을 것이 없다.
                if (decl.Entry.IsValid && decl.Entry != trigger && GetNode(decl.Entry) != null
                    && !(GetNode(decl.Entry) is TriggerNode))
                {
                    Link(trigger, decl.Entry);
                }

                moved++;
            }

            Invalidate();
            return moved;
        }
```

**`RemoveAt`을 뒤에서 앞으로 도는 것에 주의한다.** `AddTrigger`가 `AddNode`를 부르고 그것이 `Invalidate()`를 부르므로 순회 중에 인덱스가 무효화되지만, `_triggers`와 `_nodes`는 별개의 목록이라 안전하다.

- [ ] **Step 3: 컴파일 게이트를 돌린다**

`SetTrigger`/`RemoveTrigger`를 지웠으므로 그것을 부르던 곳이 전부 깨진다. **에디터 패키지와 샘플 생성기가 여기 해당한다** — Task 5에서 고친다. 이 태스크에서는 런타임 패키지의 게이트만 통과시킨다:

```bash
./Tools~/compile-check/run.sh
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

- [ ] **Step 4: 커밋**

```bash
git commit -m "feat: 트리거를 노드로 만드는 저작 API와 옛 형식 마이그레이션"
```

---

## 저장소 2 — `com.juahn.v2.uimotion.editor` (Task 5~7)

### Task 5: 에디터 — 트리거 노드를 캔버스에서 보이게 한다

이 태스크가 사용자가 요청한 것의 본체다. **트리거가 옆 패널이 아니라 캔버스에 보여야 한다.**

**Files:**
- Modify: `Editor/Graph/MotionNodeView.cs`, `MotionGraphViewImpl.cs`, `MotionTriggerPanel.cs`, `MotionNodeInspector.cs`, `MotionNodeSearchProvider.cs`
- Modify: `Editor/Samples/MotionSampleGenerator.cs`

- [ ] **Step 1: 깨진 호출을 고친다**

Task 4가 `SetTrigger`/`RemoveTrigger`를 지웠다. 먼저 컴파일을 되살린다:

```bash
./Tools~/compile-check/run.sh
```

나오는 오류를 `AddTrigger`/`FindTrigger`/`RemoveNode`로 옮긴다. 특히 `MotionSampleGenerator.BuildSample`은 이렇게 바뀐다:

```csharp
            var graph = ScriptableObject.CreateInstance<MotionGraph>();

            MotionNodeBase node = MotionNodeCatalog.Create(entry);
            NodeId nodeId = graph.AddNode(node);

            string triggerName = node.BlocksChildren ? MotionRuntime.LoopTrigger : MotionRuntime.StartTrigger;
            NodeId triggerId = graph.AddTrigger(triggerName);
            graph.Link(triggerId, nodeId);

            graph.SetNodePosition(triggerId, new Vector2(60f, 80f));
            graph.SetNodePosition(nodeId, new Vector2(320f, 80f));
```

**예시가 `Trigger` 노드 자신인 경우를 조심한다** — `TriggerNode`도 카탈로그에 있으므로 그 예시 그래프는 트리거 노드 하나가 자기 자신에 이어지는 이상한 모양이 될 수 있다. 노드가 `TriggerNode`면 트리거를 따로 만들지 말고 그 노드에 이름만 준다.

- [ ] **Step 2: 노드 뷰가 트리거를 다르게 그린다**

`MotionNodeView`에서:

1. **입력 포트를 만들지 않는다.** 트리거는 진입점이라 들어오는 흐름이 없다. 다만 계획 3에서 배운 것을 지킨다 — **이미 간선이 들어와 있는 그래프를 열었을 때 그 간선이 보이고 지울 수 있어야 한다.** 그러므로 포트는 만들되 흐리게 하고 `GetCompatiblePorts`에서 새 연결만 막는다. `AcceptsChildren`과 같은 방식으로 `AcceptsParent`를 둔다.
2. 제목을 `Trigger: Start`처럼 이름과 함께 보여 준다. 이름이 비었으면 `Trigger: (이름 없음)`으로 눈에 띄게.
3. 배경색을 달리해 흐름의 시작점임을 보이게 한다. `Start`·`Loop`·`End`는 예약 이름이므로 더 구분해도 좋다.
4. 재발사 정책을 부제로 보여 준다 (`Restart` / `Ignore` / `Queue`).

- [ ] **Step 3: 노드 인스펙터가 트리거를 편집한다**

`MotionNodeInspector`에서 `TriggerNode`를 고르면:

- **이름은 드롭다운 + 자유 입력.** 예약 이름 셋(`Start`·`Loop`·`End`)을 먼저 보여 주고 "직접 입력"을 마지막에 둔다. 오타 하나가 조용히 발사되지 않는 트리거를 만드는 것을 막는다.
- **정책은 드롭다운.** 각 값이 무엇을 뜻하는지 툴팁으로 적는다 — `Restart`는 끊고 처음부터, `Ignore`는 도는 중이면 무시, `Queue`는 줄 세움.
- 예약 이름을 골랐으면 그 위상 규약을 한 줄로 알려 준다. `Start`가 끝나면 `Loop`가 자동 발사되고, `End`는 `Loop`를 먼저 멈춘다는 것.

- [ ] **Step 4: 팔레트에서 트리거를 쉽게 넣는다**

`MotionNodeSearchProvider`에 예약 트리거 셋을 **바로 만들 수 있는 항목**으로 넣는다 — `Trigger: Start` · `Trigger: Loop` · `Trigger: End`. 노드를 놓고 이름을 다시 고르는 두 단계를 한 단계로 줄인다.

- [ ] **Step 5: 트리거 패널을 목록 뷰로 바꾼다**

`MotionTriggerPanel`은 이제 **편집기가 아니라 목차다.** 진실이 캔버스에 있으므로:

- 그래프의 트리거를 나열한다 (이름 · 정책 · 자식 수).
- 항목을 누르면 캔버스의 그 노드를 선택하고 화면에 보이게 한다 (`FrameSelection`).
- "트리거 추가" 버튼은 남긴다 — `AddTrigger` 후 캔버스에 노드가 생기고 선택된다.
- **진입점을 고르는 UI는 삭제한다.** 진입점이 노드 자신이라 고를 것이 없다.
- 이름 없는 트리거와 중복 이름을 눈에 띄게 표시한다.

- [ ] **Step 6: 마이그레이션 메뉴**

`Editor/Migration/MotionGraphMigration.cs`:

```csharp
        [MenuItem(MotionEditorPaths.MenuRoot + "Migrate Graphs")]
        public static void MigrateAll()
```

프로젝트의 모든 `MotionGraph`를 훑어 `MigrateLegacyTriggers()`를 부르고, 옮긴 그래프마다 `EditorUtility.SetDirty` + 마지막에 `AssetDatabase.SaveAssets`.

**결과를 개수만으로 알리지 않는다.** `MigrateLegacyTriggers`는 `MigrationReport`를 돌려주는데 거기에 *이어지지 않은 트리거*와 *버린 것*이 들어 있다. 그것이 있는 그래프는 사람이 직접 손봐야 하므로:

- `report.NeedsAttention`인 그래프는 `Debug.LogWarning`으로 **그래프 에셋을 문맥으로** 남긴다 (콘솔에서 눌러 바로 갈 수 있게).
- 마지막 요약에 "손봐야 할 그래프 N개"를 함께 알린다.

이어지지 않은 트리거를 조용히 넘기면 그 트리거를 발사해도 아무 일이 없는데 오류가 하나도 없는 상태가 된다 — 마이그레이션이 만들 수 있는 가장 나쁜 결과다.

**되돌릴 수 없는 변경이므로 확인 대화상자를 띄운다.** `EditorUtility.DisplayDialog`로 무엇이 바뀌는지 알리고 취소할 수 있게 한다.

- [ ] **Step 7: 게이트를 돌리고 커밋한다**

---

### Task 6: 에디터 — 실행 중 경고를 모으는 진단 창

**Files:**
- Create: `Editor/Diagnostics/MotionDiagnostics.cs`, `MotionDiagnosticsWindow.cs`
- Modify: `Runtime/Unity/Runtime/UnityMotionLog.cs` (런타임 저장소)

- [ ] **Step 1: 로그에 관찰 지점을 만든다**

`UnityMotionLog`가 지금은 `Debug.LogWarning`만 부른다. 거기에 **가로채기 지점**을 하나 둔다. 에디터가 그것을 구독한다.

```csharp
        /// <summary>
        /// 이 로그를 지나간 모든 진단. <b>에디터 도구가 구독한다.</b>
        ///
        /// 콘솔만으로는 부족한 이유 — 경고는 인스턴스당 한 번만 나오고(OnceLogger),
        /// 다른 로그에 밀려 올라가면 다시 볼 방법이 없다. 어느 플레이어에서 났는지도
        /// 메시지 문구로만 알 수 있다.
        ///
        /// <b>구독은 에디터에서만 한다.</b> 빌드에서 이것을 붙잡으면 목록이 무한히 자란다.
        /// </summary>
        public static event Action<MotionIssueLevel, string, Object> Emitted;
```

`Warn`과 `Error`에서 `Debug.Log*` 앞뒤로 이 이벤트를 쏜다. 구독자가 없으면 아무 비용도 없다.

**런타임 저장소를 건드리는 유일한 곳이다.** 다른 방법(에디터에서 `Application.logMessageReceived`를 파싱)은 문자열을 다시 뜯어야 하고 문맥 오브젝트를 잃는다.

- [ ] **Step 2: 수집기**

`Editor/Diagnostics/MotionDiagnostics.cs` — `[InitializeOnLoad]` static.

들고 있어야 할 것 (항목당):

| 필드 | 왜 |
|---|---|
| `MotionIssueLevel Level` | 정렬과 아이콘 |
| `string Message` | 본문 |
| `int InstanceId` | 문맥 오브젝트. **참조를 들지 않는다** — 진단 하나가 파괴된 오브젝트를 붙잡으면 안 된다 |
| `string ContextName` | 오브젝트가 사라진 뒤에도 어디였는지 알 수 있게 |
| `int Count` | 같은 메시지가 여러 번이면 세기만 한다 |
| `double FirstSeen` / `LastSeen` | `EditorApplication.timeSinceStartup` |

규칙:

- **상한을 둔다** (예: 500). 넘으면 오래된 것부터 버린다. 방치형에서 몇 시간 돌면 목록이 메모리를 먹는다.
- 같은 `(Level, Message, InstanceId)`는 새 항목을 만들지 않고 `Count`를 올린다.
- 플레이 모드에 들어갈 때 **비울지 말지 옵션**으로 둔다 (기본: 비운다). 그러지 않으면 지난 세션의 경고가 섞인다.
- 도메인 리로드에서 사라지는 것을 감수한다. `[SerializeField]`로 살리려 하지 않는다 — 그 복잡도만큼의 값이 없다.

- [ ] **Step 3: 창**

`Editor/Diagnostics/MotionDiagnosticsWindow.cs` — 메뉴 `Window/UI Motion/Diagnostics`.

요구사항:

1. 심각한 것부터 정렬. 같은 수준이면 최근 것부터.
2. 각 행: 아이콘 · 메시지 · 어디서(`ContextName`) · 횟수.
3. **행을 누르면 그 오브젝트를 선택하고 계층에서 보이게 한다** (`EditorGUIUtility.PingObject`). 파괴됐으면 이름만 보여 준다.
4. 수준별 필터 토글 (오류 / 경고 / 정보).
5. "비우기" 버튼. "플레이 시작 시 비우기" 토글 (`EditorPrefs`에 저장).
6. 비어 있으면 **무엇을 하면 되는지** 알려 준다 — 재생하면 여기에 쌓인다는 것.
7. **선택 변경과 목록 갱신은 GUI 패스가 끝난 뒤로 미룬다.** 이 저장소가 이미 겪은 함정이다 (`ArgumentException: Getting control N's position...`).

- [ ] **Step 4: 게이트를 돌리고 커밋한다**

---

### Task 7: 에디터 — 재생 전에 잡는다

진단 창은 이미 일어난 일을 보여 준다. 대부분의 경고는 **재생하기 전에 알 수 있다.**

**Files:** `Editor/Inspector/MotionPlayerEditor.cs`

- [ ] **Step 1: 바인딩 검사**

`MotionPlayer` 인스펙터에 "재생 전 검사" 구역을 만든다. 그래프와 바인딩을 대조해 다음을 잡는다:

| 잡을 것 | 어떻게 |
|---|---|
| 비어 있는 슬롯 | `graph.Slots`의 이름 중 바인딩이 null인 것. 이미 표시하지만 **한 줄 요약으로 올린다** |
| 타입이 안 맞는 바인딩 | `SlotDeclaration.RequiredType`이 있고 꽂힌 오브젝트에서 그 타입을 얻을 수 없는 경우. `MotionSlots`의 코어싱 규칙과 **같은 규칙으로** 판정해야 한다 — 다르면 도구가 거짓말을 한다 |
| `Fade`가 쓸 수 없는 대상 | `FadeNode`는 `CanvasGroup`이나 `Graphic` 중 하나가 필요하다. 슬롯 타입이 `Component`라 일반 검사로는 안 잡힌다 |
| 그래프의 검사 결과 | `MotionGraphValidator`. 이미 요약만 보여 주는데 **펼쳐 볼 수 있게** 한다 |

**판정 규칙을 `MotionSlots`와 공유한다.** 복사하면 언젠가 갈라지고, 그때 인스펙터가 "괜찮다"고 한 것이 런타임에 경고를 낸다. `MotionSlots`에 경고를 내지 않는 판정용 메서드를 하나 열거나, 코어싱 규칙을 별도 static으로 빼서 둘이 함께 쓴다.

- [ ] **Step 2: 손으로 확인한다**

- [ ] 슬롯을 비워 두면 인스펙터가 재생 전에 알려 준다
- [ ] `CanvasGroup`이 없는 오브젝트를 `Fade` 슬롯에 꽂으면 잡는다
- [ ] 고친 뒤 경고가 사라진다
- [ ] 재생했을 때 인스펙터가 통과시킨 것이 콘솔에서 경고를 내지 않는다 (판정 규칙이 실제로 같은지)

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

---

### Task 8: 마무리 — 문서와 마이그레이션 확인

- [ ] **Step 1: 스펙을 고친다**

`docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`:

- 4.4절(위상 규약)에 **트리거가 노드라는 것**과 그것이 파생값이라는 것을 적는다. 5절의 슬롯 설명과 같은 논리라는 점을 명시한다.
- 결정 표(2절)에 항목을 더한다 — 3번 "이름 있는 트리거 채널"의 구현이 선언 목록에서 노드로 바뀌었다는 것.

- [ ] **Step 2: README를 고친다**

트리거를 만드는 법이 바뀌었다. 런타임 패키지 README와 에디터 패키지 README 둘 다.

- [ ] **Step 3: 확인 목록에 마이그레이션과 진단을 넣는다**

`com.juahn.v2.uimotion.editor/docs/unity-verification.md`에 절을 더한다:

**트리거 노드**
- [ ] 팔레트에서 `Trigger: Start`를 놓으면 트리거가 생기고 인스펙터에 뜬다
- [ ] 트리거 노드를 지우면 그 트리거가 사라진다
- [ ] 같은 이름의 트리거 노드 둘을 만들면 경고가 뜨고 하나만 동작한다
- [ ] 이름을 비우면 경고가 뜬다
- [ ] 트리거 노드로 간선을 이으려 하면 이어지지 않는다
- [ ] 트리거 노드에서 나가는 간선이 실제 실행 순서와 같다

**마이그레이션**
- [ ] 옛 형식 그래프를 열면 **오류가 뜬다** (조용히 트리거를 잃지 않는다)
- [ ] `Window > UI Motion > Migrate Graphs`가 트리거 노드를 만들고 옛 진입 노드를 그 아래에 붙인다
- [ ] 마이그레이션 뒤 연출이 이전과 똑같이 돈다
- [ ] 두 번 돌려도 안전하다

**진단 창**
- [ ] 슬롯을 비우고 재생하면 경고가 목록에 쌓인다
- [ ] 행을 누르면 그 오브젝트가 계층에서 선택된다
- [ ] 같은 경고가 여러 번 나면 횟수만 오른다
- [ ] 오브젝트를 지운 뒤에도 창이 죽지 않는다
- [ ] 몇 분 재생해도 목록이 무한히 자라지 않는다

- [ ] **Step 4: 네 저장소의 게이트를 전부 돌리고 커밋한다**

---

## IdleMine의 테스트 장치도 고쳐야 한다

`IdleMine/Assets/_Project/1_Scripts/Editor/UiMotionTestBed.cs`가 `graph.SetTrigger(...)`를 부른다. Task 4가 그것을 지우므로 `AddTrigger` + `Link`로 바꾼다. 그러지 않으면 IdleMine이 컴파일되지 않는다.

바뀐 모양:

```csharp
            NodeId startTrigger = graph.AddTrigger(MotionRuntime.StartTrigger);
            graph.Link(startTrigger, parallelId);
            graph.SetNodePosition(startTrigger, new Vector2(-180f, 60f));
```

세 트리거 전부 같은 방식이다.
