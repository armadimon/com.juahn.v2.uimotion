# UI Motion — Unity 런타임 계층 구현 계획 (계획 2)

> **에이전트 작업자에게:** REQUIRED SUB-SKILL — `superpowers:subagent-driven-development`로 태스크 단위로 실행한다. 스텝은 체크박스(`- [ ]`)다.

**목표:** 계획 1이 만든 순수 코어 엔진을 Unity에서 실제로 돌린다 — 그래프 에셋, 플레이어 컴포넌트, 슬롯 해석, 트윈 백엔드, 13개 효과 노드.

**아키텍처:** `MotionGraph`(ScriptableObject)는 직렬화 껍데기일 뿐이고 조회 로직은 전부 순수 코어의 `MotionGraphIndex`에 있다. 그래서 그래프 로직이 `dotnet test`로 검증된다. Unity 계층은 로컬 `dotnet build` 컴파일 게이트로 검증한다.

**기술 스택:** Unity 6000.x · netstandard2.1 · C# 9.0 · NUnit(코어) · com.unity.ugui

---

## 이 계획이 확정하는 것 (스펙 11절의 열린 질문)

| 질문 | 결정 | 이유 |
|---|---|---|
| 1. 그래프 직렬화 형식 | `[SerializeReference] MotionNodeBase[] _nodes` + 평면 간선 배열 `NodeLink[] _links` | 다형 노드는 `SerializeReference`가 유일한 선택지다. 간선을 중첩 배열이 아니라 평면 목록으로 두면 YAML diff가 한 줄씩 움직여 머지 충돌이 줄고, 결손 노드(타입이 사라진 노드)가 배열의 `null` 원소로 남아 나머지 그래프가 계속 동작한다 |
| 2. 트윈 러너 구동 방식 | **단일 `MotionPump`** — 숨겨진 MonoBehaviour 하나가 모든 플레이어를 한 `Update`에서 돌린다 | 방치형이라 인벤토리 슬롯 수백 개가 동시에 산다. `MonoBehaviour.Update`는 네이티브→매니지드 경계를 매번 넘으므로 300개면 그 비용이 300배다. 펌프는 1회다 |
| 3. 에디터 프리뷰 | 계획 3에서 정한다 | 이 계획의 범위 밖 |

**추가로 이 계획이 도입하는 것**

- `IMotionContext.Host` — 코어에 여는 작은 구멍 하나. 효과 노드가 트윈 러너에 닿는 통로다. `ResolveSlot`이 `object`를 돌려주는 것과 같은 관용구다.
- `Tools~/compile-check` — Unity 계층을 `dotnet build`로 컴파일하는 로컬 게이트. Unity 에디터를 열지 않고 1초 안에 컴파일 오류를 잡는다.

## 검증 축

| 계층 | 방법 | 어디서 |
|---|---|---|
| `Runtime/Core/**` | `dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj` | CI + 로컬 |
| `Runtime/Unity/**` | `Tools~/compile-check/run.sh` | 로컬 전용 (Unity DLL은 재배포 불가라 CI에 없다) |
| 코어 순수성 | `grep -rnE "using UnityEngine\|UnityEngine\." Runtime/Core/` | CI + 로컬 |

**모든 태스크는 커밋 전에 해당하는 축을 전부 통과해야 한다.** Unity 파일을 하나라도 건드렸으면 `run.sh`를 돌린다.

## 절대 규칙 (계획 1에서 이어짐)

1. **직렬화되는 타입은 public 필드를 쓴다.** Unity는 `readonly` 필드를 직렬화하지 않고, private 필드는 `[SerializeField]`가 있어야 한다. `Runtime/Core`에서는 `[SerializeField]`를 쓸 수 없다(UnityEngine 타입이다). 그러므로 그래프 에셋에 저장되는 코어 타입의 필드는 **public 이고 non-readonly** 여야 한다.
2. **`Runtime/Core`에 UnityEngine이 들어가면 안 된다.** `using UnityEngine`도, `UnityEngine.Vector3` 같은 정규화 참조도 금지다. CI가 grep으로 막는다.
3. **슬롯 목록은 파생 값이다.** 사람이 저작하지 않는다. 노드의 `SlotRef` 필드와 `[MotionSlot]`에서 매번 계산한다. `SlotDeclaration.RequiredType`이 `[NonSerialized]`인 이유가 이것이다 — 저장하면 도메인 리로드마다 `null`이 된다.
4. **LINQ 금지.** `for` 루프와 `Dictionary`/`List`를 쓴다.
5. **코드·주석·문서에 이모지 금지.**
6. **그래프는 실행 상태를 갖지 않는다.** 같은 에셋을 수백 개 오브젝트가 동시에 쓴다. 캐시(`[NonSerialized]` 인덱스)는 읽기 전용 파생값이라 허용되지만, 재생 중 바뀌는 값은 어떤 것도 그래프에 두지 않는다.

## 파일 구조

```
Tools~/compile-check/
  UiMotion.Unity.Compile.csproj   Unity DLL을 참조해 Runtime 전체를 컴파일
  run.sh                          Unity 설치를 찾아 위 csproj를 빌드

Runtime/Core/Graph/
  NodeLink.cs                     노드 간 간선 하나 (평면 목록의 원소)
  SlotIntrospector.cs             노드들에서 슬롯 선언을 계산 (리플렉션, 순수)
  MotionGraphIndex.cs             IMotionGraphView의 조회 로직 전부 (순수, 테스트됨)
Runtime/Core/Authoring/
  IMotionContext.cs               Host 추가 (수정)
Runtime/Core/Exec/
  MotionContext.cs                Host 전달 (수정)
Runtime/Core/Nodes/
  SubGraphNode.cs                 Host 전파 (수정)

Runtime/Unity/
  juahn.v2.UiMotion.asmdef
  Graph/MotionGraph.cs            ScriptableObject. Index에 위임
  Graph/MotionGraphAuthoring.cs   에디터가 쓰는 변경 API
  Runtime/SlotBinding.cs          이름 -> UnityEngine.Object
  Runtime/SlotTable.cs            ISlotResolver 구현
  Runtime/MotionSlots.cs          object -> T 코어싱 (GetComponent 포함)
  Runtime/UnityMotionLog.cs       IMotionLog -> Debug
  Runtime/MotionPlayer.cs         프리팹에 붙는 유일한 컴포넌트
  Runtime/MotionPump.cs           전역 틱 펌프
  Runtime/MotionSignals.cs        SendSignal 노드의 수신 지점
  Tween/IMotionTweenRunner.cs     교체 가능한 트윈 백엔드
  Tween/BuiltinTweenRunner.cs     코어 타이머 기반 기본 구현
  Tween/MotionContextExtensions.cs  ctx.Tween()
  Nodes/UnityEffectNode.cs        13개 효과 노드의 공통 베이스
  Nodes/SubGraphAssetNode.cs      MotionGraph를 가리키는 서브그래프
  Nodes/MoveNode.cs · ScaleNode.cs · RotateNode.cs
  Nodes/FadeNode.cs · ColorNode.cs
  Nodes/PunchScaleNode.cs · ShakeNode.cs
  Nodes/FloatNode.cs · BounceNode.cs
  Nodes/FlyAcrossNode.cs
  Nodes/SetActiveNode.cs · SendSignalNode.cs · PlayAnimatorNode.cs
```

---

### Task 1: Unity 컴파일 검증 하네스

이 태스크가 나머지 전부의 안전망이다. 이것 없이 Unity 코드를 쓰면 에디터를 열기 전까지 오타조차 발견되지 않는다.

**Files:**
- Create: `Tools~/compile-check/UiMotion.Unity.Compile.csproj`
- Create: `Tools~/compile-check/run.sh`
- Create: `Runtime/Unity/juahn.v2.UiMotion.asmdef`
- Create: `Runtime/Unity/Runtime/UnityMotionLog.cs`
- Modify: `.gitignore`
- Modify: `package.json`

- [ ] **Step 1: asmdef를 만든다**

`Runtime/Unity/juahn.v2.UiMotion.asmdef`:

```json
{
    "name": "juahn.v2.UiMotion",
    "rootNamespace": "Juahn.UiMotion",
    "references": [
        "juahn.v2.UiMotion.Core",
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
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

- [ ] **Step 2: 첫 Unity 파일을 만든다 — 로그 어댑터**

컴파일 하네스가 검증할 대상이 하나는 있어야 한다. 마침 가장 단순한 것이 로그 어댑터다.

`Runtime/Unity/Runtime/UnityMotionLog.cs`:

```csharp
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 코어의 진단을 Unity 콘솔로 흘린다.
    ///
    /// 문맥 오브젝트를 함께 넘기는 이유는 콘솔에서 메시지를 눌렀을 때
    /// 문제를 낸 오브젝트가 계층에서 선택되게 하기 위해서다. 방치형 게임에서
    /// "어떤 팝업의 어떤 슬롯이 비었는가"를 찾는 데 이것이 결정적이다.
    /// </summary>
    public sealed class UnityMotionLog : IMotionLog
    {
        private readonly Object _context;

        public UnityMotionLog(Object context)
        {
            _context = context;
        }

        public void Warn(string message)
        {
            Debug.LogWarning("[UiMotion] " + message, _context);
        }

        public void Error(string message)
        {
            Debug.LogError("[UiMotion] " + message, _context);
        }
    }
}
```

- [ ] **Step 3: 컴파일 프로젝트를 만든다**

`Tools~/compile-check/UiMotion.Unity.Compile.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    Unity 계층을 Unity 에디터 없이 컴파일한다.

    Unity의 매니지드 DLL은 재배포할 수 없으므로 이 검사는 CI가 아니라 로컬 게이트다.
    Unity 파일을 건드린 모든 커밋은 run.sh 통과가 조건이다.

    UnityManaged / UnityUgui 경로는 run.sh가 넘긴다.
  -->

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>UiMotion.Unity.CompileCheck</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <!-- 직렬화 필드는 인스펙터가 채우므로 "할당된 적 없음" 경고는 의미가 없다. -->
    <NoWarn>CS0649;CS0169;CS0414</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../Runtime/Core/**/*.cs" LinkBase="Core" />
    <Compile Include="../../Runtime/Unity/**/*.cs" LinkBase="Unity" />
  </ItemGroup>

  <ItemGroup>
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
    <Reference Include="UnityEngine.UI">
      <HintPath>$(UnityUgui)</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

- [ ] **Step 4: 실행 스크립트를 만든다**

`Tools~/compile-check/run.sh`:

```bash
#!/usr/bin/env bash
#
# Unity 계층 컴파일 게이트.
#
# Unity 에디터를 열지 않고 Runtime/Core + Runtime/Unity 전체를 컴파일한다.
# Unity 파일을 건드렸으면 커밋 전에 이것을 돌린다.
#
#   ./Tools~/compile-check/run.sh
#   UNITY_ROOT=/path/to/Editor/6000.x.y ./Tools~/compile-check/run.sh
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# --- Unity 설치를 찾는다 ------------------------------------------------
if [ -z "${UNITY_ROOT:-}" ]; then
  UNITY_ROOT="$(ls -d /Applications/Unity/Hub/Editor/*/ 2>/dev/null | sort -V | tail -1 || true)"
fi

if [ -z "${UNITY_ROOT}" ] || [ ! -d "${UNITY_ROOT}" ]; then
  echo "Unity 설치를 찾지 못했습니다. UNITY_ROOT를 지정하세요." >&2
  echo "  예: UNITY_ROOT=/Applications/Unity/Hub/Editor/6000.5.3f1 $0" >&2
  exit 2
fi

UNITY_MANAGED="${UNITY_ROOT}/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine"
if [ ! -d "${UNITY_MANAGED}" ]; then
  echo "매니지드 어셈블리 폴더가 없습니다: ${UNITY_MANAGED}" >&2
  echo "이 스크립트는 macOS 레이아웃을 가정합니다. 다른 OS면 UNITY_MANAGED를 직접 넘기세요." >&2
  exit 2
fi

# uGUI는 패키지라 에디터 본체가 아니라 템플릿 캐시에 들어 있다.
if [ -z "${UNITY_UGUI:-}" ]; then
  UNITY_UGUI="$(ls "${UNITY_ROOT}"/Unity.app/Contents/Resources/PackageManager/ProjectTemplates/libcache/*/ScriptAssemblies/UnityEngine.UI.dll 2>/dev/null | head -1 || true)"
fi

if [ -z "${UNITY_UGUI}" ] || [ ! -f "${UNITY_UGUI}" ]; then
  echo "UnityEngine.UI.dll을 찾지 못했습니다. UNITY_UGUI로 지정하세요." >&2
  exit 2
fi

echo "Unity:  ${UNITY_ROOT}"
echo "uGUI:   ${UNITY_UGUI}"
echo

dotnet build "${HERE}/UiMotion.Unity.Compile.csproj" \
  -p:UnityManaged="${UNITY_MANAGED}" \
  -p:UnityUgui="${UNITY_UGUI}" \
  -v quiet --nologo

echo
echo "Unity 계층 컴파일 통과."
```

실행 권한을 준다:

```bash
chmod +x Tools~/compile-check/run.sh
```

- [ ] **Step 5: `.gitignore`에 예외를 추가한다**

`*.csproj`가 무시되므로 이 프로젝트 파일도 커밋되지 않는다. `Tests~` 예외 바로 아래에 추가한다:

```
# 테스트 프로젝트는 dotnet test에 필요하므로 커밋한다.
!Tests~/dotnet/*.csproj

# 컴파일 게이트 프로젝트도 커밋한다.
!Tools~/compile-check/*.csproj
```

- [ ] **Step 6: `package.json`에 uGUI 의존성을 선언한다**

`Fade`·`Color` 노드가 `UnityEngine.UI.Graphic`을 만지므로 uGUI가 필요하다. Unity 1st-party 패키지라 "참조 0"(UiService·DOTween·UniTask를 참조하지 않는다) 원칙은 그대로다.

`"dependencies": {}`를 다음으로 바꾼다:

```json
  "dependencies": {
    "com.unity.ugui": "2.0.0"
  }
```

- [ ] **Step 7: 게이트를 돌려 통과를 확인한다**

```bash
./Tools~/compile-check/run.sh
```

기대: `Unity 계층 컴파일 통과.`

- [ ] **Step 8: 게이트가 실제로 오류를 잡는지 확인한다 (중요)**

통과만 확인하면 게이트가 아무것도 컴파일하지 않고 있어도 모른다. 일부러 깨뜨려 본다:

```bash
echo 'class Broken { void X() { UnityEngine.Vector3 v = "nope"; } }' > Runtime/Unity/Runtime/_Broken.cs
./Tools~/compile-check/run.sh; echo "exit=$?"
```

기대: `error CS0029` 가 뜨고 `exit=1`.

되돌린다:

```bash
rm Runtime/Unity/Runtime/_Broken.cs
./Tools~/compile-check/run.sh
```

- [ ] **Step 9: 코어 테스트와 순수성 가드가 여전히 통과하는지 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
```

기대: `Passed! - Failed: 0, Passed: 232` 그리고 `purity exit=1`(일치 없음).

- [ ] **Step 10: 커밋**

```bash
git add Tools~ Runtime/Unity .gitignore package.json
git commit -m "build: Unity 계층 컴파일 게이트와 어셈블리 정의 추가"
```

---

### Task 2: 코어 — NodeLink와 IMotionContext.Host

**Files:**
- Create: `Runtime/Core/Graph/NodeLink.cs`
- Modify: `Runtime/Core/Authoring/IMotionContext.cs`
- Modify: `Runtime/Core/Exec/MotionContext.cs`
- Modify: `Runtime/Core/Exec/MotionRuntime.cs`
- Modify: `Runtime/Core/Nodes/SubGraphNode.cs`
- Test: `Tests~/dotnet/NodeLinkTests.cs`, `Tests~/dotnet/MotionContextHostTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다 — NodeLink**

`Tests~/dotnet/NodeLinkTests.cs`:

```csharp
using System;
using System.Reflection;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class NodeLinkTests
    {
        [Test]
        public void Default_IsInvalid()
        {
            var link = default(NodeLink);
            Assert.That(link.IsValid, Is.False);
        }

        [Test]
        public void BothEndsValid_IsValid()
        {
            var link = new NodeLink(new NodeId(1), new NodeId(2));
            Assert.That(link.IsValid, Is.True);
        }

        [Test]
        public void MissingTo_IsInvalid()
        {
            var link = new NodeLink(new NodeId(1), NodeId.None);
            Assert.That(link.IsValid, Is.False);
        }

        [Test]
        public void SameEnds_AreEqual()
        {
            var a = new NodeLink(new NodeId(3), new NodeId(4));
            var b = new NodeLink(new NodeId(3), new NodeId(4));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ReversedEnds_AreNotEqual()
        {
            var a = new NodeLink(new NodeId(3), new NodeId(4));
            var b = new NodeLink(new NodeId(4), new NodeId(3));
            Assert.That(a, Is.Not.EqualTo(b));
        }

        // Unity 직렬화 제약을 못 박는다. 계획 1의 NodeId/SlotRef와 같은 이유다 —
        // readonly 필드나 private 필드는 그래프 에셋에 저장되지 않는다.
        [Test]
        public void Fields_ArePublicAndAssignable()
        {
            FieldInfo from = typeof(NodeLink).GetField("From");
            FieldInfo to = typeof(NodeLink).GetField("To");

            Assert.That(from, Is.Not.Null, "From은 public 필드여야 한다");
            Assert.That(to, Is.Not.Null, "To는 public 필드여야 한다");
            Assert.That(from.IsInitOnly, Is.False, "readonly면 Unity가 직렬화하지 않는다");
            Assert.That(to.IsInitOnly, Is.False, "readonly면 Unity가 직렬화하지 않는다");
        }

        [Test]
        public void Type_IsSerializable()
        {
            Assert.That(typeof(NodeLink).IsDefined(typeof(SerializableAttribute), false), Is.True);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `NodeLink`를 찾을 수 없음.

- [ ] **Step 3: NodeLink를 구현한다**

`Runtime/Core/Graph/NodeLink.cs`:

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드에서 노드로 가는 간선 하나. 실행 흐름만 나른다 — 값은 흐르지 않는다.
    ///
    /// <b>왜 중첩 배열이 아니라 평면 목록인가</b> — 그래프는 <c>NodeLink[]</c> 하나로 저장된다.
    /// 자식 배열을 노드마다 중첩해 두면 YAML에서 노드 하나를 지울 때 파일 전체가 밀려
    /// 머지 충돌이 난다. 평면 목록은 줄 단위로 움직인다.
    ///
    /// <b>순서가 의미를 갖는다</b> — 같은 부모에서 나가는 간선들의 배열 순서가 곧
    /// <c>Sequence</c>의 실행 순서다. 에디터가 이 순서를 유지할 책임을 진다.
    ///
    /// <see cref="NodeId"/>와 같은 이유로 필드가 public이고 readonly가 아니다.
    /// </summary>
    [Serializable]
    public struct NodeLink : IEquatable<NodeLink>
    {
        public NodeId From;
        public NodeId To;

        public NodeLink(NodeId from, NodeId to)
        {
            From = from;
            To = to;
        }

        /// <summary>양 끝이 모두 실제 노드를 가리키는가.</summary>
        public bool IsValid => From.IsValid && To.IsValid;

        public bool Equals(NodeLink other) => From == other.From && To == other.To;

        public override bool Equals(object obj) => obj is NodeLink other && Equals(other);

        public override int GetHashCode() => (From.Value * 397) ^ To.Value;

        public override string ToString() => From + " -> " + To;

        public static bool operator ==(NodeLink a, NodeLink b) => a.Equals(b);

        public static bool operator !=(NodeLink a, NodeLink b) => !a.Equals(b);
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: `Passed: 239`.

- [ ] **Step 5: 실패하는 테스트를 쓴다 — Host 전달**

`Tests~/dotnet/MotionContextHostTests.cs`:

```csharp
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// Host는 코어에 뚫은 구멍 하나다. 코어는 이것이 무엇인지 모르고 나르기만 한다 —
    /// Unity 계층이 MotionPlayer를 넣고, 효과 노드가 캐스트해서 트윈 러너를 꺼낸다.
    /// </summary>
    [TestFixture]
    public sealed class MotionContextHostTests
    {
        private sealed class HostProbeNode : MotionEffectNode
        {
            public object SeenHost;
            public int SeenDepth = -1;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                SeenHost = ctx.Host;
                SeenDepth = ctx.Depth;
                return MotionHandle.Completed;
            }
        }

        [Test]
        public void Context_WithoutHost_HasNullHost()
        {
            var graph = new FakeGraph("g");
            var scope = new MotionScope("t", null);
            var ctx = new MotionContext(graph, scope, null, null);

            Assert.That(ctx.Host, Is.Null);
        }

        [Test]
        public void Context_CarriesHost()
        {
            var host = new object();
            var graph = new FakeGraph("g");
            var scope = new MotionScope("t", null);
            var ctx = new MotionContext(graph, scope, null, null, null, 0, host);

            Assert.That(ctx.Host, Is.SameAs(host));
        }

        [Test]
        public void Runtime_PassesHostToNodes()
        {
            var host = new object();
            var probe = new HostProbeNode();

            var graph = new FakeGraph("g");
            NodeId id = graph.Add(probe);
            graph.DeclareTrigger("Start", id);

            var runtime = new MotionRuntime(graph, null, null, host);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(probe.SeenHost, Is.SameAs(host));
        }

        [Test]
        public void SubGraph_PropagatesHostAndDepth()
        {
            var host = new object();

            var inner = new FakeGraph("inner");
            var probe = new HostProbeNode();
            NodeId innerEntry = inner.Add(probe);
            inner.DeclareTrigger("Start", innerEntry);

            var outer = new FakeGraph("outer");
            var sub = new FakeSubGraphNode { Target = inner };
            NodeId outerEntry = outer.Add(sub);
            outer.DeclareTrigger("Start", outerEntry);

            var runtime = new MotionRuntime(outer, null, null, host);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(probe.SeenHost, Is.SameAs(host), "서브그래프가 Host를 잃으면 안쪽 노드가 트윈 러너를 못 찾는다");
            Assert.That(probe.SeenDepth, Is.EqualTo(1));
        }
    }
}
```

`FakeSubGraphNode`는 `Tests~/dotnet/SubGraphTests.cs`에 이미 있다 (`Target` 속성으로 대상 그래프를 받는다). 새로 만들지 않는다.

- [ ] **Step 6: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `IMotionContext`에 `Host`가 없고, `MotionRuntime`에 4번째 인자를 받는 생성자가 없음.

- [ ] **Step 7: `IMotionContext`에 Host를 추가한다**

`Runtime/Core/Authoring/IMotionContext.cs`의 `int Depth { get; }` 바로 아래에 넣는다:

```csharp
        /// <summary>
        /// 이 실행을 소유한 호스트. <b>코어는 이것이 무엇인지 모른다</b> —
        /// Unity 계층이 <c>MotionPlayer</c>를 넣고 효과 노드가 캐스트해서 트윈 러너를 꺼낸다.
        ///
        /// <see cref="ResolveSlot"/>이 <c>object</c>를 돌려주는 것과 같은 이유다:
        /// 코어가 Unity 타입을 알지 않기 위해서다. 실행기 밖에서 만든 문맥에서는 null일 수 있다.
        /// </summary>
        object Host { get; }
```

- [ ] **Step 8: `MotionContext`에 Host를 흘린다**

`Runtime/Core/Exec/MotionContext.cs`에서 6인자 생성자를 다음으로 바꾸고 7인자 생성자를 추가한다:

```csharp
        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers, int depth)
            : this(graph, scope, resolver, log, triggers, depth, null)
        {
        }

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers, int depth, object host)
        {
            Graph = graph;
            Scope = scope;
            _resolver = resolver;
            Log = log;
            Triggers = triggers;
            Depth = depth;
            Host = host;
        }
```

그리고 `public int Depth { get; }` 아래에 추가한다:

```csharp
        public object Host { get; }
```

- [ ] **Step 9: `MotionRuntime`이 Host를 들고 넘기게 한다**

`Runtime/Core/Exec/MotionRuntime.cs`에서 필드에 추가한다:

```csharp
        private readonly object _host;
```

생성자를 다음 둘로 바꾼다:

```csharp
        public MotionRuntime(IMotionGraphView graph, ISlotResolver resolver, IMotionLog log)
            : this(graph, resolver, log, null)
        {
        }

        /// <param name="host">
        /// 효과 노드가 캐스트해서 쓰는 호스트. Unity 계층은 여기에 <c>MotionPlayer</c>를 넣는다.
        /// </param>
        public MotionRuntime(IMotionGraphView graph, ISlotResolver resolver, IMotionLog log, object host)
        {
            _graph = graph;
            _resolver = resolver;
            _log = new OnceLogger(log);
            _host = host;

            BuildRunners();
        }
```

`CreateScope`의 문맥 생성을 바꾼다:

```csharp
            scope.Begin(new MotionContext(_graph, scope, _resolver, _log, this, 0, _host), entry);
```

- [ ] **Step 10: `SubGraphNode`가 Host를 전파하게 한다**

`Runtime/Core/Nodes/SubGraphNode.cs`의 `innerScope.Begin(...)` 호출을 바꾼다:

```csharp
            innerScope.Begin(
                new MotionContext(inner, innerScope, new ContextSlotResolver(ctx), ctx.Log, ctx.Triggers,
                    ctx.Depth + 1, ctx.Host),
                entry);
```

- [ ] **Step 11: 통과를 확인한다**

`IMotionContext`에 멤버를 추가하면 그것을 구현하는 모든 타입이 깨진다. 현재 구현체는
`MotionContext` 하나뿐이다 (테스트의 `FakeLog`/`FakeSlotResolver`는 다른 인터페이스다).
컴파일이 깨지면 그때 나오는 타입에 `public object Host { get; }`를 채운다.

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: `Failed: 0`, 총 243개.

- [ ] **Step 12: 순수성과 컴파일 게이트를 확인한다**

```bash
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh
```

기대: `purity exit=1`, 컴파일 통과.

- [ ] **Step 13: 커밋**

```bash
git add Runtime/Core Tests~
git commit -m "feat: NodeLink와 문맥 Host 통로 추가"
```

---

### Task 3: 코어 — SlotIntrospector (파생 슬롯 계산)

스펙 5.1의 핵심을 코드로 옮긴다. **슬롯 목록은 저작값이 아니라 파생값이다.** 사람이 슬롯을 추가하지 않는다 — 그래프가 자기 노드들을 훑어 계산한다.

이 클래스가 순수 코어에 있는 이유는 리플렉션과 문자열만 쓰기 때문이다. UnityEngine이 필요 없으니 `dotnet test`로 검증한다.

**Files:**
- Create: `Runtime/Core/Graph/SlotIntrospector.cs`
- Test: `Tests~/dotnet/SlotIntrospectorTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests~/dotnet/SlotIntrospectorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SlotIntrospectorTests
    {
        // 코어는 요구 타입을 검사하지 않고 나르기만 하므로 아무 타입이나 써도 된다.
        private sealed class OneSlotNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target = new SlotRef("Icon");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class SelfOnlyNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target = SlotRef.Self;

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class UntypedSlotNode : MotionEffectNode
        {
            public SlotRef Target = new SlotRef("Bare");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class EmptySlotNode : MotionEffectNode
        {
            public SlotRef Target;

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class TwoSlotNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef First = new SlotRef("A");
            [MotionSlot(typeof(int))] public SlotRef Second = new SlotRef("B");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private abstract class BaseWithSlot : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Inherited = new SlotRef("FromBase");
        }

        private sealed class DerivedWithSlot : BaseWithSlot
        {
            [MotionSlot(typeof(int))] public SlotRef Own = new SlotRef("FromDerived");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class ConflictingTypeNode : MotionEffectNode
        {
            [MotionSlot(typeof(int))] public SlotRef Target = new SlotRef("Icon");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private static List<SlotDeclaration> Collect(params MotionNodeBase[] nodes)
        {
            var into = new List<SlotDeclaration>();
            SlotIntrospector.Collect(nodes, into);
            return into;
        }

        [Test]
        public void NullNodes_YieldsEmpty()
        {
            var into = new List<SlotDeclaration>();
            SlotIntrospector.Collect(null, into);
            Assert.That(into, Is.Empty);
        }

        [Test]
        public void SingleSlot_IsCollectedWithType()
        {
            List<SlotDeclaration> slots = Collect(new OneSlotNode());

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
            Assert.That(slots[0].RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void SelfSlot_IsNotDeclared()
        {
            // Self는 예약 슬롯이라 언제나 플레이어 자신이다. 인스펙터에 뜨면 안 된다.
            Assert.That(Collect(new SelfOnlyNode()), Is.Empty);
        }

        [Test]
        public void EmptyName_IsSkipped()
        {
            Assert.That(Collect(new EmptySlotNode()), Is.Empty);
        }

        [Test]
        public void MissingAttribute_YieldsNullType()
        {
            List<SlotDeclaration> slots = Collect(new UntypedSlotNode());

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].Name, Is.EqualTo("Bare"));
            Assert.That(slots[0].RequiredType, Is.Null);
        }

        [Test]
        public void NullNodeInArray_IsSkipped()
        {
            // 결손 노드. 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
            List<SlotDeclaration> slots = Collect(null, new OneSlotNode(), null);

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
        }

        [Test]
        public void DuplicateName_IsCollapsed_FirstTypeWins()
        {
            List<SlotDeclaration> slots = Collect(new OneSlotNode(), new ConflictingTypeNode());

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void FieldOrder_IsPreserved()
        {
            List<SlotDeclaration> slots = Collect(new TwoSlotNode());

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].Name, Is.EqualTo("A"));
            Assert.That(slots[1].Name, Is.EqualTo("B"));
        }

        [Test]
        public void InheritedSlot_IsCollected_BaseFirst()
        {
            // 순서가 결정적이어야 인스펙터의 슬롯 목록이 리로드마다 흔들리지 않는다.
            List<SlotDeclaration> slots = Collect(new DerivedWithSlot());

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].Name, Is.EqualTo("FromBase"));
            Assert.That(slots[1].Name, Is.EqualTo("FromDerived"));
        }

        [Test]
        public void NodeOrder_IsPreserved()
        {
            List<SlotDeclaration> slots = Collect(new ConflictingTypeNode(), new TwoSlotNode());

            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
            Assert.That(slots[1].Name, Is.EqualTo("A"));
            Assert.That(slots[2].Name, Is.EqualTo("B"));
        }

        [Test]
        public void Collect_ClearsTargetList()
        {
            var into = new List<SlotDeclaration> { new SlotDeclaration("stale") };
            SlotIntrospector.Collect(new MotionNodeBase[] { new OneSlotNode() }, into);

            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Name, Is.EqualTo("Icon"));
        }

        [Test]
        public void RepeatedCalls_AreStable()
        {
            // 타입별 리플렉션 결과를 캐시하므로 두 번째 호출이 첫 번째와 달라지면 안 된다.
            List<SlotDeclaration> first = Collect(new DerivedWithSlot());
            List<SlotDeclaration> second = Collect(new DerivedWithSlot());

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Name, Is.EqualTo(first[i].Name));
                Assert.That(second[i].RequiredType, Is.EqualTo(first[i].RequiredType));
            }
        }

        [Test]
        public void GetSlotFields_ExposesFieldsForEditorRebinding()
        {
            // 에디터가 슬롯 이름을 바꿔 쓰려면 FieldInfo가 필요하다.
            FieldInfo[] fields = SlotIntrospector.GetSlotFields(typeof(TwoSlotNode));

            Assert.That(fields.Length, Is.EqualTo(2));
            Assert.That(fields[0].Name, Is.EqualTo("First"));
            Assert.That(fields[1].Name, Is.EqualTo("Second"));
        }

        [Test]
        public void GetSlotFields_NullType_ReturnsEmpty()
        {
            Assert.That(SlotIntrospector.GetSlotFields(null), Is.Empty);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `SlotIntrospector`를 찾을 수 없음.

- [ ] **Step 3: 구현한다**

`Runtime/Core/Graph/SlotIntrospector.cs`:

```csharp
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
```

- [ ] **Step 4: 통과를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: `Failed: 0`.

- [ ] **Step 5: 순수성 가드를 확인하고 커밋한다**

```bash
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
git add Runtime/Core Tests~
git commit -m "feat: 노드에서 슬롯 선언을 계산하는 SlotIntrospector 추가"
```

---

### Task 4: 코어 — MotionGraphIndex

`IMotionGraphView`의 조회 로직 전부를 순수 코어에 둔다. Unity의 `MotionGraph`는 직렬화 껍데기가 되고 여기에 위임한다. 그래서 그래프 로직이 `dotnet test`로 검증된다.

**Files:**
- Create: `Runtime/Core/Graph/MotionGraphIndex.cs`
- Test: `Tests~/dotnet/MotionGraphIndexTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests~/dotnet/MotionGraphIndexTests.cs`:

```csharp
using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionGraphIndexTests
    {
        private sealed class IdNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target;

            public IdNode(int id, string slotName = null)
            {
                Id = new NodeId(id);
                Target = slotName == null ? default : new SlotRef(slotName);
            }

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private static MotionGraphIndex Build(
            IReadOnlyList<MotionNodeBase> nodes = null,
            IReadOnlyList<NodeLink> links = null,
            IReadOnlyList<TriggerDeclaration> triggers = null,
            IMotionLog log = null)
        {
            return new MotionGraphIndex("g", nodes, links, triggers, log);
        }

        [Test]
        public void EmptyGraph_IsSafe()
        {
            MotionGraphIndex index = Build();

            Assert.That(index.GraphName, Is.EqualTo("g"));
            Assert.That(index.Triggers, Is.Empty);
            Assert.That(index.Slots, Is.Empty);
            Assert.That(index.GetNode(new NodeId(1)), Is.Null);
            Assert.That(index.GetEntry("Start"), Is.EqualTo(NodeId.None));
            Assert.That(index.GetChildren(new NodeId(1)), Is.Empty);
        }

        [Test]
        public void BlankName_FallsBack()
        {
            var index = new MotionGraphIndex(null, null, null, null, null);
            Assert.That(index.GraphName, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetNode_FindsByDeclaredId()
        {
            var a = new IdNode(7);
            MotionGraphIndex index = Build(new MotionNodeBase[] { a });

            Assert.That(index.GetNode(new NodeId(7)), Is.SameAs(a));
            Assert.That(index.GetNode(new NodeId(8)), Is.Null);
        }

        [Test]
        public void NullNode_IsSkipped_RestSurvives()
        {
            // 결손 노드. 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
            var a = new IdNode(2);
            MotionGraphIndex index = Build(new MotionNodeBase[] { null, a, null });

            Assert.That(index.GetNode(new NodeId(2)), Is.SameAs(a));
        }

        [Test]
        public void NodeWithoutId_IsSkippedAndLogged()
        {
            var log = new FakeLog();
            var orphan = new IdNode(0);

            MotionGraphIndex index = Build(new MotionNodeBase[] { orphan }, log: log);

            Assert.That(index.GetNode(NodeId.None), Is.Null);
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateId_FirstWins_AndLogs()
        {
            var log = new FakeLog();
            var first = new IdNode(1);
            var second = new IdNode(1);

            MotionGraphIndex index = Build(new MotionNodeBase[] { first, second }, log: log);

            Assert.That(index.GetNode(new NodeId(1)), Is.SameAs(first));
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetChildren_PreservesLinkOrder()
        {
            // 같은 부모에서 나가는 간선의 배열 순서가 곧 Sequence의 실행 순서다.
            var nodes = new MotionNodeBase[] { new IdNode(1), new IdNode(2), new IdNode(3), new IdNode(4) };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(3)),
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(1), new NodeId(4)),
            };

            IReadOnlyList<NodeId> children = Build(nodes, links).GetChildren(new NodeId(1));

            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(children[0], Is.EqualTo(new NodeId(3)));
            Assert.That(children[1], Is.EqualTo(new NodeId(2)));
            Assert.That(children[2], Is.EqualTo(new NodeId(4)));
        }

        [Test]
        public void GetChildren_NoChildren_ReturnsEmptyNotNull()
        {
            MotionGraphIndex index = Build(new MotionNodeBase[] { new IdNode(1) });

            IReadOnlyList<NodeId> children = index.GetChildren(new NodeId(1));

            Assert.That(children, Is.Not.Null);
            Assert.That(children, Is.Empty);
        }

        [Test]
        public void InvalidLink_IsDropped()
        {
            var nodes = new MotionNodeBase[] { new IdNode(1) };
            var links = new[] { new NodeLink(new NodeId(1), NodeId.None) };

            Assert.That(Build(nodes, links).GetChildren(new NodeId(1)), Is.Empty);
        }

        [Test]
        public void LinkToMissingNode_IsKept()
        {
            // 구조적으로는 멀쩡한 간선이다. 노드가 결손인지는 NodeRun이 판단한다 —
            // "없는 노드"를 두 곳에서 다르게 처리하면 동작을 추론할 수 없게 된다.
            var nodes = new MotionNodeBase[] { new IdNode(1) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(99)) };

            IReadOnlyList<NodeId> children = Build(nodes, links).GetChildren(new NodeId(1));

            Assert.That(children.Count, Is.EqualTo(1));
            Assert.That(children[0], Is.EqualTo(new NodeId(99)));
        }

        [Test]
        public void GetEntry_FindsDeclaredTrigger()
        {
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(5)) };
            MotionGraphIndex index = Build(triggers: triggers);

            Assert.That(index.GetEntry("Start"), Is.EqualTo(new NodeId(5)));
            Assert.That(index.GetEntry("Loop"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void GetEntry_IsCaseSensitive()
        {
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(5)) };

            Assert.That(Build(triggers: triggers).GetEntry("start"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void DuplicateTrigger_FirstWins_AndLogs()
        {
            var log = new FakeLog();
            var triggers = new[]
            {
                new TriggerDeclaration("Start", new NodeId(1)),
                new TriggerDeclaration("Start", new NodeId(2)),
            };

            MotionGraphIndex index = Build(triggers: triggers, log: log);

            Assert.That(index.GetEntry("Start"), Is.EqualTo(new NodeId(1)));
            Assert.That(index.Triggers.Count, Is.EqualTo(1), "런타임이 러너를 두 번 만들면 안 된다");
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void NamelessTrigger_IsDropped()
        {
            var triggers = new[] { new TriggerDeclaration(null, new NodeId(1)) };

            Assert.That(Build(triggers: triggers).Triggers, Is.Empty);
        }

        [Test]
        public void NullTriggerEntry_IsDropped()
        {
            var triggers = new TriggerDeclaration[] { null };

            Assert.That(Build(triggers: triggers).Triggers, Is.Empty);
        }

        [Test]
        public void Slots_AreDerivedFromNodes()
        {
            var nodes = new MotionNodeBase[] { new IdNode(1, "Icon"), new IdNode(2, "Label") };

            IReadOnlyList<SlotDeclaration> slots = Build(nodes).Slots;

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
            Assert.That(slots[1].Name, Is.EqualTo("Label"));
            Assert.That(slots[0].RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void Index_DrivesMotionRuntime()
        {
            // Index가 IMotionGraphView로 실제 실행기에 그대로 꽂히는지 확인한다.
            var log = new ExecutionLog();
            var first = new RecordingEffect("a") { Log = log, Id = new NodeId(1) };
            var second = new RecordingEffect("b") { Log = log, Id = new NodeId(2) };

            var index = new MotionGraphIndex("g",
                new MotionNodeBase[] { first, second },
                new[] { new NodeLink(new NodeId(1), new NodeId(2)) },
                new[] { new TriggerDeclaration("Start", new NodeId(1)) },
                null);

            var runtime = new MotionRuntime(index, null, null);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(log.Entries, Does.Contain("a:start"));
            Assert.That(log.Entries, Does.Contain("b:start"));
        }
    }
}
```

`RecordingEffect`는 `Id`를 생성자에서 받지 않으므로 위처럼 객체 초기자로 `Id`를 채운다 (`MotionNodeBase.Id`는 public 필드다).

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `MotionGraphIndex`를 찾을 수 없음.

- [ ] **Step 3: 구현한다**

`Runtime/Core/Graph/MotionGraphIndex.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 직렬화된 배열들(노드 · 간선 · 트리거)을 실행기가 쓸 수 있는 조회 구조로 바꾼다.
    ///
    /// <b>이것이 <see cref="IMotionGraphView"/> 구현의 전부다.</b> Unity의 <c>MotionGraph</c>는
    /// ScriptableObject 껍데기일 뿐이고 모든 조회를 여기에 위임한다. 그 덕분에 그래프 로직이
    /// UnityEngine 없이 <c>dotnet test</c>로 검증된다.
    ///
    /// <b>읽기 전용이다.</b> 만들 때 한 번 계산하고 그 뒤로는 바뀌지 않는다. 그래프가 실행
    /// 상태를 갖지 않는다는 불변식이 여기에 걸려 있다 — 같은 에셋을 수백 개 오브젝트가
    /// 동시에 쓰기 때문이다. 저작 쪽이 배열을 바꾸면 인덱스를 <b>새로 만든다</b>.
    /// </summary>
    public sealed class MotionGraphIndex : IMotionGraphView
    {
        private static readonly NodeId[] NoChildren = new NodeId[0];

        private readonly Dictionary<int, MotionNodeBase> _nodes = new Dictionary<int, MotionNodeBase>();
        private readonly Dictionary<int, List<NodeId>> _children = new Dictionary<int, List<NodeId>>();
        private readonly Dictionary<string, NodeId> _entries = new Dictionary<string, NodeId>(StringComparer.Ordinal);
        private readonly List<TriggerDeclaration> _triggers = new List<TriggerDeclaration>();
        private readonly List<SlotDeclaration> _slots = new List<SlotDeclaration>();

        public MotionGraphIndex(
            string graphName,
            IReadOnlyList<MotionNodeBase> nodes,
            IReadOnlyList<NodeLink> links,
            IReadOnlyList<TriggerDeclaration> triggers,
            IMotionLog log)
        {
            GraphName = string.IsNullOrEmpty(graphName) ? "<unnamed graph>" : graphName;

            IndexNodes(nodes, log);
            IndexLinks(links);
            IndexTriggers(triggers, log);

            SlotIntrospector.Collect(nodes, _slots);
        }

        public string GraphName { get; }

        public IReadOnlyList<TriggerDeclaration> Triggers => _triggers;

        public IReadOnlyList<SlotDeclaration> Slots => _slots;

        public MotionNodeBase GetNode(NodeId id)
        {
            MotionNodeBase node;
            return _nodes.TryGetValue(id.Value, out node) ? node : null;
        }

        public NodeId GetEntry(string triggerName)
        {
            if (triggerName == null)
            {
                return NodeId.None;
            }

            NodeId entry;
            return _entries.TryGetValue(triggerName, out entry) ? entry : NodeId.None;
        }

        public IReadOnlyList<NodeId> GetChildren(NodeId parent)
        {
            List<NodeId> list;
            return _children.TryGetValue(parent.Value, out list) ? (IReadOnlyList<NodeId>)list : NoChildren;
        }

        private void IndexNodes(IReadOnlyList<MotionNodeBase> nodes, IMotionLog log)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                MotionNodeBase node = nodes[i];

                // 결손 노드 — 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
                // 조용히 건너뛰고 나머지 그래프를 살린다. 경고를 내지 않는 이유는
                // 에디터가 결손을 눈에 보이게 표시할 것이기 때문이다.
                if (node == null)
                {
                    continue;
                }

                if (!node.Id.IsValid)
                {
                    Warn(log, "node of type " + node.GetType().Name + " has no id and was dropped");
                    continue;
                }

                if (_nodes.ContainsKey(node.Id.Value))
                {
                    Warn(log, "duplicate node id " + node.Id + "; the first one wins");
                    continue;
                }

                _nodes[node.Id.Value] = node;
            }
        }

        private void IndexLinks(IReadOnlyList<NodeLink> links)
        {
            if (links == null)
            {
                return;
            }

            for (int i = 0; i < links.Count; i++)
            {
                NodeLink link = links[i];
                if (!link.IsValid)
                {
                    continue;
                }

                List<NodeId> list;
                if (!_children.TryGetValue(link.From.Value, out list))
                {
                    list = new List<NodeId>();
                    _children[link.From.Value] = list;
                }

                list.Add(link.To);
            }
        }

        private void IndexTriggers(IReadOnlyList<TriggerDeclaration> triggers, IMotionLog log)
        {
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null || string.IsNullOrEmpty(decl.Name))
                {
                    continue;
                }

                if (_entries.ContainsKey(decl.Name))
                {
                    Warn(log, "duplicate trigger '" + decl.Name + "'; the first one wins");
                    continue;
                }

                _entries[decl.Name] = decl.Entry;
                _triggers.Add(decl);
            }
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

- [ ] **Step 4: 통과를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: `Failed: 0`.

- [ ] **Step 5: 순수성 가드를 확인하고 커밋한다**

```bash
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
git add Runtime/Core Tests~
git commit -m "feat: 그래프 조회 로직을 담는 MotionGraphIndex 추가"
```

---

### Task 5: Unity — MotionGraph 에셋

**Files:**
- Create: `Runtime/Unity/Graph/MotionGraph.cs`
- Create: `Runtime/Unity/Graph/MotionGraphAuthoring.cs`

이 태스크부터는 `dotnet test`가 아니라 `./Tools~/compile-check/run.sh`가 게이트다. 그래프 조회 로직은 이미 Task 4에서 테스트됐다 — 여기서 새로 짜는 것은 직렬화 껍데기뿐이다.

- [ ] **Step 1: MotionGraph를 만든다**

`Runtime/Unity/Graph/MotionGraph.cs`:

```csharp
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
```

**인덱스 무효화 경로가 왜 셋인가** — 그래프는 세 가지 경로로 바뀐다. 저작 API(Task 5 Step 2)는
`Invalidate()`를 직접 부른다. 에디터 창이 `SerializedObject.ApplyModifiedProperties()`로 바꾸면
Unity가 `OnValidate`를 부른다. 도메인 리로드와 에셋 재로드는 `OnEnable`을 부른다. 셋 중 하나라도
빠지면 **조용히 낡은 인덱스가 산다** — 노드를 지웠는데 계속 재생되는 종류의 버그가 되고,
그때는 원인이 인덱스라는 것을 짐작하기 어렵다.

- [ ] **Step 2: 저작 API를 만든다**

에디터 패키지는 별도 어셈블리라서 그래프를 바꾸려면 public API가 필요하다.

`Runtime/Unity/Graph/MotionGraphAuthoring.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프를 바꾸는 쪽. <b>에디터 저작용이다</b> — 런타임에 부르면 프로젝트의 에셋을
    /// 통째로 바꾸는 것이 되므로 절대 부르지 않는다.
    ///
    /// 별도 어셈블리인 에디터 패키지가 써야 하므로 public이고, <c>#if UNITY_EDITOR</c>로
    /// 감싸지 않는다.
    ///
    /// <b>규칙: 이 클래스에 메서드를 추가하면 마지막 줄이 <c>Invalidate()</c>여야 한다.</b>
    /// 강제하는 장치가 없으므로 사람이 지켜야 한다. 빠뜨리면 낡은 인덱스가 조용히 살아남는다.
    /// </summary>
    public sealed partial class MotionGraph
    {
        /// <summary>노드를 넣고 새 id를 부여해 돌려준다.</summary>
        public NodeId AddNode(MotionNodeBase node)
        {
            if (node == null)
            {
                return NodeId.None;
            }

            var id = new NodeId(_nextNodeId++);
            node.Id = id;
            _nodes.Add(node);
            Invalidate();
            return id;
        }

        /// <summary>
        /// 노드와 그것에 닿는 간선을 전부 지운다. 그 노드를 진입점으로 삼던 트리거는
        /// 진입점을 잃고 <see cref="NodeId.None"/>이 된다 — 트리거 자체를 조용히 지우지는 않는다.
        /// </summary>
        public bool RemoveNode(NodeId id)
        {
            if (!id.IsValid)
            {
                return false;
            }

            bool removed = false;

            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i] != null && _nodes[i].Id == id)
                {
                    _nodes.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
            {
                return false;
            }

            for (int i = _links.Count - 1; i >= 0; i--)
            {
                if (_links[i].From == id || _links[i].To == id)
                {
                    _links.RemoveAt(i);
                }
            }

            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i] != null && _triggers[i].Entry == id)
                {
                    _triggers[i].Entry = NodeId.None;
                }
            }

            Invalidate();
            return true;
        }

        /// <summary>간선을 잇는다. 같은 간선을 두 번 넣지 않는다.</summary>
        public bool Link(NodeId from, NodeId to)
        {
            var link = new NodeLink(from, to);
            if (!link.IsValid || _links.Contains(link))
            {
                return false;
            }

            _links.Add(link);
            Invalidate();
            return true;
        }

        public bool Unlink(NodeId from, NodeId to)
        {
            if (!_links.Remove(new NodeLink(from, to)))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        /// <summary>
        /// 간선의 순서를 바꾼다. 같은 부모에서 나가는 간선의 순서가 곧 실행 순서이므로
        /// 그래프 창의 "위로/아래로"가 이것을 부른다.
        /// </summary>
        public bool MoveLink(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _links.Count || toIndex < 0 || toIndex >= _links.Count)
            {
                return false;
            }

            NodeLink link = _links[fromIndex];
            _links.RemoveAt(fromIndex);
            _links.Insert(toIndex, link);
            Invalidate();
            return true;
        }

        /// <summary>트리거를 선언하거나 이미 있으면 덮어쓴다.</summary>
        public void SetTrigger(string triggerName, NodeId entry, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i] != null && _triggers[i].Name == triggerName)
                {
                    _triggers[i].Entry = entry;
                    _triggers[i].Policy = policy;
                    Invalidate();
                    return;
                }
            }

            _triggers.Add(new TriggerDeclaration(triggerName, entry, policy));
            Invalidate();
        }

        public bool RemoveTrigger(string triggerName)
        {
            for (int i = _triggers.Count - 1; i >= 0; i--)
            {
                if (_triggers[i] != null && _triggers[i].Name == triggerName)
                {
                    _triggers.RemoveAt(i);
                    Invalidate();
                    return true;
                }
            }

            return false;
        }

        /// <summary>파생 인덱스를 버린다. 다음 조회에서 다시 계산된다.</summary>
        public void Invalidate()
        {
            _index = null;
        }
    }
}
```

- [ ] **Step 3: 컴파일 게이트를 통과시킨다**

```bash
./Tools~/compile-check/run.sh
```

기대: 통과.

- [ ] **Step 4: 커밋**

```bash
git add Runtime/Unity
git commit -m "feat: MotionGraph 에셋과 저작 API 추가"
```

---

### Task 6: Unity — 슬롯 해석

**Files:**
- Create: `Runtime/Core/Diag/MotionLogs.cs`
- Create: `Runtime/Unity/Runtime/SlotBinding.cs`
- Create: `Runtime/Unity/Runtime/SlotTable.cs`
- Create: `Runtime/Unity/Runtime/MotionSlots.cs`
- Test: `Tests~/dotnet/MotionLogsTests.cs`

- [ ] **Step 1: 실패하는 테스트를 쓴다 — 코어의 경고 억제 헬퍼**

미할당 슬롯 경고는 노드가 재생될 때마다 나온다. `Loop`가 0.5초마다 도는 연출이 세 시간 켜져 있으면 2만 줄이다. 코어의 `OnceLogger`가 이미 억제를 아는데, `IMotionLog` 인터페이스에는 그 통로가 없다. 헬퍼를 하나 둔다.

`Tests~/dotnet/MotionLogsTests.cs`:

```csharp
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionLogsTests
    {
        [Test]
        public void NullLog_IsSafe()
        {
            Assert.DoesNotThrow(() => MotionLogs.WarnOnce(null, "k", "m"));
        }

        [Test]
        public void OnceLogger_SuppressesRepeats()
        {
            var sink = new FakeLog();
            var once = new OnceLogger(sink);

            MotionLogs.WarnOnce(once, "slot:Icon", "unbound slot 'Icon'");
            MotionLogs.WarnOnce(once, "slot:Icon", "unbound slot 'Icon'");
            MotionLogs.WarnOnce(once, "slot:Icon", "unbound slot 'Icon'");

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnceLogger_DifferentKeys_BothPass()
        {
            var sink = new FakeLog();
            var once = new OnceLogger(sink);

            MotionLogs.WarnOnce(once, "slot:Icon", "a");
            MotionLogs.WarnOnce(once, "slot:Label", "b");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void PlainLog_FallsBackToWarn()
        {
            // 억제를 모르는 로그라도 경고 자체는 나와야 한다. 조용한 실패가 최악이다.
            var sink = new FakeLog();

            MotionLogs.WarnOnce(sink, "k", "m");
            MotionLogs.WarnOnce(sink, "k", "m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인하고 구현한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패.

`Runtime/Core/Diag/MotionLogs.cs`:

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 <see cref="IMotionLog"/> 하나만 들고도 억제된 경고를 낼 수 있게 하는 헬퍼.
    ///
    /// <see cref="IMotionContext.Log"/>의 타입은 <see cref="IMotionLog"/>라 억제 통로가 없다.
    /// 그런데 <see cref="MotionRuntime"/>이 넘기는 실제 인스턴스는 <see cref="OnceLogger"/>다.
    /// 인터페이스를 넓히는 대신 여기서 한 번 내려찍는다.
    /// </summary>
    public static class MotionLogs
    {
        /// <summary>
        /// <paramref name="key"/>가 처음일 때만 경고한다. 억제를 모르는 로그면 그냥 경고한다 —
        /// 조용한 실패보다 시끄러운 편이 낫다.
        /// </summary>
        public static void WarnOnce(IMotionLog log, string key, string message)
        {
            if (log == null)
            {
                return;
            }

            var once = log as OnceLogger;
            if (once != null)
            {
                once.WarnOnce(key, message);
                return;
            }

            log.Warn(message);
        }
    }
}
```

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: `Failed: 0`.

- [ ] **Step 3: SlotBinding을 만든다**

`Runtime/Unity/Runtime/SlotBinding.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯 이름 하나와 그것이 가리키는 실제 오브젝트. <b>프리팹의 플레이어가 갖는다</b> —
    /// 그래프가 아니다. 그래서 그래프 에셋 하나를 여러 프리팹이 재사용할 수 있다.
    ///
    /// <see cref="Target"/>이 <c>UnityEngine.Object</c>인 이유는 무엇이 들어올지 모르기
    /// 때문이다. 노드가 요구하는 타입으로 바꾸는 것은 <see cref="MotionSlots"/>가 한다.
    /// </summary>
    [Serializable]
    public struct SlotBinding
    {
        public string Name;
        public UnityEngine.Object Target;

        public SlotBinding(string name, UnityEngine.Object target)
        {
            Name = name;
            Target = target;
        }
    }
}
```

- [ ] **Step 4: SlotTable을 만든다**

`Runtime/Unity/Runtime/SlotTable.cs`:

```csharp
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
```

- [ ] **Step 5: MotionSlots를 만든다**

`Runtime/Unity/Runtime/MotionSlots.cs`:

```csharp
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯을 노드가 원하는 타입으로 바꾼다.
    ///
    /// 코어의 <see cref="ISlotResolver"/>는 <c>object</c>만 돌려준다 — 코어가 Unity 타입을
    /// 모르기 위해서다. 그 <c>object</c>를 <c>RectTransform</c>이나 <c>CanvasGroup</c>으로
    /// 바꾸는 일이 여기서 일어난다.
    ///
    /// 바인딩된 것이 컴포넌트든 게임오브젝트든 <c>GetComponent</c>로 찾아 준다. 인스펙터에
    /// 무엇을 끌어다 놓든 의도대로 동작하게 하기 위해서다.
    /// </summary>
    public static class MotionSlots
    {
        /// <summary>
        /// 해석에 실패하면 null을 돌려주고 <b>인스턴스당 한 번만</b> 경고한다.
        /// 방치형 게임에서 매 프레임 경고가 나오면 진짜 문제를 찾을 수 없다.
        /// </summary>
        public static T Resolve<T>(IMotionContext ctx, SlotRef slot) where T : class
        {
            if (ctx == null)
            {
                return null;
            }

            object raw = ctx.ResolveSlot(slot);
            if (raw == null)
            {
                WarnUnbound(ctx, slot, typeof(T), "is not bound");
                return null;
            }

            T coerced = Coerce<T>(raw);
            if (coerced == null)
            {
                WarnUnbound(ctx, slot, typeof(T), "is bound to " + raw.GetType().Name + " which has no");
            }

            return coerced;
        }

        private static T Coerce<T>(object raw) where T : class
        {
            var direct = raw as T;
            if (direct != null)
            {
                return direct;
            }

            var component = raw as Component;
            GameObject go = component != null ? component.gameObject : raw as GameObject;

            if (go == null)
            {
                return null;
            }

            // GameObject 자체를 원하는 노드(SetActive 등)도 있다.
            var wanted = go as T;
            if (wanted != null)
            {
                return wanted;
            }

            return go.GetComponent(typeof(T)) as T;
        }

        private static void WarnUnbound(IMotionContext ctx, SlotRef slot, System.Type wanted, string reason)
        {
            string graphName = ctx.Graph == null ? "<no graph>" : ctx.Graph.GraphName;
            string key = "slot:" + graphName + ":" + slot.Name + ":" + wanted.Name;

            MotionLogs.WarnOnce(ctx.Log, key,
                "graph '" + graphName + "': slot '" + slot + "' " + reason + " " + wanted.Name +
                "; the node was skipped");
        }
    }
}
```

- [ ] **Step 6: 게이트를 전부 돌리고 커밋한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh
git add Runtime Tests~
git commit -m "feat: 슬롯 바인딩과 타입 해석 추가"
```

---

### Task 7: Unity — 트윈 백엔드

**Files:**
- Create: `Runtime/Unity/Tween/IMotionTweenRunner.cs`
- Create: `Runtime/Unity/Tween/BuiltinTweenRunner.cs`
- Create: `Runtime/Unity/Tween/MotionContextExtensions.cs`

- [ ] **Step 1: 인터페이스를 만든다**

`Runtime/Unity/Tween/IMotionTweenRunner.cs`:

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 교체 가능한 트윈 백엔드.
    ///
    /// <b>표면이 일부러 작다.</b> 러너는 "시간이 어떻게 흐르고 이징을 어떻게 샘플링하는가"만
    /// 알고, "RectTransform을 어떻게 움직이는가"는 모른다. 그래서 효과 노드를 백엔드마다
    /// 다시 짤 필요가 없다 — 노드는 한 번만 짜고 백엔드만 갈아 끼운다.
    ///
    /// 기본 구현은 코어의 타이머다(<see cref="BuiltinTweenRunner"/>). DOTween 백엔드는
    /// 별도 패키지에서 이 인터페이스를 구현한다.
    /// </summary>
    public interface IMotionTweenRunner
    {
        /// <summary>
        /// <paramref name="duration"/>초 동안 <b>이징된</b> 진행률을 <paramref name="onEased"/>로 흘린다.
        ///
        /// 계약:
        /// <list type="bullet">
        /// <item>끝날 때 정확히 1을 한 번 보낸다. 그러지 않으면 페이드인이 0.97에서 멈춘다</item>
        /// <item><paramref name="duration"/>이 0 이하면 즉시 1을 한 번 보내고 끝난다</item>
        /// <item>이징이 1을 넘길 수 있다(OutBack · OutElastic). 노드는 <c>LerpUnclamped</c>를 쓴다</item>
        /// </list>
        /// </summary>
        IMotionHandle Run(float duration, EaseKind ease, Action<float> onEased);
    }
}
```

- [ ] **Step 2: 기본 구현을 만든다**

`Runtime/Unity/Tween/BuiltinTweenRunner.cs`:

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 의존성 0인 기본 트윈 러너. 코어의 타이머 핸들과 이징 표만 쓴다.
    ///
    /// 이것이 있어서 패키지가 어떤 외부 트윈 라이브러리도 없이 완전히 동작한다.
    /// DOTween은 유료 에셋이라 코어에 넣으면 MIT 배포가 막힌다.
    ///
    /// 상태가 없으므로 인스턴스 하나를 공유한다.
    /// </summary>
    public sealed class BuiltinTweenRunner : IMotionTweenRunner
    {
        public static readonly BuiltinTweenRunner Shared = new BuiltinTweenRunner();

        public IMotionHandle Run(float duration, EaseKind ease, Action<float> onEased)
        {
            if (onEased == null)
            {
                return MotionHandle.FromTimer(duration, null);
            }

            EaseKind captured = ease;
            return MotionHandle.FromTimer(duration, delegate(float t)
            {
                onEased(EaseLibrary.Evaluate(captured, t));
            });
        }
    }
}
```

- [ ] **Step 3: 문맥 확장을 만든다**

`Runtime/Unity/Tween/MotionContextExtensions.cs`:

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 효과 노드가 트윈 러너에 닿는 통로.
    ///
    /// 코어의 <see cref="IMotionContext"/>는 트윈을 모른다. 대신 <c>Host</c>를 나르는데,
    /// Unity 계층은 거기에 <see cref="MotionPlayer"/>를 넣는다. 여기서 그것을 꺼낸다.
    ///
    /// 호스트가 없거나 러너가 꽂히지 않았으면 <see cref="BuiltinTweenRunner"/>로 폴백한다 —
    /// 노드가 null을 만나 죽는 경로를 아예 없앤다.
    /// </summary>
    public static class MotionContextExtensions
    {
        public static IMotionTweenRunner Tween(this IMotionContext ctx)
        {
            if (ctx == null)
            {
                return BuiltinTweenRunner.Shared;
            }

            var player = ctx.Host as MotionPlayer;
            if (player == null || player.TweenRunner == null)
            {
                return BuiltinTweenRunner.Shared;
            }

            return player.TweenRunner;
        }
    }
}
```

이 파일은 `MotionPlayer`가 생긴 뒤에야 컴파일된다. Task 9까지 컴파일 게이트가 깨져 있어도 정상이다 — Task 7·8을 먼저 커밋하지 말고 **Task 9까지 마친 뒤 한 번에** 게이트를 돌린다.

- [ ] **Step 4: 아직 커밋하지 않는다**

`MotionPlayer`가 없어 컴파일이 되지 않는다. Task 8·9를 이어서 하고 Task 9의 마지막에 함께 커밋한다.

---

### Task 8: Unity — MotionPump

**Files:**
- Create: `Runtime/Unity/Runtime/MotionPump.cs`

- [ ] **Step 1: 펌프를 만든다**

`Runtime/Unity/Runtime/MotionPump.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 살아 있는 모든 플레이어를 한 <c>Update</c>에서 굴리는 전역 펌프.
    ///
    /// <b>왜 플레이어마다 Update를 두지 않는가</b> — 방치형 게임이라 인벤토리 슬롯 수백 개가
    /// 동시에 산다. <c>MonoBehaviour.Update</c>는 호출마다 네이티브에서 매니지드로 넘어오므로
    /// 300개면 그 비용이 300배다. 펌프는 1회다.
    ///
    /// 에디터에서 플레이 중이 아닐 때는 만들지 않는다. 씬에 숨은 오브젝트를 남기면 안 되기
    /// 때문이다. 에디터 프리뷰는 <see cref="MotionPlayer.TickFromPump"/>를 직접 부른다.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class MotionPump : MonoBehaviour
    {
        private static MotionPump _instance;
        private static bool _quitting;

        private readonly List<MotionPlayer> _players = new List<MotionPlayer>();
        private bool _ticking;
        private bool _needsCompact;

        public static void Register(MotionPlayer player)
        {
            if (player == null || !Application.isPlaying || _quitting)
            {
                return;
            }

            MotionPump pump = EnsureInstance();
            if (pump == null || pump._players.Contains(player))
            {
                return;
            }

            pump._players.Add(player);
        }

        public static void Unregister(MotionPlayer player)
        {
            if (_instance == null || player == null)
            {
                return;
            }

            List<MotionPlayer> players = _instance._players;
            int index = players.IndexOf(player);
            if (index < 0)
            {
                return;
            }

            // 틱 도중에 목록을 줄이면 인덱스가 어긋난다. 자리를 비워 두고 나중에 정리한다.
            if (_instance._ticking)
            {
                players[index] = null;
                _instance._needsCompact = true;
                return;
            }

            players.RemoveAt(index);
        }

        private static MotionPump EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("[UiMotion Pump]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);

            _instance = go.AddComponent<MotionPump>();
            return _instance;
        }

        private void Update()
        {
            float unscaled = Time.unscaledDeltaTime;
            float scaled = Time.deltaTime;

            _ticking = true;

            // 틱 도중에 플레이어가 비활성화되거나 파괴될 수 있으므로 매번 다시 검사한다.
            for (int i = 0; i < _players.Count; i++)
            {
                MotionPlayer player = _players[i];

                if (player == null)
                {
                    _players[i] = null;
                    _needsCompact = true;
                    continue;
                }

                player.TickFromPump(unscaled, scaled);
            }

            _ticking = false;

            if (_needsCompact)
            {
                Compact();
            }
        }

        private void Compact()
        {
            _needsCompact = false;

            for (int i = _players.Count - 1; i >= 0; i--)
            {
                if (_players[i] == null)
                {
                    _players.RemoveAt(i);
                }
            }
        }

        private void OnApplicationQuit()
        {
            // 종료 중에 펌프를 되살리면 "파괴된 씬에 오브젝트를 만들 수 없다" 오류가 난다.
            _quitting = true;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }
    }
}
```

- [ ] **Step 2: 아직 커밋하지 않는다**

`MotionPlayer`가 없어 컴파일이 되지 않는다. Task 9로 이어간다.

---

### Task 9: Unity — MotionPlayer

프리팹에 붙는 유일한 컴포넌트다. Task 7·8이 이것을 기다리고 있으므로 이 태스크 끝에서 셋을 함께 커밋한다.

**Files:**
- Create: `Runtime/Unity/Runtime/MotionPlayer.cs`

- [ ] **Step 1: MotionPlayer를 만든다**

`Runtime/Unity/Runtime/MotionPlayer.cs`:

```csharp
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
        [NonSerialized] private UnityMotionLog _log;
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

        public void Fire(string trigger)
        {
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
                    _runtime = null;
                    return;
                }
            }

            var grown = new SlotBinding[_bindings.Length + 1];
            Array.Copy(_bindings, grown, _bindings.Length);
            grown[_bindings.Length] = new SlotBinding(slotName, target);
            _bindings = grown;
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
            EnsureRuntime();

            if (_runtime != null)
            {
                // 다시 활성화되면 경고 억제를 푼다. 지난번에 이미 경고한 문제를
                // 이번에는 못 보고 넘어가면 안 된다.
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

            if (_log == null)
            {
                _log = new UnityMotionLog(this);
            }

            var slots = new SlotTable(transform, _bindings);
            _runtime = new MotionRuntime(_graph, slots, _log, this);
            return _runtime;
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
```

- [ ] **Step 2: 컴파일 게이트를 통과시킨다**

Task 7·8·9가 여기서 처음으로 함께 컴파일된다.

```bash
./Tools~/compile-check/run.sh
```

기대: 통과. 오류가 나면 대개 `MotionPlayer`/`MotionPump`/`IMotionTweenRunner`의 상호 참조 오타다.

- [ ] **Step 3: 커밋**

```bash
git add Runtime/Unity
git commit -m "feat: MotionPlayer와 전역 틱 펌프, 트윈 백엔드 추가"
```

---

### Task 10: Unity — 효과 노드 베이스와 신호

13개 효과 노드가 공유할 것들을 먼저 만든다.

**Files:**
- Create: `Runtime/Unity/Nodes/UnityEffectNode.cs`
- Create: `Runtime/Unity/Nodes/SubGraphAssetNode.cs`
- Create: `Runtime/Unity/Runtime/MotionSignals.cs`

- [ ] **Step 1: 효과 노드 베이스를 만든다**

`Runtime/Unity/Nodes/UnityEffectNode.cs`:

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// Unity 대상을 만지는 효과 노드의 공통 베이스.
    ///
    /// 13개 노드가 전부 똑같이 하는 세 가지 — 슬롯 해석, 원상 복구 등록, 트윈 실행 —
    /// 를 여기 모은다. 노드를 새로 만드는 사람이 그 규약을 다시 발명하지 않게 하기 위해서다.
    ///
    /// <b>이 클래스에는 <c>[MotionNode]</c>를 달지 않는다.</b> 어트리뷰트가
    /// <c>Inherited = false</c>라 파생이 물려받지 않으므로 팔레트에 뜨지 않는다.
    /// </summary>
    [Serializable]
    public abstract class UnityEffectNode : MotionEffectNode
    {
        /// <summary>슬롯을 원하는 타입으로. 실패하면 null이고 경고가 한 번 남는다.</summary>
        protected static T Resolve<T>(IMotionContext ctx, SlotRef slot) where T : class
        {
            return MotionSlots.Resolve<T>(ctx, slot);
        }

        /// <summary>
        /// 취소될 때 되돌릴 것을 등록한다. <b>자연 완료 시에는 실행되지 않는다</b> —
        /// 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다.
        /// </summary>
        protected static void Remember(IMotionContext ctx, Action revert)
        {
            if (ctx != null && ctx.Scope != null && revert != null)
            {
                ctx.Scope.Remember(revert);
            }
        }

        /// <summary>
        /// 이징된 진행률을 흘리는 트윈을 시작한다. 호스트에 백엔드가 꽂혀 있으면 그것을,
        /// 아니면 내장 러너를 쓴다.
        /// </summary>
        protected static IMotionHandle Run(IMotionContext ctx, float duration, EaseKind ease, Action<float> onEased)
        {
            return ctx.Tween().Run(duration, ease, onEased);
        }
    }
}
```

- [ ] **Step 2: 서브그래프 노드를 만든다**

`Runtime/Unity/Nodes/SubGraphAssetNode.cs`:

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 다른 그래프 에셋을 노드 하나처럼 실행한다. 자주 쓰는 관용구를 한 곳에서 고치기 위한
    /// 재사용 단위다 — 복붙 그래프가 쌓이는 것을 막는다.
    ///
    /// 슬롯은 바깥 그래프와 공유한다. 같은 플레이어의 같은 계층이기 때문이다.
    /// </summary>
    [MotionNode(
        Name = "Sub Graph",
        Category = "Flow",
        Summary = "다른 그래프를 여기서 통째로 재생한다. 슬롯은 바깥과 공유한다.",
        Sample = "SubGraph")]
    [Serializable]
    public sealed class SubGraphAssetNode : SubGraphNode
    {
        [MotionParam(Label = "그래프", Tooltip = "재생할 그래프 에셋.")]
        public MotionGraph Target;

        protected override IMotionGraphView ResolveGraph()
        {
            // Unity의 가짜 null을 진짜 null로 바꾼다. 그냥 돌려주면 파괴된 에셋이
            // null이 아닌 참조로 나간다.
            return Target == null ? null : (IMotionGraphView)Target;
        }
    }
}
```

- [ ] **Step 3: 신호 통로를 만든다**

`Runtime/Unity/Runtime/MotionSignals.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프가 바깥세상에 무언가를 알리는 유일한 통로.
    ///
    /// <b>왜 사운드 노드와 햅틱 노드를 만들지 않는가</b> — 프로젝트마다 오디오 시스템이
    /// 다르므로 코어에 넣으면 "어떤 프로젝트에서든 단독으로 쓸 수 있다"가 깨진다.
    /// 그래프는 <c>"sfx:click"</c> 같은 문자열만 던지고, 그것을 받아 실제로 소리를 내거나
    /// 진동시키는 쪽은 프로젝트가 구현한다.
    ///
    /// 프로젝트 부트스트랩에서 구독하고, <b>씬 전환 때 구독을 해제한다</b> —
    /// static 이벤트라 구독이 남으면 파괴된 오브젝트를 붙잡는다.
    /// </summary>
    public static class MotionSignals
    {
        /// <summary>신호가 발생했다. 두 번째 인자는 신호를 낸 플레이어의 오브젝트다(없으면 null).</summary>
        public static event Action<string, GameObject> Received;

        public static void Emit(string signal, GameObject source)
        {
            if (string.IsNullOrEmpty(signal))
            {
                return;
            }

            Action<string, GameObject> handler = Received;
            if (handler != null)
            {
                handler(signal, source);
            }
        }

        /// <summary>구독을 전부 끊는다. 테스트와 씬 전환이 쓴다.</summary>
        public static void Clear()
        {
            Received = null;
        }
    }
}
```

- [ ] **Step 4: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime/Unity
git commit -m "feat: 효과 노드 베이스와 서브그래프 노드, 신호 통로 추가"
```

---

### Task 11: 효과 노드 — Move · Scale · Rotate

**Files:**
- Create: `Runtime/Unity/Nodes/MoveNode.cs`, `ScaleNode.cs`, `RotateNode.cs`

세 노드가 같은 뼈대를 공유한다: 슬롯을 잡고, 시작값을 캡처하고, 복구를 등록하고, 트윈을 돌린다.

**모든 효과 노드가 지키는 두 가지**

1. 콜백 안에서 대상이 살아 있는지 매번 확인한다. 트윈이 도는 도중에 오브젝트가 파괴될 수 있다.
2. `LerpUnclamped`를 쓴다. `OutBack`·`OutElastic`은 이징값이 1을 넘거나 0 아래로 내려간다 — `Lerp`로 클램프하면 그 오버슈트가 사라져 연출이 밋밋해진다.

- [ ] **Step 1: MoveNode를 만든다**

`Runtime/Unity/Nodes/MoveNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Move",
        Category = "Transform",
        Summary = "대상을 지정한 위치로 옮긴다. 팝업이 아래에서 올라오거나 패널이 옆으로 미끄러지는 연출에 쓴다.",
        Sample = "Move")]
    [Serializable]
    public sealed class MoveNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표", Tooltip = "상대 이동이면 시작 위치로부터의 오프셋이다.")]
        public Vector2 To;

        [MotionParam(Label = "상대 이동", Tooltip = "켜면 지금 위치를 기준으로 더한다. 프리팹마다 위치가 달라도 같은 그래프를 쓸 수 있다.")]
        public bool Relative = true;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.25f;

        public EaseKind Ease = EaseKind.OutCubic;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector2 from = target.anchoredPosition;
            Vector2 to = Relative ? from + To : To;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = from;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target != null)
                {
                    target.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                }
            });
        }
    }
}
```

- [ ] **Step 2: ScaleNode를 만든다**

`Runtime/Unity/Nodes/ScaleNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Scale",
        Category = "Transform",
        Summary = "대상의 크기를 바꾼다. 팝업이 작게 나타나 커지는 등장 연출에 쓴다.",
        Sample = "Scale")]
    [Serializable]
    public sealed class ScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 배율", Tooltip = "상대 배율이면 지금 크기에 성분별로 곱한다.")]
        public Vector3 To = Vector3.one;

        [MotionParam(Label = "상대 배율")]
        public bool Relative;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.25f;

        public EaseKind Ease = EaseKind.OutBack;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 from = target.localScale;
            Vector3 to = Relative
                ? new Vector3(from.x * To.x, from.y * To.y, from.z * To.z)
                : To;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = from;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target != null)
                {
                    target.localScale = Vector3.LerpUnclamped(from, to, e);
                }
            });
        }
    }
}
```

- [ ] **Step 3: RotateNode를 만든다**

`Runtime/Unity/Nodes/RotateNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Rotate",
        Category = "Transform",
        Summary = "대상을 회전시킨다. 로딩 스피너나 획득 아이콘의 한 바퀴 돌기에 쓴다.",
        Sample = "Rotate")]
    [Serializable]
    public sealed class RotateNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 각도", Tooltip = "상대 회전이면 지금 각도에 더한다. 360을 넘겨 여러 바퀴를 돌릴 수 있다.")]
        public Vector3 ToEuler;

        [MotionParam(Label = "상대 회전")]
        public bool Relative = true;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.4f;

        public EaseKind Ease = EaseKind.Linear;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            // 쿼터니언이 아니라 오일러각을 보간한다. 쿼터니언은 최단 경로로 돌기 때문에
            // "두 바퀴 돌기"(720도) 같은 지시를 표현할 수 없다.
            Vector3 fromEuler = target.localEulerAngles;
            Vector3 toEuler = Relative ? fromEuler + ToEuler : ToEuler;
            Quaternion fromRotation = target.localRotation;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localRotation = fromRotation;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target != null)
                {
                    target.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(fromEuler, toEuler, e));
                }
            });
        }
    }
}
```

- [ ] **Step 4: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime/Unity
git commit -m "feat: Move, Scale, Rotate 효과 노드 추가"
```

---

### Task 12: 효과 노드 — Fade · Color

**Files:**
- Create: `Runtime/Unity/Nodes/FadeNode.cs`, `ColorNode.cs`

- [ ] **Step 1: FadeNode를 만든다**

`Runtime/Unity/Nodes/FadeNode.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Fade",
        Category = "Graphics",
        Summary = "대상을 서서히 나타내거나 사라지게 한다. CanvasGroup을 우선 쓰고, 없으면 Graphic의 알파를 쓴다.",
        Sample = "Fade")]
    [Serializable]
    public sealed class FadeNode : UnityEffectNode
    {
        /// <summary>
        /// 요구 타입이 <c>Component</c>인 이유 — CanvasGroup과 Graphic 둘 다 받는다.
        /// 화면 전체를 페이드할 때는 CanvasGroup이, 아이콘 하나면 Image가 자연스럽다.
        /// </summary>
        [MotionSlot(typeof(Component))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 알파", Min = 0f, Max = 1f)]
        public float To = 1f;

        [MotionParam(Label = "시작 알파에서", Tooltip = "켜면 From에서 시작한다. 끄면 지금 알파에서 시작한다.")]
        public bool UseFrom;

        [MotionParam(Label = "시작 알파", Min = 0f, Max = 1f)]
        public float From;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.2f;

        public EaseKind Ease = EaseKind.OutQuad;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            CanvasGroup group = Resolve<CanvasGroup>(ctx, Target);
            if (group != null)
            {
                return FadeGroup(ctx, group);
            }

            Graphic graphic = Resolve<Graphic>(ctx, Target);
            if (graphic != null)
            {
                return FadeGraphic(ctx, graphic);
            }

            return MotionHandle.Skipped;
        }

        private IMotionHandle FadeGroup(IMotionContext ctx, CanvasGroup group)
        {
            float original = group.alpha;
            float from = UseFrom ? From : original;
            float to = To;

            Remember(ctx, delegate
            {
                if (group != null)
                {
                    group.alpha = original;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (group != null)
                {
                    group.alpha = Mathf.Clamp01(Mathf.LerpUnclamped(from, to, e));
                }
            });
        }

        private IMotionHandle FadeGraphic(IMotionContext ctx, Graphic graphic)
        {
            Color originalColor = graphic.color;
            float from = UseFrom ? From : originalColor.a;
            float to = To;

            Remember(ctx, delegate
            {
                if (graphic != null)
                {
                    graphic.color = originalColor;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (graphic == null)
                {
                    return;
                }

                Color next = graphic.color;
                next.a = Mathf.Clamp01(Mathf.LerpUnclamped(from, to, e));
                graphic.color = next;
            });
        }
    }
}
```

알파를 `Clamp01`하는 이유: `OutBack`이 1을 넘기면 알파가 1.2가 되는데, 그 자체는 무해하지만 되돌아올 때 눈에 띄는 계단이 생긴다. 위치와 달리 알파는 오버슈트가 보이지 않으므로 클램프가 손해가 없다.

- [ ] **Step 2: ColorNode를 만든다**

`Runtime/Unity/Nodes/ColorNode.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Color",
        Category = "Graphics",
        Summary = "대상의 색을 바꾼다. 버튼이 눌렸을 때 어두워지거나 경고로 붉어지는 연출에 쓴다.",
        Sample = "Color")]
    [Serializable]
    public sealed class ColorNode : UnityEffectNode
    {
        [MotionSlot(typeof(Graphic))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "목표 색")]
        public Color To = Color.white;

        [MotionParam(Label = "알파 유지", Tooltip = "켜면 색만 바꾸고 투명도는 건드리지 않는다. Fade와 겹쳐 쓸 때 켠다.")]
        public bool KeepAlpha = true;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.15f;

        public EaseKind Ease = EaseKind.OutQuad;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Graphic graphic = Resolve<Graphic>(ctx, Target);
            if (graphic == null)
            {
                return MotionHandle.Skipped;
            }

            Color from = graphic.color;
            Color to = To;
            if (KeepAlpha)
            {
                to.a = from.a;
            }

            Remember(ctx, delegate
            {
                if (graphic != null)
                {
                    graphic.color = from;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (graphic != null)
                {
                    graphic.color = Color.LerpUnclamped(from, to, e);
                }
            });
        }
    }
}
```

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime/Unity
git commit -m "feat: Fade와 Color 효과 노드 추가"
```

---

### Task 13: 효과 노드 — PunchScale · Shake

두 노드 모두 **제자리에서 끝난다.** 왕복 연출이라 자연 완료 시에는 되돌릴 것이 없다. 그래도 `Remember`를 등록하는 이유는 중간에 취소됐을 때 대상이 부풀거나 흔들린 상태로 굳는 것을 막기 위해서다.

**Files:**
- Create: `Runtime/Unity/Nodes/PunchScaleNode.cs`, `ShakeNode.cs`

- [ ] **Step 1: PunchScaleNode를 만든다**

`Runtime/Unity/Nodes/PunchScaleNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Punch Scale",
        Category = "Transform",
        Summary = "대상을 잠깐 부풀렸다 되돌린다. 획득, 강조, 버튼 눌림 순간에 쓴다.",
        Sample = "PunchScale")]
    [Serializable]
    public sealed class PunchScaleNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "세기", Tooltip = "0.2면 최대 20퍼센트까지 부푼다. 음수면 쪼그라든다.", Min = -1f, Max = 1f)]
        public float Amplitude = 0.2f;

        [MotionParam(Label = "시간", Min = 0f, Max = 2f)]
        public float Duration = 0.2f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            Transform target = Resolve<Transform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector3 from = target.localScale;
            float amplitude = Amplitude;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.localScale = from;
                }
            });

            // 이징을 받지 않는 이유 — 펀치는 자기 곡선을 갖는다. sin(pi * t)는 t=0과 t=1에서
            // 정확히 0이고 t=0.5에서 정확히 1이다. 그래서 정확히 제자리에서 끝난다.
            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                float wave = Mathf.Sin(t * Mathf.PI);
                target.localScale = from * (1f + amplitude * wave);
            });
        }
    }
}
```

- [ ] **Step 2: ShakeNode를 만든다**

`Runtime/Unity/Nodes/ShakeNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Shake",
        Category = "Transform",
        Summary = "대상을 흔든다. 조건 미달로 버튼을 눌렀을 때의 거부 반응이나 피격 연출에 쓴다.",
        Sample = "Shake")]
    [Serializable]
    public sealed class ShakeNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "세기", Tooltip = "최대 흔들림 폭(픽셀).", Min = 0f, Max = 200f)]
        public float Strength = 12f;

        [MotionParam(Label = "시간", Min = 0f, Max = 3f)]
        public float Duration = 0.3f;

        [MotionParam(Label = "빈도", Tooltip = "초당 방향이 바뀌는 횟수.", Min = 1f, Max = 60f)]
        public float Frequency = 20f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector2 origin = target.anchoredPosition;
            float strength = Strength;

            // 매 프레임 난수를 뽑으면 흔들림이 프레임률에 따라 달라진다. 시작할 때
            // 경로를 뽑아 두고 그 사이를 보간하면 60fps든 30fps든 같은 흔들림이 된다.
            int steps = Mathf.Max(2, Mathf.CeilToInt(Duration * Mathf.Max(1f, Frequency)));
            var path = new Vector2[steps + 1];
            path[0] = Vector2.zero;
            for (int i = 1; i < steps; i++)
            {
                path[i] = UnityEngine.Random.insideUnitCircle;
            }

            // 마지막 점을 0으로 박아 정확히 제자리에서 끝나게 한다.
            path[steps] = Vector2.zero;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = origin;
                }
            });

            return Run(ctx, Duration, EaseKind.Linear, delegate(float t)
            {
                if (target == null)
                {
                    return;
                }

                float scaled = t * steps;
                int index = (int)scaled;
                if (index >= steps)
                {
                    target.anchoredPosition = origin;
                    return;
                }

                Vector2 offset = Vector2.Lerp(path[index], path[index + 1], scaled - index);

                // 뒤로 갈수록 잦아든다.
                target.anchoredPosition = origin + offset * (strength * (1f - t));
            });
        }
    }
}
```

- [ ] **Step 3: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime/Unity
git commit -m "feat: Punch Scale과 Shake 효과 노드 추가"
```

---

### Task 14: 무한 핸들과 유지 연출 — Float · Bounce

부유와 통통 튀기는 **끝나지 않는** 연출이다. `Loop` 트리거에 물려 두고 `End`가 발사되면 취소되면서 제자리로 돌아온다. IdlePaori의 `UIFloatingModule`이 `_origin` 캡처와 `OnStop` 복원으로 손수 하던 일을 프레임워크가 보장하는 자리다.

끝나지 않는 핸들이 코어에 없으므로 먼저 만든다. 순수 코드이므로 `dotnet test`로 검증한다.

**Files:**
- Modify: `Runtime/Core/Exec/MotionHandle.cs`
- Create: `Runtime/Unity/Nodes/FloatNode.cs`, `BounceNode.cs`
- Test: `Tests~/dotnet/MotionHandleTests.cs` (기존 파일에 추가)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests~/dotnet/MotionHandleTests.cs`의 클래스 안에 추가한다:

```csharp
        [Test]
        public void Forever_NeverCompletesOnItsOwn()
        {
            IMotionHandle handle = MotionHandle.Forever(null);

            for (int i = 0; i < 1000; i++)
            {
                handle.Tick(1f);
            }

            Assert.That(handle.IsDone, Is.False, "유지 연출은 취소될 때까지 끝나지 않는다");
        }

        [Test]
        public void Forever_AccumulatesElapsed()
        {
            float last = -1f;
            IMotionHandle handle = MotionHandle.Forever(delegate(float elapsed) { last = elapsed; });

            handle.Tick(0.5f);
            Assert.That(last, Is.EqualTo(0.5f).Within(1e-5f));

            handle.Tick(0.25f);
            Assert.That(last, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Forever_FirstTickReportsElapsedNotZero()
        {
            // 첫 틱에서 0을 보내면 사인파가 한 프레임 멈춘 것처럼 보인다.
            float first = -1f;
            IMotionHandle handle = MotionHandle.Forever(delegate(float elapsed) { first = elapsed; });

            handle.Tick(0.1f);

            Assert.That(first, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void Forever_CancelStopsCallbacks()
        {
            int calls = 0;
            IMotionHandle handle = MotionHandle.Forever(delegate { calls++; });

            handle.Tick(1f);
            handle.Cancel();
            handle.Tick(1f);
            handle.Tick(1f);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Forever_NullCallback_IsSafe()
        {
            IMotionHandle handle = MotionHandle.Forever(null);

            Assert.DoesNotThrow(delegate { handle.Tick(1f); });
            Assert.DoesNotThrow(handle.Cancel);
        }
```

- [ ] **Step 2: 실패를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

기대: 컴파일 실패 — `MotionHandle.Forever`가 없음.

- [ ] **Step 3: Forever를 구현한다**

`Runtime/Core/Exec/MotionHandle.cs`의 `FromTimer` 아래에 추가한다:

```csharp
        /// <summary>
        /// <b>취소될 때까지 끝나지 않는</b> 핸들. 콜백은 누적 경과 시간(초)을 받는다.
        ///
        /// 부유나 반복 회전처럼 "켜 두면 계속 도는" 유지 연출에 쓴다. 이런 노드는
        /// <c>Loop</c> 트리거에 물려 두고 <c>End</c>가 취소하게 한다.
        ///
        /// <b>주의</b> — 이 핸들을 쓰는 노드는 자식으로 이어지는 흐름을 막는다.
        /// 뒤에 무언가를 이어 붙이고 싶으면 <see cref="FromTimer"/>를 쓴다.
        /// </summary>
        public static IMotionHandle Forever(Action<float> onElapsed)
        {
            return new ForeverHandle(onElapsed);
        }
```

그리고 `TimerHandle` 클래스 아래에 추가한다:

```csharp
        private sealed class ForeverHandle : IMotionHandle
        {
            private readonly Action<float> _onElapsed;
            private float _elapsed;
            private bool _cancelled;

            public ForeverHandle(Action<float> onElapsed)
            {
                _onElapsed = onElapsed;
            }

            public bool IsDone => _cancelled;

            public void Tick(float deltaSeconds)
            {
                if (_cancelled)
                {
                    return;
                }

                if (deltaSeconds > 0f)
                {
                    _elapsed += deltaSeconds;
                }

                if (_onElapsed != null)
                {
                    _onElapsed(_elapsed);
                }
            }

            public void Cancel()
            {
                _cancelled = true;
            }
        }
```

- [ ] **Step 4: 통과를 확인한다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
```

기대: `Failed: 0`, `purity exit=1`.

- [ ] **Step 5: FloatNode를 만든다**

`Runtime/Unity/Nodes/FloatNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Float",
        Category = "Transform",
        Summary = "대상을 천천히 위아래로 부유시킨다. 끝나지 않으므로 Loop 트리거에 문다. 취소되면 제자리로 돌아온다.",
        Sample = "Float")]
    [Serializable]
    public sealed class FloatNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "진폭", Tooltip = "각 축의 최대 이동 폭(픽셀).")]
        public Vector2 Amplitude = new Vector2(0f, 8f);

        [MotionParam(Label = "주기", Tooltip = "한 번 왕복하는 데 걸리는 시간(초).", Min = 0.1f, Max = 10f)]
        public float Period = 2f;

        [MotionParam(Label = "위상", Tooltip = "0에서 1. 여러 오브젝트를 어긋나게 띄울 때 쓴다.", Min = 0f, Max = 1f)]
        public float Phase;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            // 원점을 시작할 때 한 번만 잡는다. 이것을 매번 다시 잡으면 사인파의 중간 위치가
            // 다음 기준이 되어 오브젝트가 갈수록 밀린다. 방치형에서 몇 시간 뒤에 드러나는
            // 종류의 버그다.
            Vector2 origin = target.anchoredPosition;
            Vector2 amplitude = Amplitude;
            float period = Mathf.Max(0.01f, Period);
            float phase = Phase;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = origin;
                }
            });

            return MotionHandle.Forever(delegate(float elapsed)
            {
                if (target == null)
                {
                    return;
                }

                float wave = Mathf.Sin((elapsed / period + phase) * 2f * Mathf.PI);
                target.anchoredPosition = origin + amplitude * wave;
            });
        }
    }
}
```

- [ ] **Step 6: BounceNode를 만든다**

`Runtime/Unity/Nodes/BounceNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Bounce",
        Category = "Transform",
        Summary = "대상을 통통 튀게 한다. 끝나지 않으므로 Loop 트리거에 문다. 눌러 달라고 조르는 버튼에 쓴다.",
        Sample = "Bounce")]
    [Serializable]
    public sealed class BounceNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "높이", Tooltip = "튀어 오르는 높이(픽셀).", Min = 0f, Max = 200f)]
        public float Height = 16f;

        [MotionParam(Label = "주기", Tooltip = "한 번 튀는 데 걸리는 시간(초).", Min = 0.1f, Max = 10f)]
        public float Period = 0.8f;

        [MotionParam(Label = "쉬는 시간", Tooltip = "튄 다음 가만히 있는 시간(초).", Min = 0f, Max = 10f)]
        public float RestTime = 0.4f;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            Vector2 origin = target.anchoredPosition;
            float height = Height;
            float period = Mathf.Max(0.01f, Period);
            float cycle = period + Mathf.Max(0f, RestTime);

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.anchoredPosition = origin;
                }
            });

            return MotionHandle.Forever(delegate(float elapsed)
            {
                if (target == null)
                {
                    return;
                }

                float inCycle = elapsed % cycle;

                if (inCycle >= period)
                {
                    // 쉬는 구간. 정확히 원점에 놓는다.
                    target.anchoredPosition = origin;
                    return;
                }

                // 반원 궤적이라 올라갈 때 빠르고 꼭대기에서 느려진다 — 중력에 가까운 느낌이다.
                float t = inCycle / period;
                float offset = height * Mathf.Sin(t * Mathf.PI);
                target.anchoredPosition = new Vector2(origin.x, origin.y + offset);
            });
        }
    }
}
```

- [ ] **Step 7: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime Tests~
git commit -m "feat: 무한 핸들과 Float, Bounce 유지 연출 노드 추가"
```

---

### Task 15: 효과 노드 — FlyAcross

**Files:**
- Create: `Runtime/Unity/Nodes/FlyAcrossNode.cs`

- [ ] **Step 1: FlyAcrossNode를 만든다**

`Runtime/Unity/Nodes/FlyAcrossNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Fly Across",
        Category = "Transform",
        Summary = "대상을 한 지점에서 다른 지점으로 곡선을 그리며 날린다. 획득한 재화가 상단 카운터로 빨려 들어가는 연출에 쓴다.",
        Sample = "FlyAcross")]
    [Serializable]
    public sealed class FlyAcrossNode : UnityEffectNode
    {
        [MotionSlot(typeof(RectTransform))]
        public SlotRef Target = SlotRef.Self;

        [MotionSlot(typeof(RectTransform))]
        [MotionParam(Label = "출발", Tooltip = "비워 두면 대상의 지금 위치에서 출발한다.")]
        public SlotRef From;

        [MotionSlot(typeof(RectTransform))]
        [MotionParam(Label = "도착")]
        public SlotRef To;

        [MotionParam(Label = "호 높이", Tooltip = "직선에서 얼마나 부풀릴지(월드 단위). 0이면 직선이다.")]
        public float Arc = 80f;

        [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
        public float Duration = 0.5f;

        public EaseKind Ease = EaseKind.InOutCubic;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            RectTransform target = Resolve<RectTransform>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            RectTransform destination = Resolve<RectTransform>(ctx, To);
            if (destination == null)
            {
                // 도착지가 없으면 날 곳이 없다. 슬롯 해석이 이미 경고를 한 번 남겼다.
                return MotionHandle.Skipped;
            }

            // 캔버스가 서로 다를 수 있으므로 월드 좌표로 계산한다. anchoredPosition은
            // 부모가 다르면 비교할 수 없다.
            RectTransform origin = From.IsValid ? Resolve<RectTransform>(ctx, From) : null;

            Vector3 startPosition = target.position;
            Vector3 fromPosition = origin != null ? origin.position : startPosition;
            Vector3 toPosition = destination.position;
            float arc = Arc;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.position = startPosition;
                }
            });

            return Run(ctx, Duration, Ease, delegate(float e)
            {
                if (target == null)
                {
                    return;
                }

                Vector3 straight = Vector3.LerpUnclamped(fromPosition, toPosition, e);

                // sin(pi * e)는 양 끝에서 정확히 0이라 출발점과 도착점을 어긋나게 하지 않는다.
                straight.y += arc * Mathf.Sin(Mathf.Clamp01(e) * Mathf.PI);

                target.position = straight;
            });
        }
    }
}
```

`Arc`에 들어가는 `e`만 `Clamp01`하는 이유: 이징이 1을 넘겼을 때 `Sin`이 음수가 되어 도착 순간 아래로 튀는 것을 막는다. 직선 성분은 오버슈트를 그대로 살린다.

- [ ] **Step 2: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime/Unity
git commit -m "feat: Fly Across 효과 노드 추가"
```

---

### Task 16: 효과 노드 — SetActive · SendSignal · PlayAnimator

**Files:**
- Create: `Runtime/Unity/Nodes/SetActiveNode.cs`, `SendSignalNode.cs`, `PlayAnimatorNode.cs`

- [ ] **Step 1: SetActiveNode를 만든다**

`Runtime/Unity/Nodes/SetActiveNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Set Active",
        Category = "Object",
        Summary = "대상을 켜거나 끈다. 즉시 끝난다. 연출 중간에 파티클이나 이펙트 오브젝트를 켤 때 쓴다.",
        Sample = "SetActive")]
    [Serializable]
    public sealed class SetActiveNode : UnityEffectNode
    {
        [MotionSlot(typeof(GameObject))]
        public SlotRef Target = SlotRef.Self;

        [MotionParam(Label = "켜기")]
        public bool Active = true;

        public override bool Reverts => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            GameObject target = Resolve<GameObject>(ctx, Target);
            if (target == null)
            {
                return MotionHandle.Skipped;
            }

            bool wasActive = target.activeSelf;

            Remember(ctx, delegate
            {
                if (target != null)
                {
                    target.SetActive(wasActive);
                }
            });

            target.SetActive(Active);
            return MotionHandle.Completed;
        }
    }
}
```

- [ ] **Step 2: SendSignalNode를 만든다**

`Runtime/Unity/Nodes/SendSignalNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Send Signal",
        Category = "Signal",
        Summary = "문자열 신호를 밖으로 던진다. 소리와 진동은 프로젝트가 이 신호를 받아 처리한다. 즉시 끝난다.",
        Sample = "SendSignal")]
    [Serializable]
    public sealed class SendSignalNode : UnityEffectNode
    {
        [MotionParam(Label = "신호", Tooltip = "예: sfx:click, haptic:light. 규약은 프로젝트가 정한다.")]
        public string Signal = "";

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (string.IsNullOrEmpty(Signal))
            {
                return MotionHandle.Skipped;
            }

            // 신호를 낸 오브젝트를 함께 넘긴다. 받는 쪽이 "어느 버튼이 눌렸나"를 알아야
            // 위치 기반 사운드나 화면 흔들림을 붙일 수 있다.
            var player = ctx == null ? null : ctx.Host as MotionPlayer;
            GameObject source = player == null ? null : player.gameObject;

            MotionSignals.Emit(Signal, source);
            return MotionHandle.Completed;
        }
    }
}
```

- [ ] **Step 3: PlayAnimatorNode를 만든다**

`Runtime/Unity/Nodes/PlayAnimatorNode.cs`:

```csharp
using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Play Animator",
        Category = "Object",
        Summary = "Animator의 상태를 재생한다. 그래프로 표현하기 어려운 손으로 만든 애니메이션을 연출 중간에 끼울 때 쓴다.",
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

            animator.Play(State, Layer, 0f);

            if (!WaitForCompletion)
            {
                return MotionHandle.Completed;
            }

            return new AnimatorWaitHandle(animator, Layer, Mathf.Max(0.1f, Timeout));
        }

        /// <summary>
        /// 상태가 한 바퀴 돌 때까지 기다린다.
        ///
        /// <b>왜 타임아웃이 있는가</b> — 상태 이름이 틀리면 <c>Animator.Play</c>는 조용히
        /// 아무것도 하지 않는다. 그러면 <c>normalizedTime</c>이 영원히 1에 닿지 않아
        /// <c>End</c> 연출을 기다리는 팝업이 화면에 박제된다.
        /// </summary>
        private sealed class AnimatorWaitHandle : IMotionHandle
        {
            private readonly Animator _animator;
            private readonly int _layer;
            private readonly float _timeout;
            private float _waited;
            private bool _done;

            public AnimatorWaitHandle(Animator animator, int layer, float timeout)
            {
                _animator = animator;
                _layer = layer;
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
```

- [ ] **Step 4: 게이트를 돌리고 커밋한다**

```bash
./Tools~/compile-check/run.sh
git add Runtime/Unity
git commit -m "feat: Set Active, Send Signal, Play Animator 노드 추가"
```

---

### Task 17: 마무리 — 문서와 CI

**Files:**
- Modify: `README.md`, `CHANGELOG.md`, `.github/workflows/ci.yml`
- Modify: `docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`

- [ ] **Step 1: CI에 컴파일 게이트의 존재를 기록한다**

Unity DLL은 재배포할 수 없어 CI에서 컴파일할 수 없다. 그 사실을 워크플로에 명시해 나중에 "왜 Unity 계층은 CI가 없지"를 다시 묻지 않게 한다.

`.github/workflows/ci.yml`의 `Run core tests` 스텝 뒤에 추가한다:

```yaml
      - name: Note Unity layer verification
        run: |
          # Runtime/Unity는 UnityEngine 어셈블리가 필요한데 그것은 재배포할 수 없다.
          # 그래서 컴파일 검증은 로컬 게이트다: ./Tools~/compile-check/run.sh
          # Unity 파일을 건드리는 커밋은 그것을 통과해야 한다.
          if [ ! -x Tools~/compile-check/run.sh ]; then
            echo "Tools~/compile-check/run.sh가 없거나 실행 권한이 없습니다"; exit 1
          fi
          echo "Unity 계층 컴파일 게이트가 제자리에 있습니다 (로컬 실행)"
```

- [ ] **Step 2: 컴파일 게이트가 모든 Unity 파일을 실제로 훑는지 확인한다**

`Runtime/Unity`의 파일 수와 컴파일 대상 수가 맞는지 본다:

```bash
find Runtime/Unity -name '*.cs' | wc -l
./Tools~/compile-check/run.sh
```

기대: `Runtime/Unity`에 21개 안팎의 `.cs`가 있고 컴파일이 통과한다.

- [ ] **Step 3: 스펙의 열린 질문을 닫는다**

`docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`의 11절을 다음으로 바꾼다:

```markdown
## 11. 열린 질문

1. ~~그래프 직렬화 형식~~ — **정해짐(계획 2).** `[SerializeReference] List<MotionNodeBase>` + 평면 간선 목록 `List<NodeLink>`. 타입이 사라진 노드는 배열의 null 원소로 남고 실행 시 건너뛰어진다. 평면 간선 목록을 고른 이유는 YAML diff가 줄 단위로 움직여 머지 충돌이 줄기 때문이다.
2. ~~내장 트윈 러너의 구동 방식~~ — **정해짐(계획 2).** 단일 `MotionPump`. 숨겨진 MonoBehaviour 하나가 모든 플레이어를 한 `Update`에서 돌린다. 플레이어마다 `Update`를 두면 수백 개가 동시에 살아 있는 방치형에서 네이티브-매니지드 경계 비용이 그 수만큼 곱해진다.
3. 에디터 프리뷰가 프리팹 스테이지와 씬 인스턴스 중 무엇을 대상으로 할지, 그리고 프리뷰가 만든 변경이 프리팹에 새어 나가지 않게 하는 방법 — 계획 3에서 정한다.
```

3.1절의 어셈블리 목록 아래에 다음 문단을 추가한다:

```markdown
**코어에 뚫은 구멍 하나 — `IMotionContext.Host`.** 효과 노드가 트윈 백엔드에 닿아야 하는데
코어는 백엔드를 모른다. 문맥이 `object Host`를 나르고 Unity 계층이 거기에 `MotionPlayer`를
넣는다. `ISlotResolver`가 `object`를 돌려주는 것과 똑같은 관용구다 — 코어가 Unity 타입을
알지 않기 위해 타입을 잃는 지점을 하나로 모은다.
```

10.2절을 다음으로 바꾼다:

```markdown
### 10.2 Unity 계층 — dotnet 컴파일 게이트 + 에디터 스모크

`juahn.v2.UiMotion`은 UnityEngine을 참조하므로 `dotnet test`로 돌릴 수 없다. 대신
**컴파일은 Unity 에디터 없이 검증한다** — `Tools~/compile-check/run.sh`가 설치된 Unity의
매니지드 DLL을 참조해 `Runtime` 전체를 `dotnet build`로 컴파일한다. 1초 안에 끝나므로
에디터를 열기 전에 오타와 타입 오류가 전부 잡힌다.

Unity의 DLL은 재배포할 수 없어 이 게이트는 CI가 아니라 로컬이다. CI는 `package.json`
유효성, asmdef 존재, 코어 순수성, `dotnet test`를 지킨다.

동작 검증은 각 노드의 `Sample` 그래프가 맡는다. Node Doctor가 전부 재생해보는 것으로
회귀를 얕게 잡는다.

Unity Test Framework 기반 EditMode/PlayMode 테스트는 범위 밖이다. 시간 기반이라
불안정해지기 쉽고, 로직의 핵심은 이미 10.1이 덮는다.
```

- [ ] **Step 4: README를 갱신한다**

`README.md`에 "Unity 계층" 절을 추가한다. 최소한 다음을 담는다:

- `MotionGraph` 에셋을 만들고 `MotionPlayer`를 프리팹에 붙이는 흐름
- `Fire` / `Stop` / `WaitFor` / `ClaimTriggerOwnership`의 용도
- 슬롯 목록이 파생값이라는 것과 `SyncBindings`
- 노드를 새로 만드는 법 (`UnityEffectNode` 상속 + `[MotionNode]` + `[MotionSlot]`)
- 검증 명령 두 줄: `dotnet test ...`와 `./Tools~/compile-check/run.sh`

- [ ] **Step 5: CHANGELOG를 갱신한다**

`## [Unreleased]` 아래에 추가한다:

```markdown
### Added
- Unity 런타임 계층 — `MotionGraph` 에셋, `MotionPlayer` 컴포넌트, 전역 틱 펌프
- 슬롯 바인딩과 타입 해석 (`SlotTable` · `MotionSlots`)
- 교체 가능한 트윈 백엔드 (`IMotionTweenRunner` · `BuiltinTweenRunner`)
- 효과 노드 13종 — Move · Scale · Rotate · Fade · Color · Punch Scale · Shake ·
  Float · Bounce · Fly Across · Set Active · Send Signal · Play Animator
- 순수 코어에 `MotionGraphIndex` · `SlotIntrospector` · `NodeLink` · `MotionHandle.Forever`
- Unity 계층 컴파일 게이트 (`Tools~/compile-check`)

### Changed
- `IMotionContext`에 `Host` 추가 — 효과 노드가 트윈 백엔드에 닿는 통로
```

- [ ] **Step 6: 모든 게이트를 마지막으로 돌린다**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh
git status --short
```

기대: 테스트 전부 통과, `purity exit=1`, 컴파일 통과.

- [ ] **Step 7: 커밋**

```bash
git add -A
git commit -m "docs: Unity 계층 문서화와 스펙의 열린 질문 정리"
```

---

## 남은 것 (이 계획의 범위 밖)

- **계획 3 — 에디터 툴.** 그래프 창, 노드 팔레트, `MotionPlayer` 인스펙터(슬롯 자동 바인딩), Node Doctor, 인에디터 프리뷰, 프리셋 브라우저. **각 노드의 `Sample` 그래프 에셋을 여기서 생성한다** — 지금은 이름만 선언돼 있고 파일은 없다. Node Doctor가 그 부재를 잡아내는 것이 첫 번째 할 일이다.
- **계획 4 — 어댑터.** `com.juahn.v2.uimotion.uiservice` 브릿지와 `com.juahn.v2.uimotion.dotween` 백엔드.

---

## 측정된 성능 특성 (구현 후 실측)

순수 코어를 `dotnet` 콘솔에서 직접 재고, Unity 부분은 재지 않았다. 아래 숫자는 데스크톱 기준이다.

**트리거 1회 발사 (완주까지)**

| | 할당 |
|---|---|
| 고정 비용 (스코프 + 문맥 + 루트 NodeRun) | 336 B |
| 효과 노드 1개마다 | +272 B |
| 효과 3개짜리 팝업 연출 | 1,152 B |
| `MotionRuntime` 1개 (플레이어당 1회) | 784 B |
| `MotionGraphIndex` 1개 (그래프당 1회, 전 플레이어 공유) | 1,864 B |

**정상 상태 (600프레임 평균)**

| 방식 | 플레이어 50개 | 플레이어 300개 |
|---|---|---|
| 유지 연출 (`Float`·`Bounce`, `MotionHandle.Forever`) | 1.6 us · **0 B/프레임** | 12.0 us · **0 B/프레임** |
| 유한 트윈을 계속 재발사 | 3.4 us · 2.9 KB/프레임 | 23.3 us · **17.6 KB/프레임** |

### 여기서 나오는 저작 규칙 (계획 3이 강제해야 한다)

**같은 연출을 두 가지로 만들 수 있는데 비용이 수백 배 다르다.** "계속 통통 튀는 버튼"을
`Bounce` 노드로 만들면 프레임당 0 B이고, `Scale`을 `Repeat`으로 감싸면 300개 기준
초당 500 KB 안팎이 나간다. 방치형이라 이 차이가 몇 시간에 걸쳐 GC 압력으로 쌓인다.

계획 3의 그래프 창은 다음을 경고해야 한다:

- 짧은 주기(예: 1초 미만)로 도는 `Repeat`이나 `Loop` 트리거 아래에 유한 트윈만 있을 때 —
  유지 연출 노드로 바꿀 수 있는지 묻는다.
- `Forever` 핸들을 쓰는 노드(`Float`·`Bounce`)에 자식이 달렸을 때 — 그 자식은 영원히
  실행되지 않는다. 이건 성능이 아니라 조용한 오작동이다.

### 맥락 — 이 숫자들은 대개 병목이 아니다

`anchoredPosition`을 쓰면 캔버스가 dirty가 되고 리빌드는 보통 0.1~1 ms다. 플레이어 300개를
해석하는 12 us보다 10~80배 크다. 즉 병목은 "무엇으로 보간하는가"가 아니라 "몇 개를 얼마나
자주 dirty로 만드는가"다. 그래프 계층을 최적화하기 전에 그쪽을 먼저 본다.

정말 발사 할당이 문제가 되면 `MotionScope`·`NodeRun`·핸들을 풀링할 수 있다. 실행 상태를
전부 스코프가 소유하는 구조라 풀링을 얹기 좋은 형태다. 지금은 하지 않는다 — 측정된 문제가 없다.
