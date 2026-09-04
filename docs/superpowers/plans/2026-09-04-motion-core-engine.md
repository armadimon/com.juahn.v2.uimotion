# UI Motion Graph — 순수 코어 실행 엔진 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** UnityEngine 없이 동작하는 UI 모션 그래프 실행 엔진을 TDD로 만든다. `dotnet test`로 CI에서 전부 검증된다.

**Architecture:** 실행기는 `IMotionGraphView` 하나를 통해서만 그래프를 본다. 트리거를 발사하면 `MotionScope`가 하나 생기고, 그 스코프가 노드 실행 트리(`NodeRun`)·취소·원상 복구를 전부 소유한다. 시간은 바깥에서 `Tick(dt)`로 주입하므로 테스트가 결정적이다.

**Tech Stack:** C# (`netstandard2.1`), NUnit 3, `dotnet` CLI, GitHub Actions. Unity 6000.0 호환 어셈블리.

**스펙:** `docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`

**후속 계획 (이 계획의 범위 밖):**
- 계획 2 — Unity 런타임 계층 (`MotionGraph` SO · `MotionPlayer` · 슬롯 테이블 · 트윈 러너 · 효과 노드)
- 계획 3 — 에디터 툴 (그래프 창 · 팔레트 · 인스펙터 · 프리뷰 · Node Doctor)
- 계획 4 — 노드 세트 확장 + UiService 브릿지 + DOTween 백엔드

---

## 사전 지식

읽는 사람이 이 저장소를 처음 본다고 가정하고 적는다.

**리포지토리** — `com.juahn.v2.uimotion`은 그 자체로 하나의 git 저장소이자 하나의 Unity 패키지(UPM)다. 형제 저장소들(`com.juahn.v2.di`, `com.juahn.v2.timing` 등)이 같은 부모 폴더 `~/UnityProject/JuahnFrameworkV2/` 아래에 나란히 있다. 부모 폴더는 저장소가 아니다.

**패키지 관례** — 루트에 `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`, `.gitignore`, `.github/workflows/ci.yml`을 두고 코드는 `Runtime/` 아래에 둔다. `Runtime/`에는 `.asmdef`(어셈블리 정의)가 반드시 하나 이상 있어야 CI를 통과한다.

**`.meta` 파일** — Unity는 모든 파일마다 `.meta`를 만든다. 이 계획에서는 `.meta`를 직접 만들지 않는다. Unity 에디터가 패키지를 처음 열 때 생성하며, 그때 커밋한다. `dotnet` 테스트는 `.meta` 없이 돈다.

**어셈블리 두 개** — `juahn.v2.UiMotion.Core`(순수 C#, 이 계획의 전부)와 `juahn.v2.UiMotion`(UnityEngine 의존, 계획 2). 이 계획에서는 Core만 만들되, Unity 어셈블리의 자리(폴더와 asmdef)는 Task 1에서 미리 만들어 둔다.

**왜 순수 C#인가** — Unity 테스트는 GitHub Actions에서 Unity 라이선스가 필요해 못 돌린다. Core를 UnityEngine 없이 짜면 `dotnet test`로 무료로, 모든 푸시마다 돌릴 수 있다. 버그가 가장 많이 날 곳(취소·원상복구·스코프 생명주기·순환 검출)이 마침 전부 여기다.

**절대 하지 말 것** — Core 어셈블리 안에서 `using UnityEngine;`을 쓰는 것. `noEngineReferences: true`라 컴파일도 안 되지만, 우회하려고 시도하지 말 것. 대상을 실제로 만지는 코드는 전부 계획 2로 간다.

**직렬화되는 타입은 public 필드를 쓴다.** Unity는 `readonly` 필드를 직렬화하지 않고, private 필드는 `[SerializeField]`가 있어야 직렬화한다. 그런데 `[SerializeField]`는 UnityEngine 타입이라 이 어셈블리에서 쓸 수 없다. 남는 방법은 **public 필드**뿐이다.

그래서 그래프 에셋에 직렬화되는 값 타입(`NodeId`, `SlotRef`)은 `readonly struct`가 아니라 그냥 `struct`이고, 데이터를 public 필드로 노출한다. 캡슐화를 잃지만 대안이 없다. 이 타입들은 값으로 전달되는 작은 구조체라 실질적 위험은 낮다.

반대로 **직렬화되지 않는 런타임 전용 타입**(`MotionTimer` 등)은 `readonly`와 private 필드를 그대로 써도 된다. 어느 쪽인지 헷갈리면 "이 값이 `.asset` 파일에 저장되는가"로 판단한다.

**커밋 메시지** — `<타입>: <설명>` 형식. 타입은 `feat`, `fix`, `refactor`, `docs`, `test`, `chore` 중 하나. 본문은 한국어. 어트리뷰션 푸터는 붙이지 않는다.

**코드 스타일** — 주석과 문서에 이모지를 쓰지 않는다. 파일 하나는 200~400줄, 최대 800줄. LINQ를 쓰지 않는다(`foreach`와 명시적 루프를 쓴다).

---

## 파일 구조

이 계획이 만드는 파일 전부다. 각 파일은 책임 하나만 진다.

### 패키지 루트

| 파일 | 책임 |
|---|---|
| `package.json` | UPM 매니페스트 |
| `README.md` | 패키지 소개와 설치법 |
| `CHANGELOG.md` | 버전 이력 |
| `LICENSE.md` | MIT (형제 패키지에서 복사) |
| `.gitignore` | Unity/dotnet 산출물 제외 |
| `.github/workflows/ci.yml` | 패키지 검증 + `dotnet test` |

### `Runtime/Core/` — `juahn.v2.UiMotion.Core` (순수 C#)

| 파일 | 책임 |
|---|---|
| `juahn.v2.UiMotion.Core.asmdef` | 어셈블리 정의 (`noEngineReferences: true`) |
| `Graph/NodeId.cs` | 노드 식별자 값 타입 |
| `Graph/SlotRef.cs` | 슬롯 이름 참조 값 타입 |
| `Graph/SlotDeclaration.cs` | 그래프가 선언한 슬롯 하나 |
| `Graph/TriggerPolicy.cs` | 재발사 정책 열거형 |
| `Graph/TriggerDeclaration.cs` | 그래프가 선언한 트리거 하나 |
| `Graph/IMotionGraphView.cs` | 실행기가 보는 그래프의 유일한 창구 |
| `Easing/EaseKind.cs` | 이징 종류 열거형 |
| `Easing/EaseLibrary.cs` | 이징 함수 평가 |
| `Exec/MotionTimer.cs` | 경과/진행률 계산 |
| `Exec/IMotionHandle.cs` | 재생 중인 무언가의 계약 |
| `Exec/MotionHandle.cs` | 즉시 완료·건너뜀·타이머 핸들 팩토리 |
| `Exec/IMotionScope.cs` | 노드가 보는 스코프의 좁은 면 (원상 복구 등록) |
| `Exec/MotionScope.cs` | 트리거 1회 발사의 실행 단위 |
| `Exec/NodeRun.cs` | 노드 하나와 그 자식 전파 |
| `Exec/MotionContext.cs` | `IMotionContext`의 기본 구현 |
| `Exec/ITriggerSink.cs` | 노드가 다른 트리거를 건드리는 통로 |
| `Exec/TriggerRunner.cs` | 트리거 하나의 정책·대기·현재 스코프 |
| `Exec/MotionRuntime.cs` | 트리거 발사 창구, 위상 규약 |
| `Exec/GraphCycleDetector.cs` | 서브그래프 순환 검출 |
| `Authoring/IMotionContext.cs` | 노드가 받는 실행 문맥 |
| `Authoring/ISlotResolver.cs` | 슬롯 이름 → 대상 해석 (Unity 계층이 구현) |
| `Authoring/MotionNodeBase.cs` | 모든 노드의 베이스 |
| `Authoring/MotionFlowNode.cs` | 실행 순서를 정하는 노드의 베이스 |
| `Authoring/MotionEffectNode.cs` | 무언가를 움직이는 노드의 베이스 |
| `Authoring/MotionNodeAttribute.cs` | 이름·분류·설명·예시 선언 |
| `Authoring/MotionSlotAttribute.cs` | 슬롯 필드의 요구 타입 선언 |
| `Authoring/MotionParamAttribute.cs` | 파라미터 필드의 표시 정보 |
| `Nodes/TriggerNode.cs` | 진입 노드 |
| `Nodes/SequenceNode.cs` | 자식을 차례로 |
| `Nodes/ParallelNode.cs` | 자식을 동시에 |
| `Nodes/DelayNode.cs` | 기다린 뒤 자식으로 |
| `Nodes/RepeatNode.cs` | 자식을 반복 |
| `Nodes/StopTriggerNode.cs` | 다른 트리거를 멈춤 |
| `Nodes/SubGraphNode.cs` | 다른 그래프를 노드 하나처럼 |
| `Diag/IMotionLog.cs` | 로그 싱크 계약 |
| `Diag/OnceLogger.cs` | 같은 키의 경고를 한 번만 |

### `Runtime/Unity/` — `juahn.v2.UiMotion` (계획 2에서 채움)

| 파일 | 책임 |
|---|---|
| `juahn.v2.UiMotion.asmdef` | 어셈블리 정의 (Core 참조). Task 1에서 자리만 만든다 |

### `Tests~/dotnet/` — 테스트 (Unity가 무시하는 `~` 접미 폴더)

| 파일 | 책임 |
|---|---|
| `UiMotion.Core.Tests.csproj` | NUnit 테스트 프로젝트. Core 소스를 링크한다 |
| `SanityTests.cs` | 하네스 자체가 도는지 |
| `Fakes/FakeGraph.cs` | 테스트용 인메모리 `IMotionGraphView` |
| `Fakes/FakeNodes.cs` | 실행 순서를 기록하는 노드와 `ExecutionLog` |
| `Fakes/FakeContext.cs` | 테스트용 `ISlotResolver`·`IMotionLog` |
| `Fakes/FakeGraphTests.cs` | 테스트 도구 자체의 검증 |
| `NodeIdTests.cs` | 식별자 유효성·동등성 |
| `EaseLibraryTests.cs` | 이징 경계값 |
| `MotionTimerTests.cs` | 경과·진행률·오버슈트 |
| `MotionHandleTests.cs` | 완료·건너뜀·타이머 핸들 |
| `SlotRefTests.cs` | 슬롯 이름 값 타입 |
| `MotionNodeBaseTests.cs` | 노드 베이스 계약 |
| `MotionScopeTests.cs` | 취소·원상 복구 역순 |
| `NodeRunTests.cs` | 자식 전파 |
| `MotionScopeExecutionTests.cs` | 스코프의 진입 노드 실행 |
| `SequenceParallelTests.cs` | 차례·동시 실행 |
| `DelayRepeatTests.cs` | 지연·반복 |
| `OnceLoggerTests.cs` | 경고 1회 |
| `TriggerRunnerTests.cs` | Restart·Ignore·Queue |
| `MotionRuntimeTests.cs` | 위상 규약, Start→Loop, End가 Loop 취소 |
| `SubGraphTests.cs` | 서브그래프 실행·깊이 제한·순환 검출 |
| `MotionAttributeTests.cs` | 오서링 어트리뷰트 |
| `PopupScenarioTests.cs` | 팝업 연출 전체를 조립한 통합 스모크 |

---

## Task 1: 패키지 스캐폴드와 두 어셈블리

검증 인프라를 가장 먼저 세운다. 이게 없으면 이후 모든 태스크의 TDD가 불가능하다.

**Files:**
- Create: `package.json`
- Create: `LICENSE.md`
- Create: `.gitignore`
- Create: `Runtime/Core/juahn.v2.UiMotion.Core.asmdef`
- Create: `Runtime/Unity/juahn.v2.UiMotion.asmdef`

- [ ] **Step 1: `package.json` 작성**

형제 패키지(`com.juahn.v2.di`)와 필드 구성을 맞춘다. CI가 `name`이 `com.juahn.v2.`로 시작하는지 검사한다.

```json
{
  "name": "com.juahn.v2.uimotion",
  "displayName": "UI Motion",
  "author": "armadimon",
  "version": "0.1.0",
  "unity": "6000.0",
  "license": "MIT",
  "type": "library",
  "description": "Graph-authored UI motion. Compose Start/Loop/End phases from nodes, save them as reusable preset assets, and play them with a single component. The core assembly has no UnityEngine dependency.",
  "dependencies": {}
}
```

- [ ] **Step 2: `LICENSE.md`를 형제 패키지에서 복사**

```bash
cp ../com.juahn.v2.di/LICENSE.md .
```

- [ ] **Step 3: `.gitignore` 작성**

Unity 산출물과 dotnet 산출물을 둘 다 제외한다. `.meta`는 커밋해야 하므로 제외하지 않는다.

```gitignore
# Unity
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Ll]ogs/
*.csproj
*.sln
*.user

# dotnet
bin/
[Bb]in/
**/TestResults/

# OS
.DS_Store
```

- [ ] **Step 4: Core 어셈블리 정의 작성**

`Runtime/Core/juahn.v2.UiMotion.Core.asmdef`. `noEngineReferences: true`가 이 계획 전체의 핵심 제약이다 — 이 값이 있으면 `using UnityEngine;`이 컴파일되지 않는다.

```json
{
    "name": "juahn.v2.UiMotion.Core",
    "rootNamespace": "Juahn.UiMotion",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 5: Unity 어셈블리 정의 작성 (자리만)**

`Runtime/Unity/juahn.v2.UiMotion.asmdef`. 계획 2가 여기를 채운다. 지금은 빈 어셈블리라 컴파일에 아무 영향이 없다.

```json
{
    "name": "juahn.v2.UiMotion",
    "rootNamespace": "Juahn.UiMotion.Unity",
    "references": [
        "juahn.v2.UiMotion.Core"
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

- [ ] **Step 6: 검증**

```bash
node -e "JSON.parse(require('fs').readFileSync('package.json','utf8')); console.log('package.json OK')"
ls Runtime/Core/*.asmdef Runtime/Unity/*.asmdef
```

기대 출력: `package.json OK` 와 두 asmdef 경로.

- [ ] **Step 7: 커밋**

```bash
git add package.json LICENSE.md .gitignore Runtime
git commit -m "chore: 패키지 스캐폴드와 두 어셈블리 정의 추가

코어를 UnityEngine 의존 여부로 나눈다. juahn.v2.UiMotion.Core는
noEngineReferences=true라 dotnet만으로 컴파일·테스트할 수 있다."
```

---

## Task 2: dotnet 테스트 하네스와 CI

Core 소스를 링크하는 NUnit 프로젝트를 만들고 GitHub Actions에 물린다. 이 태스크가 끝나면 이후 모든 태스크가 `dotnet test` 한 줄로 검증된다.

**Files:**
- Create: `Tests~/dotnet/UiMotion.Core.Tests.csproj`
- Create: `Tests~/dotnet/SanityTests.cs`
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests~/dotnet/SanityTests.cs`. 하네스 자체가 도는지 확인하는 최소 테스트다. 아직 Core에 타입이 없으므로 프레임워크 동작만 본다.

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SanityTests
    {
        [Test]
        public void TestHarness_Runs()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }
    }
}
```

- [ ] **Step 2: 테스트 프로젝트 작성**

`Tests~/dotnet/UiMotion.Core.Tests.csproj`.

`Tests~`의 `~` 접미사는 Unity가 이 폴더를 무시하게 만드는 UPM 관례다. 그래서 Unity 프로젝트에서는 이 csproj가 보이지 않고, `dotnet`에서는 정상적으로 쓰인다.

`Compile Include`로 Core 소스를 링크한다. 파일을 복사하지 않으므로 원본이 유일한 진실이다. `Runtime/Unity/`는 링크하지 않는다 — UnityEngine이 없으면 컴파일되지 않기 때문이다.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <RootNamespace>Juahn.UiMotion.Tests</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <!-- 테스트 소스 -->
    <Compile Include="**/*.cs" Exclude="bin/**;obj/**" />
  </ItemGroup>

  <ItemGroup>
    <!-- 순수 코어 소스를 링크한다. 복사하지 않는다. -->
    <Compile Include="../../Runtime/Core/**/*.cs" LinkBase="Core" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>

</Project>
```

`LangVersion` 9.0은 Unity 6000.0의 C# 버전에 맞춘 것이다. 이보다 새 문법을 쓰면 `dotnet`에서는 통과하고 Unity에서 깨진다.

- [ ] **Step 3: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj
```

기대: `Passed! - Failed: 0, Passed: 1`

여기서 실패하면 하네스 문제다. 다음으로 넘어가지 말 것.

- [ ] **Step 4: CI 워크플로 작성**

`.github/workflows/ci.yml`. 형제 패키지의 `_shared/ci.yml` 세 스텝을 그대로 두고 `dotnet test` 스텝을 뒤에 붙인다.

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
          console.log('package fields OK: ' + p.name + '@' + p.version);
          "

      - name: Check assembly definition present
        run: |
          if ! ls Runtime/**/*.asmdef >/dev/null 2>&1; then
            echo "no Runtime asmdef found"; exit 1
          fi
          echo "asmdef present"

      - name: Guard core assembly purity
        run: |
          if grep -rn "using UnityEngine" Runtime/Core/ ; then
            echo "Runtime/Core must not reference UnityEngine"; exit 1
          fi
          echo "core purity OK"

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run core tests
        run: dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
```

"Guard core assembly purity" 스텝을 넣은 이유는, `noEngineReferences`는 Unity 컴파일에서만 강제되고 `dotnet`에서는 강제되지 않기 때문이다. 이 grep이 없으면 Unity에서만 깨지는 코드가 CI를 통과한다.

- [ ] **Step 5: 순수성 가드가 실제로 동작하는지 확인**

```bash
grep -rn "using UnityEngine" Runtime/Core/ ; echo "exit=$?"
```

기대: 아무 출력 없이 `exit=1` (grep이 못 찾음 = 정상).

- [ ] **Step 6: 커밋**

```bash
git add Tests~ .github
git commit -m "test: dotnet 테스트 하네스와 CI 게이트 추가

Tests~/dotnet이 Core 소스를 링크해 Unity 없이 테스트한다.
CI에 순수성 가드(grep)를 넣어 UnityEngine 참조가 새어드는 것을 막는다."
```

---

## Task 3: `NodeId`

**Files:**
- Create: `Runtime/Core/Graph/NodeId.cs`
- Test: `Tests~/dotnet/NodeIdTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class NodeIdTests
    {
        [Test]
        public void Default_IsNone()
        {
            Assert.That(default(NodeId), Is.EqualTo(NodeId.None));
            Assert.That(NodeId.None.IsValid, Is.False);
        }

        [Test]
        public void PositiveValue_IsValid()
        {
            Assert.That(new NodeId(1).IsValid, Is.True);
            Assert.That(new NodeId(7).Value, Is.EqualTo(7));
        }

        [Test]
        public void ZeroOrNegative_IsNotValid()
        {
            Assert.That(new NodeId(0).IsValid, Is.False);
            Assert.That(new NodeId(-3).IsValid, Is.False);
        }

        [Test]
        public void SameValue_AreEqual()
        {
            Assert.That(new NodeId(4), Is.EqualTo(new NodeId(4)));
            Assert.That(new NodeId(4).GetHashCode(), Is.EqualTo(new NodeId(4).GetHashCode()));
        }

        [Test]
        public void DifferentValue_AreNotEqual()
        {
            Assert.That(new NodeId(4), Is.Not.EqualTo(new NodeId(5)));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter NodeIdTests
```

기대: 컴파일 실패 — `The type or namespace name 'NodeId' could not be found`

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Core/Graph/NodeId.cs`.

`int` 하나를 감싼 구조체다. 왜 `int`를 그냥 쓰지 않는가 — 그래프에는 노드 인덱스, 포트 번호, 자식 순번처럼 정수가 여럿 돌아다닌다. 전부 `int`면 서로 바꿔 넣어도 컴파일이 통과한다. 이 타입 하나가 그 실수를 컴파일 타임에 막는다.

0을 "없음"으로 예약하는 이유는 `default(NodeId)`가 자동으로 무효값이 되게 하기 위해서다. Unity 직렬화는 필드를 0으로 초기화하므로, 이렇게 두면 직렬화되지 않은 필드가 자동으로 `None`이 된다.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프 안에서 노드 하나를 가리키는 식별자.
    ///
    /// 0은 "없음"으로 예약한다. 그래서 <c>default(NodeId)</c>와 직렬화되지 않은 필드가
    /// 자동으로 <see cref="None"/>이 된다.
    ///
    /// <b>왜 readonly struct가 아니고 필드가 public인가</b> — Unity는 <c>readonly</c> 필드를
    /// 직렬화하지 않고, private 필드는 <c>[SerializeField]</c>가 있어야 직렬화한다.
    /// 그런데 <c>[SerializeField]</c>는 UnityEngine 타입이라 이 어셈블리에서 쓸 수 없다.
    /// 이 타입은 그래프 에셋에 저장되어야 하므로 그 제약이 캡슐화보다 우선한다.
    /// </summary>
    [Serializable]
    public struct NodeId : IEquatable<NodeId>
    {
        /// <summary>어떤 노드도 가리키지 않는 값.</summary>
        public static readonly NodeId None = default;

        /// <summary>원시 정수값. 직렬화 때문에 public 필드다 — 위 설명 참조.</summary>
        public int Value;

        public NodeId(int value)
        {
            Value = value;
        }

        /// <summary>실제 노드를 가리키는가.</summary>
        public bool IsValid => Value > 0;

        public bool Equals(NodeId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is NodeId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => IsValid ? "#" + Value : "#none";

        public static bool operator ==(NodeId a, NodeId b) => a.Value == b.Value;

        public static bool operator !=(NodeId a, NodeId b) => a.Value != b.Value;
    }
}
```

- [ ] **Step 4: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter NodeIdTests
```

기대: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 5: 커밋**

```bash
git add Runtime/Core/Graph/NodeId.cs Tests~/dotnet/NodeIdTests.cs
git commit -m "feat: NodeId 값 타입 추가

노드 식별자를 int와 구분해 컴파일 타임에 혼동을 막는다.
0을 None으로 예약해 직렬화되지 않은 필드가 자동으로 무효값이 되게 한다."
```

---

## Task 4: 이징

**Files:**
- Create: `Runtime/Core/Easing/EaseKind.cs`
- Create: `Runtime/Core/Easing/EaseLibrary.cs`
- Test: `Tests~/dotnet/EaseLibraryTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

모든 이징에 대해 성립해야 하는 불변식(경계값)을 먼저 못 박는다. 개별 함수의 중간값을 일일이 검사하지 않는다 — 그건 구현을 베끼는 테스트라 회귀를 못 잡는다.

```csharp
using System;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class EaseLibraryTests
    {
        private static EaseKind[] AllKinds()
        {
            return (EaseKind[])Enum.GetValues(typeof(EaseKind));
        }

        [Test]
        public void EveryEase_StartsAtZero()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, 0f), Is.EqualTo(0f).Within(1e-5f),
                    "t=0에서 0이 아님: " + kind);
            }
        }

        [Test]
        public void EveryEase_EndsAtOne()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, 1f), Is.EqualTo(1f).Within(1e-5f),
                    "t=1에서 1이 아님: " + kind);
            }
        }

        [Test]
        public void EveryEase_ClampsInputBelowZero()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, -5f), Is.EqualTo(0f).Within(1e-5f),
                    "음수 t를 클램프하지 않음: " + kind);
            }
        }

        [Test]
        public void EveryEase_ClampsInputAboveOne()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, 5f), Is.EqualTo(1f).Within(1e-5f),
                    "1보다 큰 t를 클램프하지 않음: " + kind);
            }
        }

        [Test]
        public void Linear_IsIdentity()
        {
            Assert.That(EaseLibrary.Evaluate(EaseKind.Linear, 0.25f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(EaseLibrary.Evaluate(EaseKind.Linear, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void OutQuad_IsAheadOfLinear_InTheMiddle()
        {
            // "Out" 계열은 빠르게 시작해 느리게 끝난다. 중간에서 선형보다 앞서 있어야 한다.
            float eased = EaseLibrary.Evaluate(EaseKind.OutQuad, 0.5f);
            Assert.That(eased, Is.GreaterThan(0.5f));
        }

        [Test]
        public void InQuad_IsBehindLinear_InTheMiddle()
        {
            float eased = EaseLibrary.Evaluate(EaseKind.InQuad, 0.5f);
            Assert.That(eased, Is.LessThan(0.5f));
        }

        [Test]
        public void OutBack_Overshoots()
        {
            // OutBack은 1을 넘었다가 돌아온다. 그게 이 이징의 존재 이유다.
            bool overshot = false;
            for (int i = 1; i < 100; i++)
            {
                if (EaseLibrary.Evaluate(EaseKind.OutBack, i / 100f) > 1f)
                {
                    overshot = true;
                    break;
                }
            }

            Assert.That(overshot, Is.True, "OutBack이 1을 넘지 않는다");
        }

        [Test]
        public void UnknownKind_FallsBackToLinear()
        {
            // 직렬화된 그래프가 미래 버전의 이징 값을 들고 올 수 있다. 죽지 않아야 한다.
            float value = EaseLibrary.Evaluate((EaseKind)9999, 0.25f);
            Assert.That(value, Is.EqualTo(0.25f).Within(1e-5f));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter EaseLibraryTests
```

기대: 컴파일 실패 — `The type or namespace name 'EaseKind' could not be found`

- [ ] **Step 3: `EaseKind` 작성**

`Runtime/Core/Easing/EaseKind.cs`.

명시적 정수값을 박는 이유는 직렬화 때문이다. 나중에 중간에 값을 추가해도 기존 그래프 에셋이 다른 이징으로 바뀌지 않는다.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 이징 종류. 값은 직렬화되므로 <b>절대 바꾸지 않는다</b>. 새 이징은 뒤에 추가한다.
    /// </summary>
    public enum EaseKind
    {
        Linear = 0,

        InQuad = 10,
        OutQuad = 11,
        InOutQuad = 12,

        InCubic = 20,
        OutCubic = 21,
        InOutCubic = 22,

        InSine = 30,
        OutSine = 31,
        InOutSine = 32,

        OutBack = 40,
        OutElastic = 41,
        OutBounce = 42,
    }
}
```

- [ ] **Step 4: `EaseLibrary` 작성**

`Runtime/Core/Easing/EaseLibrary.cs`.

`System.Math`만 쓴다(`UnityEngine.Mathf`는 못 쓴다). `float` 계산이므로 `Math.Sin` 등이 돌려주는 `double`을 명시적으로 캐스트한다.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 이징 함수 평가. 입력 t는 항상 [0,1]로 클램프된다.
    ///
    /// 반환값은 클램프하지 않는다 — <see cref="EaseKind.OutBack"/>처럼 1을 넘었다가 돌아오는
    /// 이징이 있고, 그 오버슈트가 연출의 핵심이기 때문이다.
    /// </summary>
    public static class EaseLibrary
    {
        private const float BackOvershoot = 1.70158f;

        /// <summary>
        /// <paramref name="t"/>를 [0,1]로 클램프한 뒤 이징을 적용한다.
        /// 모르는 <paramref name="kind"/>는 <see cref="EaseKind.Linear"/>로 떨어진다 —
        /// 미래 버전이 만든 그래프를 열어도 죽지 않기 위해서다.
        /// </summary>
        public static float Evaluate(EaseKind kind, float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }

            if (t >= 1f)
            {
                return 1f;
            }

            switch (kind)
            {
                case EaseKind.Linear: return t;

                case EaseKind.InQuad: return t * t;
                case EaseKind.OutQuad: return 1f - (1f - t) * (1f - t);
                case EaseKind.InOutQuad: return t < 0.5f
                    ? 2f * t * t
                    : 1f - 2f * (1f - t) * (1f - t);

                case EaseKind.InCubic: return t * t * t;
                case EaseKind.OutCubic:
                {
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                }
                case EaseKind.InOutCubic: return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - (float)Math.Pow(-2f * t + 2f, 3d) * 0.5f;

                case EaseKind.InSine: return 1f - (float)Math.Cos(t * Math.PI * 0.5d);
                case EaseKind.OutSine: return (float)Math.Sin(t * Math.PI * 0.5d);
                case EaseKind.InOutSine: return -(float)(Math.Cos(Math.PI * t) - 1d) * 0.5f;

                case EaseKind.OutBack:
                {
                    const float c1 = BackOvershoot;
                    const float c3 = c1 + 1f;
                    float inv = t - 1f;
                    return 1f + c3 * inv * inv * inv + c1 * inv * inv;
                }

                case EaseKind.OutElastic:
                {
                    const double period = 2d * Math.PI / 3d;
                    return (float)(Math.Pow(2d, -10d * t) * Math.Sin((t * 10d - 0.75d) * period) + 1d);
                }

                case EaseKind.OutBounce: return OutBounce(t);

                default: return t;
            }
        }

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
            {
                return n1 * t * t;
            }

            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }

            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
```

- [ ] **Step 5: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter EaseLibraryTests
```

기대: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Core/Easing Tests~/dotnet/EaseLibraryTests.cs
git commit -m "feat: 이징 함수 라이브러리 추가

System.Math만 써서 UnityEngine 없이 동작한다. 모르는 EaseKind는
Linear로 떨어져 미래 버전 그래프를 열어도 죽지 않는다."
```

---

## Task 5: `MotionTimer`

**Files:**
- Create: `Runtime/Core/Exec/MotionTimer.cs`
- Test: `Tests~/dotnet/MotionTimerTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionTimerTests
    {
        [Test]
        public void FreshTimer_IsNotDone()
        {
            var timer = new MotionTimer(1f);
            Assert.That(timer.IsDone, Is.False);
            Assert.That(timer.Normalized, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Advance_AccumulatesElapsed()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(0.25f);
            timer.Advance(0.25f);
            Assert.That(timer.Normalized, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Advance_PastDuration_IsDone()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(1.5f);
            Assert.That(timer.IsDone, Is.True);
        }

        [Test]
        public void Normalized_NeverExceedsOne()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(99f);
            Assert.That(timer.Normalized, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ZeroDuration_IsDoneImmediately()
        {
            // 지속시간 0은 "즉시"를 뜻한다. 한 프레임도 기다리지 않는다.
            var timer = new MotionTimer(0f);
            Assert.That(timer.IsDone, Is.True);
            Assert.That(timer.Normalized, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void NegativeDuration_IsTreatedAsZero()
        {
            var timer = new MotionTimer(-3f);
            Assert.That(timer.IsDone, Is.True);
        }

        [Test]
        public void Reset_RewindsToStart()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(0.8f);
            timer.Reset();
            Assert.That(timer.IsDone, Is.False);
            Assert.That(timer.Normalized, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Overshoot_ReportsLeftoverTime()
        {
            // 반복 노드가 남은 시간을 다음 사이클로 넘길 때 쓴다.
            // 이게 없으면 매 사이클마다 최대 한 프레임씩 밀린다.
            var timer = new MotionTimer(1f);
            timer.Advance(1.25f);
            Assert.That(timer.Overshoot, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void Overshoot_IsZero_WhileRunning()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(0.5f);
            Assert.That(timer.Overshoot, Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionTimerTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionTimer' could not be found`

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Core/Exec/MotionTimer.cs`.

구조체이므로 `Advance`가 값을 바꾸려면 호출부가 변수를 들고 있어야 한다. 테스트에서 `var timer = ...; timer.Advance(...)`가 동작하는 이유가 그것이다. 필드에 담아 쓸 때도 마찬가지다 — 프로퍼티에 담으면 변경이 사라지므로 반드시 필드에 담는다.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 지속시간이 있는 진행 상태. 시간은 바깥에서 <see cref="Advance"/>로 주입한다 —
    /// 코어가 시계를 갖지 않으므로 테스트가 결정적이다.
    ///
    /// <b>구조체다.</b> 프로퍼티가 아니라 필드에 담아야 <see cref="Advance"/>의 변경이 남는다.
    /// </summary>
    [Serializable]
    public struct MotionTimer
    {
        private readonly float _duration;
        private float _elapsed;

        /// <summary>
        /// 지속시간 0 이하는 "즉시"를 뜻한다 — 만들자마자 완료 상태다.
        /// </summary>
        public MotionTimer(float duration)
        {
            _duration = duration > 0f ? duration : 0f;
            _elapsed = 0f;
        }

        public float Duration => _duration;

        public float Elapsed => _elapsed;

        public bool IsDone => _elapsed >= _duration;

        /// <summary>진행률 [0,1]. 지속시간이 0이면 항상 1이다.</summary>
        public float Normalized
        {
            get
            {
                if (_duration <= 0f)
                {
                    return 1f;
                }

                float t = _elapsed / _duration;
                return t >= 1f ? 1f : t;
            }
        }

        /// <summary>
        /// 완료 후 초과한 시간. 반복 노드가 이 값을 다음 사이클로 넘겨
        /// 사이클마다 한 프레임씩 밀리는 것을 막는다. 진행 중에는 0이다.
        /// </summary>
        public float Overshoot
        {
            get
            {
                float over = _elapsed - _duration;
                return over > 0f ? over : 0f;
            }
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0f)
            {
                _elapsed += deltaSeconds;
            }
        }

        public void Reset()
        {
            _elapsed = 0f;
        }
    }
}
```

- [ ] **Step 4: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionTimerTests
```

기대: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 5: 커밋**

```bash
git add Runtime/Core/Exec/MotionTimer.cs Tests~/dotnet/MotionTimerTests.cs
git commit -m "feat: MotionTimer 추가

시간을 바깥에서 주입받아 코어가 시계를 갖지 않게 한다.
Overshoot을 노출해 반복 연출이 사이클마다 한 프레임씩 밀리는 것을 막는다."
```

---

## Task 6: 재생 핸들

**Files:**
- Create: `Runtime/Core/Exec/IMotionHandle.cs`
- Create: `Runtime/Core/Exec/MotionHandle.cs`
- Test: `Tests~/dotnet/MotionHandleTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionHandleTests
    {
        [Test]
        public void Completed_IsDoneImmediately()
        {
            Assert.That(MotionHandle.Completed.IsDone, Is.True);
        }

        [Test]
        public void Skipped_IsDoneImmediately()
        {
            Assert.That(MotionHandle.Skipped.IsDone, Is.True);
        }

        [Test]
        public void Skipped_IsDistinguishableFromCompleted()
        {
            // 진단이 "슬롯이 비어서 건너뜀"과 "정상적으로 즉시 끝남"을 구분해야 한다.
            Assert.That(MotionHandle.IsSkipped(MotionHandle.Skipped), Is.True);
            Assert.That(MotionHandle.IsSkipped(MotionHandle.Completed), Is.False);
        }

        [Test]
        public void FromTimer_ReportsProgress_UntilDone()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            Assert.That(handle.IsDone, Is.False);

            handle.Tick(0.5f);
            handle.Tick(0.5f);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(samples, Is.EqualTo(new[] { 0.5f, 1f }).Within(1e-5f));
        }

        [Test]
        public void FromTimer_EmitsFinalOne_EvenOnOvershoot()
        {
            // 마지막 프레임이 커서 duration을 훌쩍 넘겨도 끝값은 정확히 1이어야 한다.
            // 안 그러면 페이드인이 alpha 0.97에서 멈춘다.
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            handle.Tick(10f);

            Assert.That(samples, Is.EqualTo(new[] { 1f }).Within(1e-5f));
        }

        [Test]
        public void FromTimer_ZeroDuration_CompletesOnFirstTick_WithOne()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(0f, t => samples.Add(t));

            handle.Tick(0f);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(samples, Is.EqualTo(new[] { 1f }).Within(1e-5f));
        }

        [Test]
        public void FromTimer_Cancel_StopsProgress()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            handle.Tick(0.25f);
            handle.Cancel();
            handle.Tick(0.25f);

            Assert.That(handle.IsDone, Is.True, "취소된 핸들은 완료로 취급한다");
            Assert.That(samples.Count, Is.EqualTo(1), "취소 후에는 진행률을 더 쏘지 않는다");
        }

        [Test]
        public void FromTimer_TickAfterDone_DoesNothing()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            handle.Tick(1f);
            handle.Tick(1f);

            Assert.That(samples.Count, Is.EqualTo(1));
        }

        [Test]
        public void SharedHandles_AreSafeToReuse()
        {
            // Completed/Skipped는 싱글턴이라 수백 개 노드가 동시에 돌려준다.
            // 상태가 없어야 하고 Tick/Cancel이 아무 일도 하지 않아야 한다.
            MotionHandle.Completed.Tick(1f);
            MotionHandle.Completed.Cancel();
            Assert.That(MotionHandle.Completed.IsDone, Is.True);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionHandleTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionHandle' could not be found`

- [ ] **Step 3: `IMotionHandle` 작성**

`Runtime/Core/Exec/IMotionHandle.cs`.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 재생 중인 무언가. 노드 하나의 효과일 수도 있고, 흐름 노드가 조율하는 자식 묶음일 수도 있다.
    ///
    /// 시간은 <see cref="Tick"/>으로 바깥에서 들어온다. 핸들이 스스로 시계를 읽지 않는다.
    /// </summary>
    public interface IMotionHandle
    {
        /// <summary>끝났는가. <b>취소된 핸들도 완료로 취급한다</b> — 실행기는 둘을 구분하지 않는다.</summary>
        bool IsDone { get; }

        /// <summary>시간을 진행시킨다. <see cref="IsDone"/>이면 아무 일도 하지 않아야 한다.</summary>
        void Tick(float deltaSeconds);

        /// <summary>즉시 끊는다. 이미 끝났으면 아무 일도 하지 않아야 한다.</summary>
        void Cancel();
    }
}
```

- [ ] **Step 4: `MotionHandle` 작성**

`Runtime/Core/Exec/MotionHandle.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 흔한 <see cref="IMotionHandle"/>들을 만드는 팩토리.
    /// </summary>
    public static class MotionHandle
    {
        private static readonly DoneHandle CompletedInstance = new DoneHandle();
        private static readonly DoneHandle SkippedInstance = new DoneHandle();

        /// <summary>정상적으로 즉시 끝난 핸들. 상태가 없어 공유해도 안전하다.</summary>
        public static IMotionHandle Completed => CompletedInstance;

        /// <summary>
        /// 실행되지 못하고 건너뛴 핸들 — 슬롯이 비었거나 대상이 없을 때 돌려준다.
        /// 완료와 구분되는 이유는 진단 때문이다. 실행기는 둘을 똑같이 다룬다.
        /// </summary>
        public static IMotionHandle Skipped => SkippedInstance;

        /// <summary>이 핸들이 <see cref="Skipped"/>인가.</summary>
        public static bool IsSkipped(IMotionHandle handle)
        {
            return ReferenceEquals(handle, SkippedInstance);
        }

        /// <summary>
        /// <paramref name="duration"/>초 동안 진행률 [0,1]을 <paramref name="onProgress"/>로 보내는 핸들.
        /// 끝날 때 반드시 정확히 1을 한 번 보낸다 — 그러지 않으면 페이드인이 0.97에서 멈춘다.
        /// </summary>
        public static IMotionHandle FromTimer(float duration, Action<float> onProgress)
        {
            return new TimerHandle(duration, onProgress);
        }

        private sealed class DoneHandle : IMotionHandle
        {
            public bool IsDone => true;

            public void Tick(float deltaSeconds)
            {
            }

            public void Cancel()
            {
            }
        }

        private sealed class TimerHandle : IMotionHandle
        {
            private readonly Action<float> _onProgress;
            private MotionTimer _timer;
            private bool _finished;

            public TimerHandle(float duration, Action<float> onProgress)
            {
                _timer = new MotionTimer(duration);
                _onProgress = onProgress;
                _finished = false;
            }

            public bool IsDone => _finished;

            public void Tick(float deltaSeconds)
            {
                if (_finished)
                {
                    return;
                }

                _timer.Advance(deltaSeconds);

                // Normalized가 알아서 1로 클램프하므로 오버슈트해도 끝값은 정확히 1이다.
                float t = _timer.Normalized;

                if (_onProgress != null)
                {
                    _onProgress(t);
                }

                if (_timer.IsDone)
                {
                    _finished = true;
                }
            }

            public void Cancel()
            {
                _finished = true;
            }
        }
    }
}
```

- [ ] **Step 5: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionHandleTests
```

기대: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Core/Exec/IMotionHandle.cs Runtime/Core/Exec/MotionHandle.cs Tests~/dotnet/MotionHandleTests.cs
git commit -m "feat: 재생 핸들 계약과 기본 구현 추가

Skipped를 Completed와 구분해 슬롯 미할당을 진단할 수 있게 한다.
타이머 핸들은 오버슈트해도 끝값으로 정확히 1을 한 번 보낸다."
```

---

## Task 7: 그래프 계약

실행기가 그래프를 보는 창구를 정의한다. 여기서 정한 인터페이스가 계획 2의 `MotionGraph`(ScriptableObject)와 테스트용 가짜 그래프의 공통 계약이 된다.

**Files:**
- Create: `Runtime/Core/Graph/SlotRef.cs`
- Create: `Runtime/Core/Graph/SlotDeclaration.cs`
- Create: `Runtime/Core/Graph/TriggerPolicy.cs`
- Create: `Runtime/Core/Graph/TriggerDeclaration.cs`
- Create: `Runtime/Core/Graph/IMotionGraphView.cs`
- Test: `Tests~/dotnet/SlotRefTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

인터페이스는 테스트할 것이 없다. 값 타입인 `SlotRef`만 테스트한다.

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SlotRefTests
    {
        [Test]
        public void Self_UsesReservedName()
        {
            Assert.That(SlotRef.Self.Name, Is.EqualTo("Self"));
            Assert.That(SlotRef.Self.IsSelf, Is.True);
        }

        [Test]
        public void Default_IsNotValid()
        {
            Assert.That(default(SlotRef).IsValid, Is.False);
        }

        [Test]
        public void EmptyName_IsNotValid()
        {
            Assert.That(new SlotRef("").IsValid, Is.False);
            Assert.That(new SlotRef("   ").IsValid, Is.False);
        }

        [Test]
        public void NamedSlot_IsValidAndNotSelf()
        {
            var slot = new SlotRef("Icon");
            Assert.That(slot.IsValid, Is.True);
            Assert.That(slot.IsSelf, Is.False);
        }

        [Test]
        public void SelfName_IsCaseSensitive()
        {
            // 슬롯 이름은 계층의 오브젝트 이름과 대조되므로 대소문자를 구분한다.
            // "self"라는 자식을 만든 사람이 Self 슬롯을 덮어쓰면 안 된다.
            Assert.That(new SlotRef("self").IsSelf, Is.False);
        }

        [Test]
        public void SameName_AreEqual()
        {
            Assert.That(new SlotRef("Panel"), Is.EqualTo(new SlotRef("Panel")));
            Assert.That(new SlotRef("Panel").GetHashCode(),
                Is.EqualTo(new SlotRef("Panel").GetHashCode()));
        }

        [Test]
        public void DifferentName_AreNotEqual()
        {
            Assert.That(new SlotRef("Panel"), Is.Not.EqualTo(new SlotRef("Icon")));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter SlotRefTests
```

기대: 컴파일 실패 — `The type or namespace name 'SlotRef' could not be found`

- [ ] **Step 3: `SlotRef` 작성**

`Runtime/Core/Graph/SlotRef.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 "무엇을 움직일지"를 가리키는 이름. 실제 오브젝트는 프리팹의 플레이어가 채운다.
    ///
    /// 이름으로 가리키는 덕분에 그래프 에셋 하나를 여러 프리팹에서 재사용할 수 있다.
    ///
    /// <see cref="NodeId"/>와 같은 이유로 <c>readonly struct</c>가 아니고 필드가 public이다 —
    /// 이 타입은 노드의 필드로 그래프 에셋에 직렬화된다.
    /// </summary>
    [Serializable]
    public struct SlotRef : IEquatable<SlotRef>
    {
        /// <summary>예약 슬롯 이름. 언제나 플레이어 자신을 가리킨다.</summary>
        public const string SelfName = "Self";

        /// <summary>플레이어 자신.</summary>
        public static readonly SlotRef Self = new SlotRef(SelfName);

        /// <summary>슬롯 이름. 직렬화 때문에 public 필드다.</summary>
        public string Name;

        public SlotRef(string name)
        {
            Name = name;
        }

        /// <summary>이름이 채워져 있는가.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Name);

        /// <summary>
        /// 예약 슬롯인가. <b>대소문자를 구분한다</b> — 슬롯 이름은 계층의 오브젝트 이름과
        /// 대조되므로, "self"라는 자식이 예약 슬롯을 덮어쓰면 안 된다.
        /// </summary>
        public bool IsSelf => string.Equals(Name, SelfName, StringComparison.Ordinal);

        public bool Equals(SlotRef other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SlotRef other && Equals(other);

        public override int GetHashCode() => Name == null ? 0 : Name.GetHashCode();

        public override string ToString() => IsValid ? Name : "<unbound>";
    }
}
```

- [ ] **Step 4: `TriggerPolicy` 작성**

`Runtime/Core/Graph/TriggerPolicy.cs`.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 이미 재생 중인 트리거를 다시 발사했을 때의 처리. 값은 직렬화되므로 바꾸지 않는다.
    /// </summary>
    public enum TriggerPolicy
    {
        /// <summary>돌던 것을 끊고 처음부터 다시. 기본값.</summary>
        Restart = 0,

        /// <summary>돌고 있으면 새 발사를 버린다.</summary>
        Ignore = 1,

        /// <summary>현재 것이 끝난 뒤에 실행한다.</summary>
        Queue = 2,
    }
}
```

- [ ] **Step 5: `TriggerDeclaration`과 `SlotDeclaration` 작성**

`Runtime/Core/Graph/TriggerDeclaration.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>그래프가 선언한 트리거 하나.</summary>
    [Serializable]
    public sealed class TriggerDeclaration
    {
        public string Name;
        public TriggerPolicy Policy;
        public NodeId Entry;

        public TriggerDeclaration()
        {
        }

        public TriggerDeclaration(string name, NodeId entry, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            Name = name;
            Entry = entry;
            Policy = policy;
        }
    }
}
```

`Runtime/Core/Graph/SlotDeclaration.cs`.

`RequiredType`이 `System.Type`인 것은 문제없다 — `Type`은 순수 C#이다. 코어는 이 값을 검사하지 않고 그대로 나르기만 하며, 실제 타입 대조는 Unity 계층과 에디터가 한다.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프가 선언한 슬롯 하나. 프리팹의 플레이어가 이 목록을 보고 인스펙터를 그린다.
    /// </summary>
    [Serializable]
    public sealed class SlotDeclaration
    {
        public string Name;

        /// <summary>
        /// 이 슬롯에 넣을 수 있는 타입. 코어는 검사하지 않고 나르기만 한다 —
        /// 실제 대조는 Unity 계층과 에디터 인스펙터가 한다.
        /// </summary>
        [NonSerialized] public Type RequiredType;

        public SlotDeclaration()
        {
        }

        public SlotDeclaration(string name, Type requiredType = null)
        {
            Name = name;
            RequiredType = requiredType;
        }
    }
}
```

- [ ] **Step 6: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter SlotRefTests
```

기대: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 7: `IMotionGraphView` 작성**

`Runtime/Core/Graph/IMotionGraphView.cs`.

이 파일은 `MotionNodeBase`를 참조하는데 그 타입은 Task 8에서 만든다. 그래서 **이 시점에는 컴파일이 깨진다** — 정상이다. Task 8이 끝나면 붙는다.

```csharp
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 실행기가 그래프를 보는 <b>유일한</b> 창구. 실행기는 ScriptableObject도 직렬화도 모른다.
    ///
    /// 계획 2의 <c>MotionGraph</c>(ScriptableObject)와 테스트용 가짜 그래프가 이것을 구현한다.
    /// 그래프는 실행 상태를 일절 갖지 않는다 — 같은 에셋을 수백 개 오브젝트가 동시에 쓴다.
    /// </summary>
    public interface IMotionGraphView
    {
        /// <summary>진단용 이름.</summary>
        string GraphName { get; }

        IReadOnlyList<TriggerDeclaration> Triggers { get; }

        IReadOnlyList<SlotDeclaration> Slots { get; }

        /// <summary>노드를 찾는다. 없으면 null — 결손 노드는 실행 시 건너뛴다.</summary>
        MotionNodeBase GetNode(NodeId id);

        /// <summary>트리거의 진입 노드. 그런 트리거가 없으면 <see cref="NodeId.None"/>.</summary>
        NodeId GetEntry(string triggerName);

        /// <summary>
        /// 이 노드의 자식들. <b>순서가 보장된다</b> — <c>SequenceNode</c>가 이 순서로 실행한다.
        /// 자식이 없으면 빈 목록을 돌려준다(null 금지).
        /// </summary>
        IReadOnlyList<NodeId> GetChildren(NodeId parent);
    }
}
```

- [ ] **Step 8: 커밋하지 않고 Task 8로 진행**

`IMotionGraphView`가 아직 없는 타입을 참조하므로 지금 커밋하면 빌드가 깨진 상태가 남는다. Task 8이 끝난 뒤 함께 커밋한다.

---

## Task 8: 노드 베이스와 실행 문맥

노드가 무엇이고 실행 중에 무엇을 받는지를 정한다. 이 계약이 이후 모든 노드의 기반이다.

**Files:**
- Create: `Runtime/Core/Diag/IMotionLog.cs`
- Create: `Runtime/Core/Authoring/ISlotResolver.cs`
- Create: `Runtime/Core/Authoring/IMotionContext.cs`
- Create: `Runtime/Core/Authoring/MotionNodeBase.cs`
- Create: `Runtime/Core/Authoring/MotionFlowNode.cs`
- Create: `Runtime/Core/Authoring/MotionEffectNode.cs`
- Test: `Tests~/dotnet/MotionNodeBaseTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionNodeBaseTests
    {
        private sealed class PlainEffect : MotionEffectNode
        {
            public int PlayCount;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                PlayCount++;
                return MotionHandle.Completed;
            }
        }

        private sealed class OwningFlow : MotionFlowNode
        {
            public override bool OwnsChildren => true;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }

        [Test]
        public void EffectNode_DoesNotOwnChildren_ByDefault()
        {
            // 효과 노드의 자식은 효과가 끝난 뒤 실행기가 이어 붙인다.
            Assert.That(new PlainEffect().OwnsChildren, Is.False);
        }

        [Test]
        public void EffectNode_DoesNotRevert_ByDefault()
        {
            Assert.That(new PlainEffect().Reverts, Is.False);
        }

        [Test]
        public void FlowNode_CanOwnChildren()
        {
            Assert.That(new OwningFlow().OwnsChildren, Is.True);
        }

        [Test]
        public void Play_DelegatesToOnPlay()
        {
            var node = new PlainEffect();
            IMotionHandle handle = node.Play(null);

            Assert.That(node.PlayCount, Is.EqualTo(1));
            Assert.That(handle.IsDone, Is.True);
        }

        [Test]
        public void Play_NeverReturnsNull()
        {
            // 노드가 실수로 null을 돌려줘도 실행기가 죽으면 안 된다.
            var node = new NullReturningEffect();
            IMotionHandle handle = node.Play(null);

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.IsDone, Is.True);
        }

        private sealed class NullReturningEffect : MotionEffectNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionNodeBaseTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionEffectNode' could not be found`

- [ ] **Step 3: `IMotionLog` 작성**

`Runtime/Core/Diag/IMotionLog.cs`.

코어는 `Debug.Log`를 부를 수 없다(UnityEngine). 로그를 인터페이스로 빼서 Unity 계층이 꽂는다.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 코어가 진단을 내보내는 곳. 코어는 UnityEngine을 참조하지 않으므로
    /// <c>Debug.Log</c>를 직접 부를 수 없다 — Unity 계층이 구현을 꽂는다.
    /// </summary>
    public interface IMotionLog
    {
        void Warn(string message);
        void Error(string message);
    }
}
```

- [ ] **Step 4: `ISlotResolver` 작성**

`Runtime/Core/Authoring/ISlotResolver.cs`.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯 이름을 실제 대상으로 바꾼다. Unity 계층의 <c>SlotTable</c>이 구현한다.
    ///
    /// 코어는 대상이 무엇인지 모른다 — <c>object</c>로 받아 효과 노드가 캐스트한다.
    /// 그래서 코어가 <c>RectTransform</c> 같은 Unity 타입을 몰라도 된다.
    /// </summary>
    public interface ISlotResolver
    {
        /// <summary>바인딩되지 않았으면 null.</summary>
        object Resolve(SlotRef slot);
    }
}
```

- [ ] **Step 5: `IMotionScope`와 `IMotionContext` 작성**

`Runtime/Core/Exec/IMotionScope.cs`.

노드가 스코프에 대해 알아야 하는 것은 "원상 복구를 등록한다" 하나뿐이다. 그 좁은 면만 인터페이스로 노출하고 구현체(`MotionScope`, Task 9)는 훨씬 많은 일을 한다.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 보는 스코프의 면. 실행 제어(Tick·Cancel)는 실행기의 몫이므로 여기 없다.
    /// </summary>
    public interface IMotionScope
    {
        /// <summary>
        /// 이 스코프가 <b>취소될 때</b> 실행할 복구 동작을 등록한다. 등록의 역순으로 실행된다.
        ///
        /// 자연 완료 시에는 실행되지 않는다 — 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다.
        /// 되돌림은 "중간에 끊겼다"의 처리이지 "끝났다"의 처리가 아니다.
        /// </summary>
        void Remember(Action revert);
    }
}
```

`Runtime/Core/Authoring/IMotionContext.cs`.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 실행될 때 받는 문맥. 노드는 이것 말고 바깥세상을 알지 못한다.
    /// </summary>
    public interface IMotionContext
    {
        /// <summary>지금 돌고 있는 그래프.</summary>
        IMotionGraphView Graph { get; }

        /// <summary>이 실행이 속한 스코프. 원상 복구를 여기에 등록한다.</summary>
        IMotionScope Scope { get; }

        IMotionLog Log { get; }

        /// <summary>슬롯을 실제 대상으로. 바인딩되지 않았으면 null.</summary>
        object ResolveSlot(SlotRef slot);
    }
}
```

- [ ] **Step 6: `MotionNodeBase` 작성**

`Runtime/Core/Authoring/MotionNodeBase.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 모든 노드의 베이스. 직접 상속하지 말고 <see cref="MotionEffectNode"/>(무언가를 움직인다)나
    /// <see cref="MotionFlowNode"/>(실행 순서를 정한다) 중 하나를 고른다.
    ///
    /// <b>UnityEngine을 참조하지 않는다.</b> Unity 직렬화에 필요한 <see cref="SerializableAttribute"/>와
    /// public 필드는 순수 C#이라 이 어셈블리에 둘 수 있다. 인스펙터 표시는
    /// <c>[MotionParam]</c>이 대신한다.
    /// </summary>
    [Serializable]
    public abstract class MotionNodeBase
    {
        /// <summary>그래프 안에서의 식별자. 그래프가 채운다.</summary>
        public NodeId Id;

        /// <summary>
        /// 자식 실행을 이 노드가 직접 조율하는가.
        ///
        /// false(기본)면 실행기가 이 노드의 핸들이 끝난 뒤 자식들을 <b>동시에</b> 시작한다.
        /// true면 실행기가 손대지 않고 노드가 알아서 한다 — <c>Sequence</c>·<c>Parallel</c>·<c>Repeat</c>가 그렇다.
        /// </summary>
        public virtual bool OwnsChildren => false;

        /// <summary>
        /// 중단되면 대상을 원래 상태로 되돌리는가. <b>엔진은 이 값을 읽지 않는다</b> —
        /// 되돌릴지는 노드가 <c>ctx.Scope.Remember</c>를 부르는지로 정해진다.
        /// 이 속성은 에디터와 Node Doctor가 표시용으로 쓴다.
        /// </summary>
        public virtual bool Reverts => false;

        /// <summary>
        /// 이 노드를 시작한다. <b>절대 null을 돌려주지 않는다</b> —
        /// 파생이 null을 주면 <see cref="MotionHandle.Completed"/>로 바꾼다.
        /// </summary>
        public IMotionHandle Play(IMotionContext ctx)
        {
            IMotionHandle handle = OnPlay(ctx);
            return handle ?? MotionHandle.Completed;
        }

        /// <summary>파생이 채운다. 실제로 무엇을 시작할지.</summary>
        protected abstract IMotionHandle OnPlay(IMotionContext ctx);
    }
}
```

- [ ] **Step 7: `MotionEffectNode`와 `MotionFlowNode` 작성**

`Runtime/Core/Authoring/MotionEffectNode.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 무언가를 움직이는 노드. 슬롯을 잡고 값을 바꾼다.
    ///
    /// 자식을 소유하지 않는다 — 효과가 끝나면 실행기가 자식들을 이어서 시작한다.
    /// 그래서 <c>Punch → Fade</c> 배선이 "펀치가 끝나면 페이드"로 읽힌다.
    /// </summary>
    [Serializable]
    public abstract class MotionEffectNode : MotionNodeBase
    {
    }
}
```

`Runtime/Core/Authoring/MotionFlowNode.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 실행 순서를 정하는 노드. 대상을 만지지 않는다.
    ///
    /// 자식을 직접 조율하는 흐름 노드는 <see cref="MotionNodeBase.OwnsChildren"/>을 true로 덮는다.
    /// 덮지 않으면(예: <c>Delay</c>) 실행기가 자기 핸들이 끝난 뒤 자식들을 이어 붙인다.
    /// </summary>
    [Serializable]
    public abstract class MotionFlowNode : MotionNodeBase
    {
    }
}
```

- [ ] **Step 8: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter "MotionNodeBaseTests|SlotRefTests"
```

기대: `Passed! - Failed: 0, Passed: 12`

이 시점에서 전체 빌드가 통과해야 한다. Task 7에서 만든 `IMotionGraphView`가 참조하던 `MotionNodeBase`가 방금 생겼기 때문이다.

- [ ] **Step 9: 커밋**

```bash
git add Runtime/Core/Graph Runtime/Core/Authoring Runtime/Core/Diag Runtime/Core/Exec/IMotionScope.cs Tests~/dotnet/SlotRefTests.cs Tests~/dotnet/MotionNodeBaseTests.cs
git commit -m "feat: 그래프 계약과 노드 베이스 추가

실행기가 그래프를 보는 창구를 IMotionGraphView 하나로 좁힌다.
노드는 OwnsChildren으로 자식 조율을 직접 할지 실행기에 맡길지 정한다.
슬롯 해석과 로그는 인터페이스로 빼서 코어의 UnityEngine 무의존을 지킨다."
```

---

## Task 9: 테스트 도구

이후 모든 테스트가 쓰는 가짜 그래프·노드·문맥을 만든다. 이 태스크에는 프로덕션 코드가 없다.

**Files:**
- Create: `Tests~/dotnet/Fakes/FakeGraph.cs`
- Create: `Tests~/dotnet/Fakes/FakeNodes.cs`
- Create: `Tests~/dotnet/Fakes/FakeContext.cs`
- Test: `Tests~/dotnet/Fakes/FakeGraphTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

도구 자체도 틀리면 이후 모든 테스트가 거짓말을 한다. 그래서 도구에도 테스트를 붙인다.

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class FakeGraphTests
    {
        [Test]
        public void AddNode_AssignsSequentialIds()
        {
            var graph = new FakeGraph();
            NodeId a = graph.Add(new RecordingEffect("a"));
            NodeId b = graph.Add(new RecordingEffect("b"));

            Assert.That(a.Value, Is.EqualTo(1));
            Assert.That(b.Value, Is.EqualTo(2));
        }

        [Test]
        public void AddNode_SetsNodeIdOnTheNode()
        {
            var graph = new FakeGraph();
            var node = new RecordingEffect("a");
            NodeId id = graph.Add(node);

            Assert.That(node.Id, Is.EqualTo(id));
        }

        [Test]
        public void GetNode_ReturnsNull_ForUnknownId()
        {
            var graph = new FakeGraph();
            Assert.That(graph.GetNode(new NodeId(99)), Is.Null);
        }

        [Test]
        public void Link_PreservesChildOrder()
        {
            var graph = new FakeGraph();
            NodeId parent = graph.Add(new RecordingEffect("p"));
            NodeId first = graph.Add(new RecordingEffect("1"));
            NodeId second = graph.Add(new RecordingEffect("2"));

            graph.Link(parent, first);
            graph.Link(parent, second);

            Assert.That(graph.GetChildren(parent), Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void GetChildren_ReturnsEmpty_NotNull_ForLeaf()
        {
            var graph = new FakeGraph();
            NodeId leaf = graph.Add(new RecordingEffect("leaf"));

            Assert.That(graph.GetChildren(leaf), Is.Not.Null);
            Assert.That(graph.GetChildren(leaf).Count, Is.EqualTo(0));
        }

        [Test]
        public void GetEntry_ReturnsNone_ForUnknownTrigger()
        {
            var graph = new FakeGraph();
            Assert.That(graph.GetEntry("Nope"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void DeclareTrigger_MakesEntryFindable()
        {
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("e"));
            graph.DeclareTrigger("Start", entry);

            Assert.That(graph.GetEntry("Start"), Is.EqualTo(entry));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter FakeGraphTests
```

기대: 컴파일 실패 — `The type or namespace name 'FakeGraph' could not be found`

- [ ] **Step 3: `FakeGraph` 작성**

`Tests~/dotnet/Fakes/FakeGraph.cs`.

```csharp
using System.Collections.Generic;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// 테스트용 인메모리 그래프. 노드를 넣고 링크하면 <see cref="IMotionGraphView"/>가 된다.
    /// </summary>
    public sealed class FakeGraph : IMotionGraphView
    {
        private readonly List<MotionNodeBase> _nodes = new List<MotionNodeBase>();
        private readonly Dictionary<int, List<NodeId>> _children = new Dictionary<int, List<NodeId>>();
        private readonly List<TriggerDeclaration> _triggers = new List<TriggerDeclaration>();
        private readonly List<SlotDeclaration> _slots = new List<SlotDeclaration>();

        private static readonly NodeId[] NoChildren = new NodeId[0];

        public string GraphName { get; set; } = "FakeGraph";

        public IReadOnlyList<TriggerDeclaration> Triggers => _triggers;

        public IReadOnlyList<SlotDeclaration> Slots => _slots;

        /// <summary>노드를 넣고 id를 부여한다. id는 1부터 순서대로다.</summary>
        public NodeId Add(MotionNodeBase node)
        {
            _nodes.Add(node);
            var id = new NodeId(_nodes.Count);
            node.Id = id;
            return id;
        }

        /// <summary>부모에 자식을 잇는다. 부른 순서가 곧 자식 순서다.</summary>
        public void Link(NodeId parent, NodeId child)
        {
            List<NodeId> list;
            if (!_children.TryGetValue(parent.Value, out list))
            {
                list = new List<NodeId>();
                _children[parent.Value] = list;
            }

            list.Add(child);
        }

        public void DeclareTrigger(string name, NodeId entry, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            _triggers.Add(new TriggerDeclaration(name, entry, policy));
        }

        public void DeclareSlot(string name)
        {
            _slots.Add(new SlotDeclaration(name));
        }

        public MotionNodeBase GetNode(NodeId id)
        {
            int index = id.Value - 1;
            if (index < 0 || index >= _nodes.Count)
            {
                return null;
            }

            return _nodes[index];
        }

        public NodeId GetEntry(string triggerName)
        {
            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i].Name == triggerName)
                {
                    return _triggers[i].Entry;
                }
            }

            return NodeId.None;
        }

        public IReadOnlyList<NodeId> GetChildren(NodeId parent)
        {
            List<NodeId> list;
            if (_children.TryGetValue(parent.Value, out list))
            {
                return list;
            }

            return NoChildren;
        }
    }
}
```

- [ ] **Step 4: `FakeNodes` 작성**

`Tests~/dotnet/Fakes/FakeNodes.cs`.

실행 순서를 검증하려면 "언제 시작했고 언제 끝났는지"를 기록하는 노드가 필요하다. 공용 기록장(`ExecutionLog`)에 이름을 남긴다.

```csharp
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
    /// 시작과 종료를 기록하는 효과 노드. <paramref name="duration"/>이 0이면 즉시 끝난다.
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
                string name = _name;
                ctx.Scope.Remember(() =>
                {
                    if (captured != null)
                    {
                        captured.Add(name + ":revert");
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
            return MotionHandle.FromTimer(_duration, t =>
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
```

- [ ] **Step 5: `FakeContext` 작성**

`Tests~/dotnet/Fakes/FakeContext.cs`.

```csharp
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
```

- [ ] **Step 6: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter FakeGraphTests
```

기대: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 7: 커밋**

```bash
git add Tests~/dotnet/Fakes
git commit -m "test: 인메모리 그래프와 기록 노드 등 테스트 도구 추가

이후 모든 테스트가 이 도구로 실행 순서와 취소 동작을 확인한다.
도구 자체가 틀리면 모든 테스트가 거짓말을 하므로 도구에도 테스트를 붙인다."
```

---

## Task 10: `MotionScope` — 원상 복구

스코프의 절반을 먼저 만든다. 노드 실행은 Task 12에서 붙인다.

**Files:**
- Create: `Runtime/Core/Exec/MotionScope.cs`
- Test: `Tests~/dotnet/MotionScopeTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionScopeTests
    {
        [Test]
        public void FreshScope_IsNotDoneAndNotCancelled()
        {
            var scope = new MotionScope("Start");
            Assert.That(scope.IsDone, Is.False);
            Assert.That(scope.IsCancelled, Is.False);
            Assert.That(scope.TriggerName, Is.EqualTo("Start"));
        }

        [Test]
        public void Cancel_RunsRevertsInReverseOrder()
        {
            // 역순이어야 하는 이유: 나중에 등록된 것이 앞의 것 위에 쌓여 있다.
            // 스케일을 바꾼 뒤 위치를 바꿨다면, 위치를 먼저 되돌려야 한다.
            var order = new List<string>();
            var scope = new MotionScope("Start");

            scope.Remember(() => order.Add("first"));
            scope.Remember(() => order.Add("second"));
            scope.Remember(() => order.Add("third"));

            scope.Cancel();

            Assert.That(order, Is.EqualTo(new[] { "third", "second", "first" }));
        }

        [Test]
        public void Cancel_MarksCancelledAndDone()
        {
            var scope = new MotionScope("Start");
            scope.Cancel();

            Assert.That(scope.IsCancelled, Is.True);
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Cancel_Twice_RunsRevertsOnlyOnce()
        {
            int calls = 0;
            var scope = new MotionScope("Start");
            scope.Remember(() => calls++);

            scope.Cancel();
            scope.Cancel();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Remember_AfterCancel_RunsImmediately()
        {
            // 취소된 스코프에 늦게 등록이 들어오면(비동기 콜백 등) 그 자리에서 되돌려야 한다.
            // 안 그러면 아무도 되돌리지 않아 값이 남는다.
            int calls = 0;
            var scope = new MotionScope("Start");
            scope.Cancel();

            scope.Remember(() => calls++);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Remember_Null_IsIgnored()
        {
            var scope = new MotionScope("Start");
            Assert.DoesNotThrow(() => scope.Remember(null));
            Assert.DoesNotThrow(() => scope.Cancel());
        }

        [Test]
        public void RevertThatThrows_DoesNotStopOtherReverts()
        {
            // 오브젝트 하나가 이미 파괴돼 예외가 나도 나머지는 전부 되돌려야 한다.
            var order = new List<string>();
            var log = new FakeLog();
            var scope = new MotionScope("Start", log);

            scope.Remember(() => order.Add("a"));
            scope.Remember(() => throw new System.InvalidOperationException("boom"));
            scope.Remember(() => order.Add("c"));

            scope.Cancel();

            Assert.That(order, Is.EqualTo(new[] { "c", "a" }));
            Assert.That(log.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_RaisesCompleted()
        {
            int raised = 0;
            var scope = new MotionScope("Start");
            scope.Completed += () => raised++;

            scope.Cancel();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_Twice_RaisesCompletedOnce()
        {
            int raised = 0;
            var scope = new MotionScope("Start");
            scope.Completed += () => raised++;

            scope.Cancel();
            scope.Cancel();

            Assert.That(raised, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionScopeTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionScope' could not be found`

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Core/Exec/MotionScope.cs`.

Task 12에서 노드 실행(`Begin`·`Tick`)을 이 클래스에 덧붙인다. 지금은 원상 복구와 완료 신호만 담는다.

```csharp
using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거 1회 발사가 만드는 실행 단위. 그 발사가 시작한 모든 것을 소유하고,
    /// 취소되면 전부 정리한다.
    ///
    /// 그래프가 아니라 <b>스코프가</b> 실행 상태를 갖는다. 같은 그래프 에셋을 수백 개
    /// 오브젝트가 동시에 쓰는 것이 정상적인 사용법이기 때문이다.
    /// </summary>
    public sealed class MotionScope : IMotionScope
    {
        private readonly List<Action> _reverts = new List<Action>();
        private readonly IMotionLog _log;

        private bool _done;
        private bool _cancelled;

        public MotionScope(string triggerName, IMotionLog log = null)
        {
            TriggerName = triggerName;
            _log = log;
        }

        /// <summary>이 스코프를 만든 트리거의 이름. 진단용.</summary>
        public string TriggerName { get; }

        /// <summary>끝났는가. 자연 완료와 취소를 모두 포함한다.</summary>
        public bool IsDone => _done;

        /// <summary>중간에 끊겼는가.</summary>
        public bool IsCancelled => _cancelled;

        /// <summary>끝났을 때 한 번 발생한다. 자연 완료든 취소든 한 번만.</summary>
        public event Action Completed;

        /// <summary>
        /// 취소될 때 실행할 복구 동작을 등록한다. 등록의 역순으로 실행된다 —
        /// 나중에 등록된 변경이 앞의 것 위에 쌓여 있기 때문이다.
        ///
        /// 이미 취소된 스코프에 등록하면 <b>그 자리에서 즉시</b> 실행한다.
        /// 안 그러면 늦게 도착한 등록을 아무도 되돌리지 않아 값이 남는다.
        /// </summary>
        public void Remember(Action revert)
        {
            if (revert == null)
            {
                return;
            }

            if (_cancelled)
            {
                RunRevert(revert);
                return;
            }

            _reverts.Add(revert);
        }

        /// <summary>즉시 끊고 등록된 복구를 역순으로 전부 실행한다. 두 번 불러도 한 번만 동작한다.</summary>
        public void Cancel()
        {
            if (_done)
            {
                return;
            }

            _cancelled = true;
            _done = true;

            for (int i = _reverts.Count - 1; i >= 0; i--)
            {
                RunRevert(_reverts[i]);
            }

            _reverts.Clear();

            RaiseCompleted();
        }

        /// <summary>
        /// 자연 완료로 표시한다. <b>복구를 실행하지 않는다</b> —
        /// 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다.
        /// </summary>
        internal void CompleteNaturally()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            _reverts.Clear();

            RaiseCompleted();
        }

        private void RaiseCompleted()
        {
            Action handler = Completed;
            if (handler != null)
            {
                handler();
            }
        }

        private void RunRevert(Action revert)
        {
            // 대상 하나가 이미 파괴돼 예외가 나도 나머지는 전부 되돌려야 한다.
            try
            {
                revert();
            }
            catch (Exception e)
            {
                if (_log != null)
                {
                    _log.Error("revert failed in scope '" + TriggerName + "': " + e.Message);
                }
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionScopeTests
```

기대: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 5: 커밋**

```bash
git add Runtime/Core/Exec/MotionScope.cs Tests~/dotnet/MotionScopeTests.cs
git commit -m "feat: MotionScope의 원상 복구 부분 추가

취소 시 등록의 역순으로 되돌린다. 자연 완료 시에는 되돌리지 않는다 —
되돌림은 '끊겼다'의 처리이지 '끝났다'의 처리가 아니다.
복구 하나가 예외를 던져도 나머지는 전부 실행된다."
```

---

## Task 11: `NodeRun` — 노드 하나와 자식 전파

**Files:**
- Create: `Runtime/Core/Exec/MotionContext.cs`
- Create: `Runtime/Core/Exec/NodeRun.cs`
- Test: `Tests~/dotnet/NodeRunTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class NodeRunTests
    {
        private static MotionContext Context(FakeGraph graph, MotionScope scope)
        {
            return new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog());
        }

        [Test]
        public void InstantNode_IsDoneAfterFirstTick()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId id = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), id);

            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void ChildrenStart_AfterParentCompletes()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new RecordingEffect("p", 1f) { Log = log });
            NodeId child = graph.Add(new RecordingEffect("c") { Log = log });
            graph.Link(parent, child);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0.5f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start" }), "부모가 도는 중엔 자식이 시작되지 않는다");

            run.Tick(0.5f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start", "p:end", "c:start", "c:end" }));
            Assert.That(run.IsDone, Is.True);
        }

        [Test]
        public void MultipleChildren_StartTogether()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new RecordingEffect("p") { Log = log });
            NodeId a = graph.Add(new RecordingEffect("a", 1f) { Log = log });
            NodeId b = graph.Add(new RecordingEffect("b", 1f) { Log = log });
            graph.Link(parent, a);
            graph.Link(parent, b);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0f);
            Assert.That(run.IsDone, Is.False, "자식이 도는 동안은 끝나지 않는다");

            run.Tick(1f);
            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start", "p:end", "a:end", "b:end" }));
        }

        [Test]
        public void OwningNode_DoesNotGetAutoChildPropagation()
        {
            // OwnsChildren이 true면 실행기가 자식을 건드리지 않는다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new OwningNoop());
            NodeId child = graph.Add(new RecordingEffect("c") { Log = log });
            graph.Link(parent, child);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries.Count, Is.EqualTo(0), "소유 노드의 자식을 실행기가 시작하면 안 된다");
        }

        [Test]
        public void MissingNode_CompletesImmediately()
        {
            // 노드 타입이 사라진 그래프를 열어도 나머지는 돌아야 한다.
            var graph = new FakeGraph();
            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), new NodeId(99));

            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
        }

        [Test]
        public void Cancel_StopsSelfAndChildren()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new RecordingEffect("p") { Log = log });
            NodeId child = graph.Add(new RecordingEffect("c", 1f) { Log = log });
            graph.Link(parent, child);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0f);
            run.Cancel();
            run.Tick(1f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start", "p:end", "c:start" }),
                "취소 후에는 자식이 끝나지 않는다");
        }

        [Test]
        public void TickAfterDone_DoesNothing()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId id = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), id);

            run.Tick(0f);
            run.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        private sealed class OwningNoop : MotionFlowNode
        {
            public override bool OwnsChildren => true;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter NodeRunTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionContext' could not be found`

- [ ] **Step 3: `MotionContext` 작성**

`Runtime/Core/Exec/MotionContext.cs`.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// <see cref="IMotionContext"/>의 기본 구현. 스코프 하나당 하나 만들어 그 스코프의
    /// 모든 노드가 공유한다.
    /// </summary>
    public sealed class MotionContext : IMotionContext
    {
        private readonly ISlotResolver _resolver;

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log)
        {
            Graph = graph;
            Scope = scope;
            _resolver = resolver;
            Log = log;
        }

        public IMotionGraphView Graph { get; }

        public IMotionScope Scope { get; }

        public IMotionLog Log { get; }

        public object ResolveSlot(SlotRef slot)
        {
            return _resolver == null ? null : _resolver.Resolve(slot);
        }
    }
}
```

- [ ] **Step 4: `NodeRun` 작성**

`Runtime/Core/Exec/NodeRun.cs`.

```csharp
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드 하나와 그 아래 전파를 굴리는 단위. 자기 핸들이 끝나면 자식들을
    /// <b>동시에</b> 시작하고, 자식이 전부 끝나야 자신도 끝난다.
    ///
    /// 자식 시작을 실행기가 맡는 덕분에 효과 노드가 <c>Punch → Fade</c>처럼
    /// 이어지는 배선을 스스로 구현하지 않아도 된다. 자식을 직접 조율하고 싶은 노드는
    /// <see cref="MotionNodeBase.OwnsChildren"/>을 true로 덮는다.
    /// </summary>
    public sealed class NodeRun
    {
        private readonly IMotionContext _ctx;
        private readonly NodeId _id;

        private IMotionHandle _self;
        private List<NodeRun> _children;
        private bool _started;
        private bool _selfDone;
        private bool _cancelled;

        public NodeRun(IMotionContext ctx, NodeId id)
        {
            _ctx = ctx;
            _id = id;
        }

        public bool IsDone { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (IsDone)
            {
                return;
            }

            if (!_started)
            {
                Start();
            }

            if (!_selfDone)
            {
                _self.Tick(deltaSeconds);

                if (!_self.IsDone)
                {
                    return;
                }

                _selfDone = true;
                SpawnChildren();

                // 자식을 방금 만들었다면 이번 프레임에 한 번 굴려 준다.
                // 안 그러면 즉시 끝나는 자식이 한 프레임씩 밀린다.
                if (_children != null)
                {
                    TickChildren(0f);
                }

                UpdateDone();
                return;
            }

            TickChildren(deltaSeconds);
            UpdateDone();
        }

        public void Cancel()
        {
            if (IsDone)
            {
                return;
            }

            _cancelled = true;

            if (_self != null)
            {
                _self.Cancel();
            }

            if (_children != null)
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    _children[i].Cancel();
                }
            }

            IsDone = true;
        }

        private void Start()
        {
            _started = true;

            MotionNodeBase node = _ctx.Graph.GetNode(_id);

            // 결손 노드 — 타입이 사라졌거나 id가 어긋났다. 건너뛰고 나머지를 살린다.
            if (node == null)
            {
                _self = MotionHandle.Completed;
                return;
            }

            _self = node.Play(_ctx);
        }

        private void SpawnChildren()
        {
            if (_cancelled)
            {
                return;
            }

            MotionNodeBase node = _ctx.Graph.GetNode(_id);

            // 자식을 직접 조율하는 노드는 실행기가 건드리지 않는다.
            if (node != null && node.OwnsChildren)
            {
                return;
            }

            IReadOnlyList<NodeId> childIds = _ctx.Graph.GetChildren(_id);
            if (childIds == null || childIds.Count == 0)
            {
                return;
            }

            _children = new List<NodeRun>(childIds.Count);
            for (int i = 0; i < childIds.Count; i++)
            {
                _children.Add(new NodeRun(_ctx, childIds[i]));
            }
        }

        private void TickChildren(float deltaSeconds)
        {
            if (_children == null)
            {
                return;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Tick(deltaSeconds);
            }
        }

        private void UpdateDone()
        {
            if (!_selfDone)
            {
                return;
            }

            if (_children == null)
            {
                IsDone = true;
                return;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                if (!_children[i].IsDone)
                {
                    return;
                }
            }

            IsDone = true;
        }
    }
}
```

- [ ] **Step 5: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter NodeRunTests
```

기대: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Core/Exec/MotionContext.cs Runtime/Core/Exec/NodeRun.cs Tests~/dotnet/NodeRunTests.cs
git commit -m "feat: NodeRun과 MotionContext 추가

노드 핸들이 끝나면 자식들을 동시에 시작하고, 자식이 전부 끝나야 자신도 끝난다.
그 덕에 효과 노드가 'A 다음 B' 배선을 스스로 구현하지 않아도 된다.
결손 노드는 건너뛰어 그래프의 나머지를 살린다."
```

---

## Task 12: 스코프에 노드 실행 붙이기

Task 10의 `MotionScope`에 진입 노드 실행을 더한다.

**Files:**
- Modify: `Runtime/Core/Exec/MotionScope.cs`
- Test: `Tests~/dotnet/MotionScopeExecutionTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionScopeExecutionTests
    {
        [Test]
        public void Begin_ThenTick_RunsEntryNode()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId entry = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
            Assert.That(scope.IsDone, Is.True);
            Assert.That(scope.IsCancelled, Is.False);
        }

        [Test]
        public void NaturalCompletion_DoesNotRunReverts()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a") { Log = log, RegisterRevert = true });

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }),
                "자연 완료 시 되돌리면 페이드인이 끝나자마자 투명해진다");
        }

        [Test]
        public void Cancel_MidFlight_RunsReverts()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a", 1f) { Log = log, RegisterRevert = true });

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            scope.Tick(0.5f);
            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:revert" }));
        }

        [Test]
        public void Completed_RaisedOnce_OnNaturalFinish()
        {
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a"));

            int raised = 0;
            var scope = new MotionScope("T");
            scope.Completed += () => raised++;

            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            scope.Tick(0f);
            scope.Tick(0f);

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void Begin_WithNoneEntry_CompletesImmediately()
        {
            var graph = new FakeGraph();
            var scope = new MotionScope("T");

            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), NodeId.None);

            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Tick_BeforeBegin_DoesNothing()
        {
            var scope = new MotionScope("T");
            Assert.DoesNotThrow(() => scope.Tick(1f));
            Assert.That(scope.IsDone, Is.False);
        }

        [Test]
        public void Cancel_StopsFurtherTicks()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a", 1f) { Log = log });

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            scope.Tick(0.1f);
            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionScopeExecutionTests
```

기대: 컴파일 실패 — `'MotionScope' does not contain a definition for 'Begin'`

- [ ] **Step 3: `MotionScope`에 실행 붙이기**

`Runtime/Core/Exec/MotionScope.cs`의 필드 선언 아래에 `_root`를 더하고, `Cancel` 앞에 `Begin`과 `Tick`을 넣는다.

필드 추가:

```csharp
        private NodeRun _root;
```

`Cancel()` 메서드 바로 위에 삽입:

```csharp
        /// <summary>
        /// 진입 노드부터 실행을 시작한다. 진입이 <see cref="NodeId.None"/>이면
        /// 할 일이 없는 것이므로 즉시 완료한다.
        /// </summary>
        public void Begin(IMotionContext ctx, NodeId entry)
        {
            if (_done || _root != null)
            {
                return;
            }

            if (!entry.IsValid)
            {
                CompleteNaturally();
                return;
            }

            _root = new NodeRun(ctx, entry);
        }

        /// <summary>시간을 진행시킨다. 실행 트리가 전부 끝나면 자연 완료로 표시한다.</summary>
        public void Tick(float deltaSeconds)
        {
            if (_done || _root == null)
            {
                return;
            }

            _root.Tick(deltaSeconds);

            if (_root.IsDone)
            {
                CompleteNaturally();
            }
        }
```

`Cancel()` 안에서 `_cancelled = true;` 다음 줄에 실행 트리 취소를 추가한다:

```csharp
            _cancelled = true;
            _done = true;

            if (_root != null)
            {
                _root.Cancel();
            }
```

- [ ] **Step 4: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter "MotionScope"
```

기대: `Passed! - Failed: 0, Passed: 16` (Task 10의 9개 + 이번 7개)

- [ ] **Step 5: 커밋**

```bash
git add Runtime/Core/Exec/MotionScope.cs Tests~/dotnet/MotionScopeExecutionTests.cs
git commit -m "feat: MotionScope에 진입 노드 실행 붙이기

스코프가 실행 트리를 소유하고, 트리가 끝나면 자연 완료로 표시한다.
취소는 트리를 끊은 뒤 등록된 복구를 역순으로 실행한다."
```

---

## Task 13: `SequenceNode`와 `ParallelNode`

**Files:**
- Create: `Runtime/Core/Nodes/SequenceNode.cs`
- Create: `Runtime/Core/Nodes/ParallelNode.cs`
- Test: `Tests~/dotnet/SequenceParallelTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SequenceParallelTests
    {
        private static MotionScope RunScope(FakeGraph graph, NodeId entry, params float[] ticks)
        {
            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);

            for (int i = 0; i < ticks.Length; i++)
            {
                scope.Tick(ticks[i]);
            }

            return scope;
        }

        [Test]
        public void Sequence_RunsChildrenInOrder_OneAtATime()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            graph.Link(seq, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("b", 1f) { Log = log }));

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), seq);

            scope.Tick(0f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }), "b가 아직 시작되면 안 된다");

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "b:start" }));

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "b:start", "b:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Sequence_WithNoChildren_CompletesImmediately()
        {
            var graph = new FakeGraph();
            NodeId seq = graph.Add(new SequenceNode());

            MotionScope scope = RunScope(graph, seq, 0f);

            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Sequence_InstantChildren_AllFinishInOneTick()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            graph.Link(seq, graph.Add(new RecordingEffect("a") { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("b") { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("c") { Log = log }));

            MotionScope scope = RunScope(graph, seq, 0f);

            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "b:start", "b:end", "c:start", "c:end"
            }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Sequence_Cancel_StopsCurrentChild()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            graph.Link(seq, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("b", 1f) { Log = log }));

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), seq);
            scope.Tick(0.5f);
            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }), "b는 영영 시작되지 않는다");
        }

        [Test]
        public void Parallel_StartsAllChildrenAtOnce()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId par = graph.Add(new ParallelNode());
            graph.Link(par, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(par, graph.Add(new RecordingEffect("b", 2f) { Log = log }));

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), par);

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "b:start", "a:end" }));
            Assert.That(scope.IsDone, Is.False, "가장 긴 자식이 끝나야 완료다");

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Parallel_WithNoChildren_CompletesImmediately()
        {
            var graph = new FakeGraph();
            NodeId par = graph.Add(new ParallelNode());

            MotionScope scope = RunScope(graph, par, 0f);

            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Parallel_ChildrenCanHaveTheirOwnChildren()
        {
            // 자식의 자식은 NodeRun이 이어 붙인다. Parallel이 관여하지 않는다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId par = graph.Add(new ParallelNode());
            NodeId a = graph.Add(new RecordingEffect("a") { Log = log });
            NodeId aChild = graph.Add(new RecordingEffect("a2") { Log = log });
            graph.Link(par, a);
            graph.Link(a, aChild);

            MotionScope scope = RunScope(graph, par, 0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "a2:start", "a2:end" }));
            Assert.That(scope.IsDone, Is.True);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter SequenceParallelTests
```

기대: 컴파일 실패 — `The type or namespace name 'SequenceNode' could not be found`

- [ ] **Step 3: `SequenceNode` 작성**

`Runtime/Core/Nodes/SequenceNode.cs`.

```csharp
using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식을 <b>차례로</b> 실행한다. 앞의 자식이 완전히 끝나야 다음이 시작된다.
    /// </summary>
    [Serializable]
    public sealed class SequenceNode : MotionFlowNode
    {
        public override bool OwnsChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            IReadOnlyList<NodeId> children = ctx.Graph.GetChildren(Id);
            if (children == null || children.Count == 0)
            {
                return MotionHandle.Completed;
            }

            return new SequenceHandle(ctx, children);
        }

        private sealed class SequenceHandle : IMotionHandle
        {
            private readonly IMotionContext _ctx;
            private readonly IReadOnlyList<NodeId> _children;

            private NodeRun _current;
            private int _index;
            private bool _done;

            public SequenceHandle(IMotionContext ctx, IReadOnlyList<NodeId> children)
            {
                _ctx = ctx;
                _children = children;
                _index = 0;
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                float remaining = deltaSeconds;

                // 즉시 끝나는 자식이 여럿 이어질 수 있다. 한 프레임에 다 소화한다 —
                // 안 그러면 자식 하나당 한 프레임씩 밀린다.
                while (!_done)
                {
                    if (_current == null)
                    {
                        if (_index >= _children.Count)
                        {
                            _done = true;
                            return;
                        }

                        _current = new NodeRun(_ctx, _children[_index]);
                        _index++;
                    }

                    _current.Tick(remaining);
                    remaining = 0f;

                    if (!_current.IsDone)
                    {
                        return;
                    }

                    _current = null;
                }
            }

            public void Cancel()
            {
                if (_done)
                {
                    return;
                }

                if (_current != null)
                {
                    _current.Cancel();
                    _current = null;
                }

                _done = true;
            }
        }
    }
}
```

- [ ] **Step 4: `ParallelNode` 작성**

`Runtime/Core/Nodes/ParallelNode.cs`.

```csharp
using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식을 <b>동시에</b> 시작하고, 전부 끝나야 완료한다.
    /// </summary>
    [Serializable]
    public sealed class ParallelNode : MotionFlowNode
    {
        public override bool OwnsChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            IReadOnlyList<NodeId> children = ctx.Graph.GetChildren(Id);
            if (children == null || children.Count == 0)
            {
                return MotionHandle.Completed;
            }

            return new ParallelHandle(ctx, children);
        }

        private sealed class ParallelHandle : IMotionHandle
        {
            private readonly List<NodeRun> _runs;
            private bool _done;

            public ParallelHandle(IMotionContext ctx, IReadOnlyList<NodeId> children)
            {
                _runs = new List<NodeRun>(children.Count);
                for (int i = 0; i < children.Count; i++)
                {
                    _runs.Add(new NodeRun(ctx, children[i]));
                }
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                bool allDone = true;
                for (int i = 0; i < _runs.Count; i++)
                {
                    _runs[i].Tick(deltaSeconds);

                    if (!_runs[i].IsDone)
                    {
                        allDone = false;
                    }
                }

                _done = allDone;
            }

            public void Cancel()
            {
                if (_done)
                {
                    return;
                }

                for (int i = 0; i < _runs.Count; i++)
                {
                    _runs[i].Cancel();
                }

                _done = true;
            }
        }
    }
}
```

- [ ] **Step 5: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter SequenceParallelTests
```

기대: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Core/Nodes/SequenceNode.cs Runtime/Core/Nodes/ParallelNode.cs Tests~/dotnet/SequenceParallelTests.cs
git commit -m "feat: Sequence와 Parallel 흐름 노드 추가

둘 다 OwnsChildren으로 자식 실행을 직접 조율한다.
Sequence는 한 프레임 안에서 즉시 끝나는 자식들을 연달아 소화해
자식 하나당 한 프레임씩 밀리는 것을 막는다."
```

---

## Task 14: `DelayNode`와 `RepeatNode`

**Files:**
- Create: `Runtime/Core/Nodes/DelayNode.cs`
- Create: `Runtime/Core/Nodes/RepeatNode.cs`
- Test: `Tests~/dotnet/DelayRepeatTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class DelayRepeatTests
    {
        private static MotionScope Begin(FakeGraph graph, NodeId entry)
        {
            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            return scope;
        }

        [Test]
        public void Delay_HoldsChildrenUntilElapsed()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId delay = graph.Add(new DelayNode { Seconds = 1f });
            graph.Link(delay, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, delay);

            scope.Tick(0.5f);
            Assert.That(log.Entries.Count, Is.EqualTo(0), "지연 중에는 자식이 시작되지 않는다");

            scope.Tick(0.5f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Delay_DoesNotOwnChildren()
        {
            // 자식 전파는 NodeRun에 맡긴다. Delay는 기다리기만 한다.
            Assert.That(new DelayNode().OwnsChildren, Is.False);
        }

        [Test]
        public void Delay_ZeroSeconds_PassesThroughImmediately()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId delay = graph.Add(new DelayNode { Seconds = 0f });
            graph.Link(delay, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, delay);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void Repeat_RunsChildrenGivenNumberOfTimes()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = 3 });
            graph.Link(repeat, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "a:start", "a:end", "a:start", "a:end"
            }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_Infinite_NeverCompletes()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);

            for (int i = 0; i < 10; i++)
            {
                scope.Tick(1f);
            }

            Assert.That(scope.IsDone, Is.False, "무한 반복은 스스로 끝나지 않는다");
            Assert.That(log.Entries.Count, Is.GreaterThanOrEqualTo(20));
        }

        [Test]
        public void Repeat_Infinite_StopsOnCancel()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(1f);
            int before = log.Entries.Count;

            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries.Count, Is.EqualTo(before));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_CarriesOvershootIntoNextCycle()
        {
            // 사이클마다 남은 시간을 버리면 반복 연출이 조금씩 느려진다.
            // 1초짜리 자식을 2.5초 진행하면 두 번 끝나고 세 번째가 0.5초 진행돼 있어야 한다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(2.5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:end", "a:end" }));
        }

        [Test]
        public void Repeat_ZeroCount_CompletesWithoutRunning()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = 0 });
            graph.Link(repeat, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(0f);

            Assert.That(log.Entries.Count, Is.EqualTo(0));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_WithNoChildren_CompletesImmediately()
        {
            var graph = new FakeGraph();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True, "돌릴 자식이 없으면 무한 반복도 끝난다");
        }
    }
}
```

`Repeat_CarriesOvershootIntoNextCycle`이 `a:start`를 기대하지 않는 이유는 `RecordingEffect`가 지속시간이 있을 때 시작을 기록하지 않고 종료만 기록하기 때문이다(Task 9의 `FromTimer` 경로).

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter DelayRepeatTests
```

기대: 컴파일 실패 — `The type or namespace name 'DelayNode' could not be found`

- [ ] **Step 3: `DelayNode` 작성**

`Runtime/Core/Nodes/DelayNode.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 정해진 시간을 기다린 뒤 자식으로 넘어간다.
    ///
    /// 자식을 소유하지 않는다 — 기다림이 끝나면 실행기가 알아서 자식을 잇는다.
    /// </summary>
    [Serializable]
    public sealed class DelayNode : MotionFlowNode
    {
        public float Seconds;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (Seconds <= 0f)
            {
                return MotionHandle.Completed;
            }

            return MotionHandle.FromTimer(Seconds, null);
        }
    }
}
```

- [ ] **Step 4: `RepeatNode` 작성**

`Runtime/Core/Nodes/RepeatNode.cs`.

```csharp
using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식들을 정해진 횟수만큼, 또는 무한히 반복한다. 한 사이클은 자식 전부가
    /// 끝나야 완료된다(<c>Parallel</c>과 같은 묶음 단위).
    /// </summary>
    [Serializable]
    public sealed class RepeatNode : MotionFlowNode
    {
        /// <summary><see cref="Count"/>에 넣으면 끝나지 않는다.</summary>
        public const int Infinite = -1;

        public int Count = Infinite;

        public override bool OwnsChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            IReadOnlyList<NodeId> children = ctx.Graph.GetChildren(Id);
            if (children == null || children.Count == 0 || Count == 0)
            {
                return MotionHandle.Completed;
            }

            return new RepeatHandle(ctx, children, Count);
        }

        private sealed class RepeatHandle : IMotionHandle
        {
            // 한 번의 Tick 안에서 도는 사이클 수의 상한. 지속시간 0짜리 자식을
            // 무한 반복으로 걸면 여기서 멈추지 않으면 프레임이 영원히 끝나지 않는다.
            private const int MaxCyclesPerTick = 64;

            private readonly IMotionContext _ctx;
            private readonly IReadOnlyList<NodeId> _children;
            private readonly int _totalCycles;

            private List<NodeRun> _runs;
            private int _completedCycles;
            private bool _done;

            public RepeatHandle(IMotionContext ctx, IReadOnlyList<NodeId> children, int totalCycles)
            {
                _ctx = ctx;
                _children = children;
                _totalCycles = totalCycles;
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                float remaining = deltaSeconds;

                for (int guard = 0; guard < MaxCyclesPerTick; guard++)
                {
                    if (_runs == null)
                    {
                        StartCycle();
                    }

                    bool allDone = true;
                    for (int i = 0; i < _runs.Count; i++)
                    {
                        _runs[i].Tick(remaining);

                        if (!_runs[i].IsDone)
                        {
                            allDone = false;
                        }
                    }

                    // 남은 시간은 첫 사이클에서만 쓴다. 이후 사이클은 0으로 시작해
                    // 같은 프레임에 여러 번 도는 것을 막는다 — 단, 즉시 끝나는 자식은
                    // 0으로도 끝나므로 아래 guard가 상한을 건다.
                    remaining = 0f;

                    if (!allDone)
                    {
                        return;
                    }

                    _runs = null;
                    _completedCycles++;

                    if (_totalCycles != Infinite && _completedCycles >= _totalCycles)
                    {
                        _done = true;
                        return;
                    }
                }
            }

            public void Cancel()
            {
                if (_done)
                {
                    return;
                }

                if (_runs != null)
                {
                    for (int i = 0; i < _runs.Count; i++)
                    {
                        _runs[i].Cancel();
                    }

                    _runs = null;
                }

                _done = true;
            }

            private void StartCycle()
            {
                _runs = new List<NodeRun>(_children.Count);
                for (int i = 0; i < _children.Count; i++)
                {
                    _runs.Add(new NodeRun(_ctx, _children[i]));
                }
            }
        }
    }
}
```

- [ ] **Step 5: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter DelayRepeatTests
```

기대: `Passed! - Failed: 0, Passed: 9`

`Repeat_CarriesOvershootIntoNextCycle`이 실패하면 `MotionTimer.Overshoot`을 사이클 사이로 넘기는 처리를 다시 본다. 현재 구현은 `NodeRun` 안에서 타이머가 오버슈트를 흡수하므로 `a:end`가 두 번 나온다.

- [ ] **Step 6: 커밋**

```bash
git add Runtime/Core/Nodes/DelayNode.cs Runtime/Core/Nodes/RepeatNode.cs Tests~/dotnet/DelayRepeatTests.cs
git commit -m "feat: Delay와 Repeat 흐름 노드 추가

Delay는 자식을 소유하지 않고 기다리기만 한다.
Repeat은 한 Tick 안의 사이클 수에 상한을 걸어, 지속시간 0짜리 자식을
무한 반복으로 걸어도 프레임이 멈추지 않게 한다."
```

---

## Task 15: `OnceLogger` — 경고 1회

**Files:**
- Create: `Runtime/Core/Diag/OnceLogger.cs`
- Test: `Tests~/dotnet/OnceLoggerTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class OnceLoggerTests
    {
        [Test]
        public void SameKey_WarnsOnlyOnce()
        {
            // 매 프레임 도는 연출이 매 프레임 경고하면 콘솔이 죽는다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            for (int i = 0; i < 100; i++)
            {
                logger.WarnOnce("slot:Icon", "slot not bound: Icon");
            }

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
            Assert.That(sink.Warnings[0], Is.EqualTo("slot not bound: Icon"));
        }

        [Test]
        public void DifferentKeys_WarnSeparately()
        {
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.WarnOnce("slot:Icon", "a");
            logger.WarnOnce("slot:Title", "b");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Reset_AllowsWarningAgain()
        {
            // 플레이어가 다시 활성화되면 다시 경고할 기회를 준다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.WarnOnce("k", "m");
            logger.Reset();
            logger.WarnOnce("k", "m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Errors_PassThroughEveryTime()
        {
            // 오류는 억제하지 않는다. 놓치면 안 되는 것이다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.Error("boom");
            logger.Error("boom");

            Assert.That(sink.Errors.Count, Is.EqualTo(2));
        }

        [Test]
        public void NullSink_DoesNotThrow()
        {
            var logger = new OnceLogger(null);

            Assert.DoesNotThrow(() => logger.WarnOnce("k", "m"));
            Assert.DoesNotThrow(() => logger.Warn("m"));
            Assert.DoesNotThrow(() => logger.Error("m"));
        }

        [Test]
        public void ImplementsIMotionLog()
        {
            // 실행기는 IMotionLog만 안다. OnceLogger를 그대로 꽂을 수 있어야 한다.
            var sink = new FakeLog();
            IMotionLog log = new OnceLogger(sink);

            log.Warn("m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter OnceLoggerTests
```

기대: 컴파일 실패 — `The type or namespace name 'OnceLogger' could not be found`

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Core/Diag/OnceLogger.cs`.

```csharp
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 같은 키의 경고를 한 번만 통과시키는 로그 래퍼.
    ///
    /// 방치형 게임이라 연출이 몇 시간씩 매 프레임 돈다. 미할당 슬롯 하나가
    /// 콘솔을 수십만 줄로 덮으면 진짜 문제를 찾을 수 없다.
    /// <b>오류는 억제하지 않는다</b> — 놓치면 안 되는 것이기 때문이다.
    /// </summary>
    public sealed class OnceLogger : IMotionLog
    {
        private readonly IMotionLog _sink;
        private readonly HashSet<string> _seen = new HashSet<string>();

        public OnceLogger(IMotionLog sink)
        {
            _sink = sink;
        }

        /// <summary><paramref name="key"/>가 처음일 때만 경고한다.</summary>
        public void WarnOnce(string key, string message)
        {
            if (!_seen.Add(key))
            {
                return;
            }

            Warn(message);
        }

        /// <summary>억제 없이 그대로 흘려보낸다.</summary>
        public void Warn(string message)
        {
            if (_sink != null)
            {
                _sink.Warn(message);
            }
        }

        public void Error(string message)
        {
            if (_sink != null)
            {
                _sink.Error(message);
            }
        }

        /// <summary>억제 기록을 비운다. 플레이어가 다시 활성화될 때 부른다.</summary>
        public void Reset()
        {
            _seen.Clear();
        }
    }
}
```

- [ ] **Step 4: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter OnceLoggerTests
```

기대: `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 5: 커밋**

```bash
git add Runtime/Core/Diag/OnceLogger.cs Tests~/dotnet/OnceLoggerTests.cs
git commit -m "feat: 같은 경고를 한 번만 내보내는 OnceLogger 추가

방치형이라 연출이 몇 시간씩 매 프레임 돈다. 미할당 슬롯 하나가
콘솔을 덮으면 진짜 문제를 찾을 수 없다. 오류는 억제하지 않는다."
```

---

## Task 16: `TriggerRunner` — 재발사 정책

**Files:**
- Create: `Runtime/Core/Exec/TriggerRunner.cs`
- Test: `Tests~/dotnet/TriggerRunnerTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class TriggerRunnerTests
    {
        private FakeGraph _graph;
        private ExecutionLog _log;
        private NodeId _entry;

        [SetUp]
        public void SetUp()
        {
            _graph = new FakeGraph();
            _log = new ExecutionLog();
            _entry = _graph.Add(new RecordingEffect("fx", 1f) { Log = _log });
        }

        private TriggerRunner Make(TriggerPolicy policy)
        {
            return new TriggerRunner("T", policy, () =>
            {
                var scope = new MotionScope("T");
                scope.Begin(new MotionContext(_graph, scope, new FakeSlotResolver(), new FakeLog()), _entry);
                return scope;
            });
        }

        [Test]
        public void Fire_StartsPlaying()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.Fire();

            Assert.That(runner.IsPlaying, Is.True);
        }

        [Test]
        public void Restart_CutsCurrentAndStartsOver()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.Fire();
            runner.Tick(0.9f);
            runner.Fire();
            runner.Tick(0.5f);

            Assert.That(runner.IsPlaying, Is.True, "새로 시작했으므로 아직 돌고 있다");
            Assert.That(_log.Entries.Count, Is.EqualTo(0), "끊긴 연출은 완료를 기록하지 않는다");
        }

        [Test]
        public void Ignore_DropsFireWhilePlaying()
        {
            TriggerRunner runner = Make(TriggerPolicy.Ignore);
            runner.Fire();
            runner.Tick(0.9f);
            runner.Fire();
            runner.Tick(0.1f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:end" }), "첫 발사가 그대로 끝난다");
            Assert.That(runner.IsPlaying, Is.False);
        }

        [Test]
        public void Queue_RunsAgainAfterCurrentFinishes()
        {
            TriggerRunner runner = Make(TriggerPolicy.Queue);
            runner.Fire();
            runner.Fire();

            runner.Tick(1f);
            Assert.That(runner.IsPlaying, Is.True, "대기 중이던 것이 이어서 시작된다");

            runner.Tick(1f);
            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:end", "fx:end" }));
            Assert.That(runner.IsPlaying, Is.False);
        }

        [Test]
        public void Queue_HoldsAtMostOne()
        {
            // 큐가 무한히 쌓이면 연타 한 번에 연출이 수십 번 돈다.
            TriggerRunner runner = Make(TriggerPolicy.Queue);
            runner.Fire();
            runner.Fire();
            runner.Fire();
            runner.Fire();

            runner.Tick(1f);
            runner.Tick(1f);

            Assert.That(_log.Entries.Count, Is.EqualTo(2), "대기는 하나만 유지한다");
        }

        [Test]
        public void Stop_CancelsAndClearsQueue()
        {
            TriggerRunner runner = Make(TriggerPolicy.Queue);
            runner.Fire();
            runner.Fire();
            runner.Stop();
            runner.Tick(5f);

            Assert.That(runner.IsPlaying, Is.False);
            Assert.That(_log.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void CompletedNaturally_RaisedOnNaturalFinishOnly()
        {
            var raised = new List<string>();

            TriggerRunner natural = Make(TriggerPolicy.Restart);
            natural.CompletedNaturally += () => raised.Add("natural");
            natural.Fire();
            natural.Tick(1f);

            TriggerRunner cancelled = Make(TriggerPolicy.Restart);
            cancelled.CompletedNaturally += () => raised.Add("cancelled");
            cancelled.Fire();
            cancelled.Tick(0.5f);
            cancelled.Stop();

            Assert.That(raised, Is.EqualTo(new[] { "natural" }));
        }

        [Test]
        public void Tick_WhenIdle_DoesNothing()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            Assert.DoesNotThrow(() => runner.Tick(1f));
            Assert.That(runner.IsPlaying, Is.False);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter TriggerRunnerTests
```

기대: 컴파일 실패 — `The type or namespace name 'TriggerRunner' could not be found`

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Core/Exec/TriggerRunner.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거 하나의 재생 상태. 한 번에 스코프 하나만 들고, 재발사를 정책대로 처리한다.
    ///
    /// 스코프를 만드는 일은 <see cref="MotionRuntime"/>이 넘겨준 팩토리가 한다 —
    /// 그래서 이 클래스는 그래프도 슬롯도 모른다.
    /// </summary>
    public sealed class TriggerRunner
    {
        private readonly Func<MotionScope> _scopeFactory;
        private readonly TriggerPolicy _policy;

        private MotionScope _current;
        private bool _queued;

        public TriggerRunner(string name, TriggerPolicy policy, Func<MotionScope> scopeFactory)
        {
            Name = name;
            _policy = policy;
            _scopeFactory = scopeFactory;
        }

        public string Name { get; }

        public bool IsPlaying => _current != null && !_current.IsDone;

        /// <summary>자연 완료했을 때만 발생한다. 취소로 끝난 경우에는 발생하지 않는다.</summary>
        public event Action CompletedNaturally;

        public void Fire()
        {
            if (!IsPlaying)
            {
                StartNew();
                return;
            }

            switch (_policy)
            {
                case TriggerPolicy.Ignore:
                    return;

                case TriggerPolicy.Queue:
                    // 대기는 하나만 유지한다. 무한히 쌓이면 연타 한 번에 연출이 수십 번 돈다.
                    _queued = true;
                    return;

                default:
                    _current.Cancel();
                    _current = null;
                    StartNew();
                    return;
            }
        }

        public void Stop()
        {
            _queued = false;

            if (_current != null)
            {
                _current.Cancel();
                _current = null;
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_current == null)
            {
                return;
            }

            _current.Tick(deltaSeconds);

            if (!_current.IsDone)
            {
                return;
            }

            bool wasCancelled = _current.IsCancelled;
            _current = null;

            if (!wasCancelled)
            {
                Action handler = CompletedNaturally;
                if (handler != null)
                {
                    handler();
                }
            }

            if (_queued)
            {
                _queued = false;
                StartNew();
            }
        }

        private void StartNew()
        {
            _current = _scopeFactory();

            // 팩토리가 진입 노드 없이 만든 스코프는 이미 끝나 있다.
            if (_current != null && _current.IsDone)
            {
                bool wasCancelled = _current.IsCancelled;
                _current = null;

                if (!wasCancelled)
                {
                    Action handler = CompletedNaturally;
                    if (handler != null)
                    {
                        handler();
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter TriggerRunnerTests
```

기대: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 5: 커밋**

```bash
git add Runtime/Core/Exec/TriggerRunner.cs Tests~/dotnet/TriggerRunnerTests.cs
git commit -m "feat: 트리거 재발사 정책 추가

Restart는 끊고 다시, Ignore는 버리고, Queue는 하나만 대기시킨다.
큐를 하나로 제한해 연타가 연출 수십 번으로 번지지 않게 한다.
CompletedNaturally는 취소로 끝난 경우 발생하지 않는다 - Start 완료 시
Loop를 자동 발사하는 규칙이 취소에도 걸리면 안 되기 때문이다."
```

---

## Task 17: `MotionRuntime` — 위상 규약

트리거 발사 창구를 만들고 `Start`/`Loop`/`End` 규약을 넣는다.

**Files:**
- Create: `Runtime/Core/Exec/ITriggerSink.cs`
- Create: `Runtime/Core/Nodes/TriggerNode.cs`
- Create: `Runtime/Core/Nodes/StopTriggerNode.cs`
- Create: `Runtime/Core/Exec/MotionRuntime.cs`
- Modify: `Runtime/Core/Authoring/IMotionContext.cs`
- Modify: `Runtime/Core/Exec/MotionContext.cs`
- Test: `Tests~/dotnet/MotionRuntimeTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionRuntimeTests
    {
        private FakeGraph _graph;
        private ExecutionLog _log;
        private FakeLog _sink;

        [SetUp]
        public void SetUp()
        {
            _graph = new FakeGraph();
            _log = new ExecutionLog();
            _sink = new FakeLog();
        }

        private MotionRuntime Make()
        {
            return new MotionRuntime(_graph, new FakeSlotResolver(), _sink);
        }

        private NodeId DeclareTrigger(string name, string effectName, float duration = 0f,
            TriggerPolicy policy = TriggerPolicy.Restart)
        {
            NodeId entry = _graph.Add(new RecordingEffect(effectName, duration) { Log = _log });
            _graph.DeclareTrigger(name, entry, policy);
            return entry;
        }

        [Test]
        public void Fire_RunsDeclaredTrigger()
        {
            DeclareTrigger("Click", "click");
            MotionRuntime runtime = Make();

            runtime.Fire("Click");
            runtime.Tick(0f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "click:start", "click:end" }));
        }

        [Test]
        public void Fire_UnknownTrigger_WarnsOnceAndIgnores()
        {
            MotionRuntime runtime = Make();

            runtime.Fire("Nope");
            runtime.Fire("Nope");
            runtime.Tick(0f);

            Assert.That(_sink.Warnings.Count, Is.EqualTo(1), "같은 오타를 매번 경고하지 않는다");
        }

        [Test]
        public void StartCompletion_AutoFiresLoop()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start");
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.StartTrigger);
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);
        }

        [Test]
        public void StartCompletion_DoesNotFireLoop_WhenAutoLoopDisabled()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start");
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            MotionRuntime runtime = Make();
            runtime.AutoLoopAfterStart = false;

            runtime.Fire(MotionRuntime.StartTrigger);
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
        }

        [Test]
        public void StartCancelled_DoesNotFireLoop()
        {
            DeclareTrigger(MotionRuntime.StartTrigger, "start", 1f);
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.StartTrigger);
            runtime.Tick(0.5f);
            runtime.Stop(MotionRuntime.StartTrigger);
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
        }

        [Test]
        public void FireEnd_StopsLoop()
        {
            DeclareTrigger(MotionRuntime.LoopTrigger, "loop", 1f);
            DeclareTrigger(MotionRuntime.EndTrigger, "end", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire(MotionRuntime.LoopTrigger);
            runtime.Tick(0.5f);
            runtime.Fire(MotionRuntime.EndTrigger);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
            Assert.That(runtime.IsPlaying(MotionRuntime.EndTrigger), Is.True);
        }

        [Test]
        public void StopAll_StopsEverything()
        {
            DeclareTrigger("A", "a", 1f);
            DeclareTrigger("B", "b", 1f);
            MotionRuntime runtime = Make();

            runtime.Fire("A");
            runtime.Fire("B");
            runtime.StopAll();

            Assert.That(runtime.IsPlaying("A"), Is.False);
            Assert.That(runtime.IsPlaying("B"), Is.False);
        }

        [Test]
        public void TriggersRunIndependently()
        {
            DeclareTrigger("A", "a", 1f);
            DeclareTrigger("B", "b", 2f);
            MotionRuntime runtime = Make();

            runtime.Fire("A");
            runtime.Fire("B");
            runtime.Tick(1f);

            Assert.That(runtime.IsPlaying("A"), Is.False);
            Assert.That(runtime.IsPlaying("B"), Is.True);
        }

        [Test]
        public void WaitFor_InvokesOnNaturalCompletion()
        {
            DeclareTrigger("A", "a", 1f);
            MotionRuntime runtime = Make();

            int called = 0;
            runtime.Fire("A");
            runtime.WaitFor("A", () => called++);

            runtime.Tick(0.5f);
            Assert.That(called, Is.EqualTo(0));

            runtime.Tick(0.5f);
            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void WaitFor_InvokesImmediately_WhenNotPlaying()
        {
            // 브릿지가 Fire("End") 뒤에 WaitFor를 부른다. End 트리거가 없는 그래프에서도
            // 콜백이 와야 프리젠터가 영영 닫히지 않는 일이 없다.
            MotionRuntime runtime = Make();

            int called = 0;
            runtime.WaitFor("Missing", () => called++);

            Assert.That(called, Is.EqualTo(1));
        }

        [Test]
        public void WaitFor_InvokesImmediately_WhenCancelled()
        {
            DeclareTrigger("A", "a", 1f);
            MotionRuntime runtime = Make();

            int called = 0;
            runtime.Fire("A");
            runtime.WaitFor("A", () => called++);
            runtime.Stop("A");
            runtime.Tick(0f);

            Assert.That(called, Is.EqualTo(1), "취소로 끝나도 대기자를 풀어 줘야 한다");
        }

        [Test]
        public void StopTriggerNode_StopsAnotherTrigger()
        {
            NodeId loopEntry = _graph.Add(new RecordingEffect("loop", 5f) { Log = _log });
            _graph.DeclareTrigger(MotionRuntime.LoopTrigger, loopEntry);

            NodeId stopEntry = _graph.Add(new StopTriggerNode { TriggerName = MotionRuntime.LoopTrigger });
            _graph.DeclareTrigger("Kill", stopEntry);

            MotionRuntime runtime = Make();
            runtime.Fire(MotionRuntime.LoopTrigger);
            runtime.Tick(0.1f);
            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);

            runtime.Fire("Kill");
            runtime.Tick(0f);

            Assert.That(runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
        }

        [Test]
        public void TriggerNode_JustPropagatesToChildren()
        {
            NodeId entry = _graph.Add(new TriggerNode { TriggerName = "Click" });
            _graph.Link(entry, _graph.Add(new RecordingEffect("fx") { Log = _log }));
            _graph.DeclareTrigger("Click", entry);

            MotionRuntime runtime = Make();
            runtime.Fire("Click");
            runtime.Tick(0f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:start", "fx:end" }));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionRuntimeTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionRuntime' could not be found`

- [ ] **Step 3: `ITriggerSink` 작성**

`Runtime/Core/Exec/ITriggerSink.cs`.

노드가 다른 트리거를 건드릴 수 있어야 한다(`StopTriggerNode`). 실행기 전체를 노출하는 대신 필요한 두 동작만 뚫는다.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 다른 트리거를 건드리는 통로. <see cref="MotionRuntime"/> 전체를 노출하지 않고
    /// 필요한 두 동작만 뚫는다.
    /// </summary>
    public interface ITriggerSink
    {
        void Fire(string trigger);

        void Stop(string trigger);
    }
}
```

- [ ] **Step 4: `IMotionContext`와 `MotionContext`에 트리거 통로 추가**

`Runtime/Core/Authoring/IMotionContext.cs`의 `object ResolveSlot(SlotRef slot);` 위에 추가:

```csharp
        /// <summary>다른 트리거를 건드리는 통로. 실행기 밖에서 만든 문맥에서는 null일 수 있다.</summary>
        ITriggerSink Triggers { get; }
```

`Runtime/Core/Exec/MotionContext.cs`를 아래로 교체한다. 기존 4인자 생성자는 테스트가 쓰고 있으므로 남긴다.

```csharp
namespace Juahn.UiMotion
{
    /// <summary>
    /// <see cref="IMotionContext"/>의 기본 구현. 스코프 하나당 하나 만들어 그 스코프의
    /// 모든 노드가 공유한다.
    /// </summary>
    public sealed class MotionContext : IMotionContext
    {
        private readonly ISlotResolver _resolver;

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log)
            : this(graph, scope, resolver, log, null)
        {
        }

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers)
        {
            Graph = graph;
            Scope = scope;
            _resolver = resolver;
            Log = log;
            Triggers = triggers;
        }

        public IMotionGraphView Graph { get; }

        public IMotionScope Scope { get; }

        public IMotionLog Log { get; }

        public ITriggerSink Triggers { get; }

        public object ResolveSlot(SlotRef slot)
        {
            return _resolver == null ? null : _resolver.Resolve(slot);
        }
    }
}
```

- [ ] **Step 5: `TriggerNode`와 `StopTriggerNode` 작성**

`Runtime/Core/Nodes/TriggerNode.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거의 진입 표식. 아무 일도 하지 않고 자식으로 흘려보낸다.
    ///
    /// 실행에는 필요 없지만 그래프 창에서 "여기가 이 트리거의 시작"을 보여 주는
    /// 앵커 역할을 한다. 그래서 실행 의미가 아니라 <b>편집 의미</b>를 갖는 노드다.
    /// </summary>
    [Serializable]
    public sealed class TriggerNode : MotionFlowNode
    {
        public string TriggerName;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            return MotionHandle.Completed;
        }
    }
}
```

`Runtime/Core/Nodes/StopTriggerNode.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 다른 트리거를 멈춘다. 상시 도는 강조 연출을 특정 시점에 끄는 데 쓴다.
    /// </summary>
    [Serializable]
    public sealed class StopTriggerNode : MotionFlowNode
    {
        public string TriggerName;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (ctx.Triggers != null && !string.IsNullOrEmpty(TriggerName))
            {
                ctx.Triggers.Stop(TriggerName);
            }

            return MotionHandle.Completed;
        }
    }
}
```

- [ ] **Step 6: `MotionRuntime` 작성**

`Runtime/Core/Exec/MotionRuntime.cs`.

```csharp
using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 트리거 발사 창구이자 위상 규약의 주인. 그래프 하나 + 슬롯 해석기 하나에 대해
    /// 하나 만든다(플레이어당 하나).
    /// </summary>
    public sealed class MotionRuntime : ITriggerSink
    {
        public const string StartTrigger = "Start";
        public const string LoopTrigger = "Loop";
        public const string EndTrigger = "End";

        private readonly IMotionGraphView _graph;
        private readonly ISlotResolver _resolver;
        private readonly OnceLogger _log;
        private readonly Dictionary<string, TriggerRunner> _runners = new Dictionary<string, TriggerRunner>();
        private readonly List<TriggerRunner> _order = new List<TriggerRunner>();
        private readonly Dictionary<string, List<Action>> _waiters = new Dictionary<string, List<Action>>();

        public MotionRuntime(IMotionGraphView graph, ISlotResolver resolver, IMotionLog log)
        {
            _graph = graph;
            _resolver = resolver;
            _log = new OnceLogger(log);

            BuildRunners();
        }

        /// <summary><see cref="StartTrigger"/>가 자연 완료하면 <see cref="LoopTrigger"/>를 자동 발사할지.</summary>
        public bool AutoLoopAfterStart { get; set; } = true;

        public void Fire(string trigger)
        {
            TriggerRunner runner;
            if (!_runners.TryGetValue(trigger, out runner))
            {
                _log.WarnOnce("trigger:" + trigger,
                    "graph '" + _graph.GraphName + "' has no trigger '" + trigger + "'");
                ReleaseWaiters(trigger);
                return;
            }

            // End는 유지 연출을 끊고 들어간다. 닫히는 중에 계속 떠다니면 안 된다.
            if (trigger == EndTrigger)
            {
                Stop(LoopTrigger);
            }

            runner.Fire();
        }

        public void Stop(string trigger)
        {
            TriggerRunner runner;
            if (_runners.TryGetValue(trigger, out runner))
            {
                runner.Stop();
                ReleaseWaiters(trigger);
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].Stop();
                ReleaseWaiters(_order[i].Name);
            }
        }

        public bool IsPlaying(string trigger)
        {
            TriggerRunner runner;
            return _runners.TryGetValue(trigger, out runner) && runner.IsPlaying;
        }

        /// <summary>
        /// 트리거가 끝나면 <paramref name="onCompleted"/>를 부른다. 자연 완료든 취소든 부른다 —
        /// 대기자를 영영 붙잡아 두면 팝업이 닫히지 않는다.
        ///
        /// 지금 재생 중이 아니면 <b>즉시</b> 부른다.
        /// </summary>
        public void WaitFor(string trigger, Action onCompleted)
        {
            if (onCompleted == null)
            {
                return;
            }

            if (!IsPlaying(trigger))
            {
                onCompleted();
                return;
            }

            List<Action> list;
            if (!_waiters.TryGetValue(trigger, out list))
            {
                list = new List<Action>();
                _waiters[trigger] = list;
            }

            list.Add(onCompleted);
        }

        public void Tick(float deltaSeconds)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                _order[i].Tick(deltaSeconds);
            }
        }

        /// <summary>경고 억제 기록을 비운다. 플레이어가 다시 활성화될 때 부른다.</summary>
        public void ResetDiagnostics()
        {
            _log.Reset();
        }

        private void BuildRunners()
        {
            IReadOnlyList<TriggerDeclaration> triggers = _graph.Triggers;
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null || string.IsNullOrEmpty(decl.Name) || _runners.ContainsKey(decl.Name))
                {
                    continue;
                }

                string name = decl.Name;
                NodeId entry = decl.Entry;

                var runner = new TriggerRunner(name, decl.Policy, () => CreateScope(name, entry));
                runner.CompletedNaturally += () => OnRunnerCompleted(name);

                _runners[name] = runner;
                _order.Add(runner);
            }
        }

        private MotionScope CreateScope(string triggerName, NodeId entry)
        {
            var scope = new MotionScope(triggerName, _log);
            scope.Begin(new MotionContext(_graph, scope, _resolver, _log, this), entry);
            return scope;
        }

        private void OnRunnerCompleted(string triggerName)
        {
            ReleaseWaiters(triggerName);

            if (triggerName != StartTrigger || !AutoLoopAfterStart)
            {
                return;
            }

            if (_runners.ContainsKey(LoopTrigger))
            {
                Fire(LoopTrigger);
            }
        }

        private void ReleaseWaiters(string triggerName)
        {
            List<Action> list;
            if (!_waiters.TryGetValue(triggerName, out list))
            {
                return;
            }

            _waiters.Remove(triggerName);

            for (int i = 0; i < list.Count; i++)
            {
                list[i]();
            }
        }
    }
}
```

- [ ] **Step 7: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionRuntimeTests
```

기대: `Passed! - Failed: 0, Passed: 13`

- [ ] **Step 8: 전체 테스트 실행**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj
```

기대: 실패 0.

- [ ] **Step 9: 커밋**

```bash
git add Runtime/Core Tests~/dotnet/MotionRuntimeTests.cs
git commit -m "feat: MotionRuntime과 위상 규약 추가

Start가 자연 완료하면 Loop를 자동 발사하고, End는 Loop를 끊고 들어간다.
WaitFor는 취소로 끝나도 대기자를 풀어 준다 - 붙잡아 두면 팝업이 영영 닫히지 않는다.
없는 트리거를 발사하면 경고 한 번 뒤 무시하고 대기자를 즉시 풀어 준다."
```

---

## Task 18: 서브그래프와 순환 검출

**Files:**
- Create: `Runtime/Core/Nodes/SubGraphNode.cs`
- Create: `Runtime/Core/Exec/GraphCycleDetector.cs`
- Modify: `Runtime/Core/Authoring/IMotionContext.cs`
- Modify: `Runtime/Core/Exec/MotionContext.cs`
- Test: `Tests~/dotnet/SubGraphTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>테스트용 서브그래프 노드. Unity 계층은 MotionGraph 에셋을 들고 같은 일을 한다.</summary>
    public sealed class FakeSubGraphNode : SubGraphNode
    {
        public IMotionGraphView Target;

        protected override IMotionGraphView ResolveGraph()
        {
            return Target;
        }
    }

    [TestFixture]
    public sealed class SubGraphTests
    {
        private static MotionScope Begin(FakeGraph graph, NodeId entry, FakeLog sink = null)
        {
            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), sink ?? new FakeLog()), entry);
            return scope;
        }

        [Test]
        public void SubGraph_RunsInnerGraphEntry()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph { GraphName = "inner" };
            NodeId innerEntry = inner.Add(new RecordingEffect("in") { Log = log });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph { GraphName = "outer" };
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:start", "in:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void SubGraph_CompletesOnlyAfterInnerFinishes()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in", 1f) { Log = log });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0.5f);
            Assert.That(scope.IsDone, Is.False);

            scope.Tick(0.5f);
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void SubGraph_PropagatesToItsOwnChildren_AfterInnerFinishes()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in", 1f) { Log = log });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });
            outer.Link(sub, outer.Add(new RecordingEffect("after") { Log = log }));

            MotionScope scope = Begin(outer, sub);
            scope.Tick(1f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:end", "after:start", "after:end" }));
        }

        [Test]
        public void SubGraph_WithNoTarget_SkipsAndWarns()
        {
            var sink = new FakeLog();
            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = null });

            MotionScope scope = Begin(outer, sub, sink);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True);
            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void SubGraph_Cancel_StopsInnerGraph()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in", 1f) { Log = log, RegisterRevert = true });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0.5f);
            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:revert" }),
                "바깥 스코프를 취소하면 안쪽 그래프의 복구도 실행돼야 한다");
        }

        [Test]
        public void SubGraph_DepthLimit_StopsRuntimeRecursion()
        {
            // 에디터가 순환을 막지만, 손으로 만든 에셋이 새어 들어올 수 있다.
            // 런타임이 스택 오버플로로 죽지 않고 경고 후 멈춰야 한다.
            var sink = new FakeLog();
            var graph = new FakeGraph { GraphName = "self" };
            var node = new FakeSubGraphNode();
            NodeId sub = graph.Add(node);
            graph.DeclareTrigger(MotionRuntime.StartTrigger, sub);
            node.Target = graph;

            MotionScope scope = Begin(graph, sub, sink);

            Assert.DoesNotThrow(() => scope.Tick(0f));
            Assert.That(sink.Warnings.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void CycleDetector_FindsDirectSelfReference()
        {
            var graph = new FakeGraph { GraphName = "self" };
            var node = new FakeSubGraphNode();
            graph.Add(node);
            node.Target = graph;

            Assert.That(GraphCycleDetector.HasCycle(graph), Is.True);
        }

        [Test]
        public void CycleDetector_FindsIndirectCycle()
        {
            var a = new FakeGraph { GraphName = "a" };
            var b = new FakeGraph { GraphName = "b" };

            var toB = new FakeSubGraphNode { Target = b };
            a.Add(toB);

            var toA = new FakeSubGraphNode { Target = a };
            b.Add(toA);

            Assert.That(GraphCycleDetector.HasCycle(a), Is.True);
        }

        [Test]
        public void CycleDetector_AllowsDiamond()
        {
            // 같은 서브그래프를 두 곳에서 쓰는 것은 순환이 아니다. 그게 재사용의 목적이다.
            var shared = new FakeGraph { GraphName = "shared" };
            shared.Add(new RecordingEffect("s"));

            var root = new FakeGraph { GraphName = "root" };
            root.Add(new FakeSubGraphNode { Target = shared });
            root.Add(new FakeSubGraphNode { Target = shared });

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        [Test]
        public void CycleDetector_IgnoresUnresolvedSubGraphs()
        {
            var root = new FakeGraph();
            root.Add(new FakeSubGraphNode { Target = null });

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter SubGraphTests
```

기대: 컴파일 실패 — `The type or namespace name 'SubGraphNode' could not be found`

- [ ] **Step 3: 문맥에 중첩 깊이 추가**

`Runtime/Core/Authoring/IMotionContext.cs`의 `ITriggerSink Triggers { get; }` 아래에 추가:

```csharp
        /// <summary>서브그래프 중첩 깊이. 루트는 0이다. 런타임 재귀 방어에 쓴다.</summary>
        int Depth { get; }
```

`Runtime/Core/Exec/MotionContext.cs`에서 5인자 생성자에 `depth`를 더하고 프로퍼티를 노출한다. 기존 두 생성자는 `depth: 0`으로 위임한다.

기존 5인자 생성자를 아래로 교체:

```csharp
        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers)
            : this(graph, scope, resolver, log, triggers, 0)
        {
        }

        public MotionContext(IMotionGraphView graph, MotionScope scope, ISlotResolver resolver, IMotionLog log,
            ITriggerSink triggers, int depth)
        {
            Graph = graph;
            Scope = scope;
            _resolver = resolver;
            Log = log;
            Triggers = triggers;
            Depth = depth;
        }
```

그리고 프로퍼티를 추가:

```csharp
        public int Depth { get; }
```

- [ ] **Step 4: `SubGraphNode` 작성**

`Runtime/Core/Nodes/SubGraphNode.cs`.

`abstract`인 이유는 코어가 그래프 에셋을 직접 참조할 수 없기 때문이다. Unity 계층이 `MotionGraph` 필드를 든 파생을 제공하고, 테스트는 가짜를 든 파생을 쓴다.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 다른 그래프를 노드 하나처럼 실행한다. 자주 쓰는 관용구를 한 곳에서 고치기 위한 재사용 단위.
    ///
    /// <b>추상 클래스다.</b> 코어는 그래프 에셋을 참조할 수 없으므로 대상 해석을 파생에 맡긴다 —
    /// Unity 계층이 <c>MotionGraph</c> 필드를 든 파생을 제공한다.
    /// </summary>
    [Serializable]
    public abstract class SubGraphNode : MotionFlowNode
    {
        /// <summary>한 번의 Tick에서 허용하는 최대 중첩 깊이. 손으로 만든 순환 에셋에 대한 방어.</summary>
        public const int MaxDepth = 8;

        /// <summary>안쪽 그래프에서 발사할 트리거.</summary>
        public string EntryTrigger = MotionRuntime.StartTrigger;

        /// <summary>대상 그래프. 없으면 null.</summary>
        protected abstract IMotionGraphView ResolveGraph();

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (ctx.Depth >= MaxDepth)
            {
                Warn(ctx, "sub-graph nesting exceeded " + MaxDepth + " levels; possible cycle");
                return MotionHandle.Skipped;
            }

            IMotionGraphView inner = ResolveGraph();
            if (inner == null)
            {
                Warn(ctx, "sub-graph node #" + Id.Value + " has no target graph");
                return MotionHandle.Skipped;
            }

            NodeId entry = inner.GetEntry(EntryTrigger);
            if (!entry.IsValid)
            {
                Warn(ctx, "sub-graph '" + inner.GraphName + "' has no trigger '" + EntryTrigger + "'");
                return MotionHandle.Skipped;
            }

            var innerScope = new MotionScope(EntryTrigger, ctx.Log);

            // 바깥 스코프가 취소되면 안쪽도 취소돼야 한다. 안 그러면 안쪽 복구가 영영 실행되지 않는다.
            ctx.Scope.Remember(innerScope.Cancel);

            innerScope.Begin(
                new MotionContext(inner, innerScope, new ContextSlotResolver(ctx), ctx.Log, ctx.Triggers, ctx.Depth + 1),
                entry);

            return new SubGraphHandle(innerScope);
        }

        private static void Warn(IMotionContext ctx, string message)
        {
            if (ctx.Log != null)
            {
                ctx.Log.Warn(message);
            }
        }

        /// <summary>슬롯은 바깥과 공유한다 — 같은 플레이어의 같은 계층이기 때문이다.</summary>
        private sealed class ContextSlotResolver : ISlotResolver
        {
            private readonly IMotionContext _outer;

            public ContextSlotResolver(IMotionContext outer)
            {
                _outer = outer;
            }

            public object Resolve(SlotRef slot)
            {
                return _outer.ResolveSlot(slot);
            }
        }

        private sealed class SubGraphHandle : IMotionHandle
        {
            private readonly MotionScope _inner;

            public SubGraphHandle(MotionScope inner)
            {
                _inner = inner;
            }

            public bool IsDone => _inner.IsDone;

            public void Tick(float deltaSeconds)
            {
                _inner.Tick(deltaSeconds);
            }

            public void Cancel()
            {
                _inner.Cancel();
            }
        }
    }
}
```

- [ ] **Step 5: `GraphCycleDetector` 작성**

`Runtime/Core/Exec/GraphCycleDetector.cs`.

```csharp
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 서브그래프 참조에 순환이 있는지 검사한다. 에디터가 저장 전에 부른다.
    ///
    /// 같은 서브그래프를 여러 곳에서 쓰는 것(다이아몬드)은 순환이 아니다 —
    /// 그게 재사용의 목적이다. 방문 중인 <b>경로</b>에 다시 나타날 때만 순환이다.
    /// </summary>
    public static class GraphCycleDetector
    {
        public static bool HasCycle(IMotionGraphView root)
        {
            if (root == null)
            {
                return false;
            }

            var onPath = new List<IMotionGraphView>();
            return Visit(root, onPath);
        }

        private static bool Visit(IMotionGraphView graph, List<IMotionGraphView> onPath)
        {
            for (int i = 0; i < onPath.Count; i++)
            {
                if (ReferenceEquals(onPath[i], graph))
                {
                    return true;
                }
            }

            onPath.Add(graph);

            foreach (IMotionGraphView child in EnumerateSubGraphs(graph))
            {
                if (Visit(child, onPath))
                {
                    return true;
                }
            }

            onPath.RemoveAt(onPath.Count - 1);
            return false;
        }

        private static IEnumerable<IMotionGraphView> EnumerateSubGraphs(IMotionGraphView graph)
        {
            // 노드 id는 1부터 순서대로다. null이 나오면 끝이다.
            for (int i = 1; ; i++)
            {
                MotionNodeBase node = graph.GetNode(new NodeId(i));
                if (node == null)
                {
                    yield break;
                }

                var sub = node as SubGraphNode;
                if (sub == null)
                {
                    continue;
                }

                IMotionGraphView target = sub.PeekGraph();
                if (target != null)
                {
                    yield return target;
                }
            }
        }
    }
}
```

`GraphCycleDetector`가 대상 그래프를 볼 수 있도록 `SubGraphNode`에 공개 창구를 더한다. `ResolveGraph` 아래에 추가:

```csharp
        /// <summary>검사 도구가 대상 그래프를 들여다보는 창구.</summary>
        public IMotionGraphView PeekGraph()
        {
            return ResolveGraph();
        }
```

- [ ] **Step 6: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter SubGraphTests
```

기대: `Passed! - Failed: 0, Passed: 10`

- [ ] **Step 7: 전체 테스트 실행**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj
```

기대: 실패 0.

- [ ] **Step 8: 커밋**

```bash
git add Runtime/Core Tests~/dotnet/SubGraphTests.cs
git commit -m "feat: 서브그래프 노드와 순환 검출 추가

SubGraphNode는 추상이다 - 코어가 그래프 에셋을 참조할 수 없으므로
대상 해석을 Unity 계층 파생에 맡긴다.
바깥 스코프 취소가 안쪽 스코프까지 끊도록 복구에 등록한다.
에디터 검사와 별개로 런타임 깊이 제한을 둬 손으로 만든 순환에도 죽지 않는다."
```

---

## Task 19: 오서링 어트리뷰트

노드 추가 계약의 "설명"과 "작동 예시" 부분이다. 에디터(계획 3)의 팔레트와 Node Doctor가 이 값을 읽는다.

**Files:**
- Create: `Runtime/Core/Authoring/MotionNodeAttribute.cs`
- Create: `Runtime/Core/Authoring/MotionSlotAttribute.cs`
- Create: `Runtime/Core/Authoring/MotionParamAttribute.cs`
- Test: `Tests~/dotnet/MotionAttributeTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System;
using System.Reflection;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionAttributeTests
    {
        [MotionNode(
            Name = "Test Node",
            Category = "Testing",
            Summary = "테스트용 노드다.",
            Sample = "TestNode")]
        private sealed class DocumentedNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))]
            public SlotRef Target = SlotRef.Self;

            [MotionParam(Label = "세기", Tooltip = "클수록 크게 움직인다.")]
            public float Amplitude = 1f;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }

        private sealed class UndocumentedNode : MotionEffectNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }

        [Test]
        public void MotionNodeAttribute_IsReadableByReflection()
        {
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DocumentedNode), typeof(MotionNodeAttribute));

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.Name, Is.EqualTo("Test Node"));
            Assert.That(attr.Category, Is.EqualTo("Testing"));
            Assert.That(attr.Summary, Is.EqualTo("테스트용 노드다."));
            Assert.That(attr.Sample, Is.EqualTo("TestNode"));
        }

        [Test]
        public void DocumentedNode_IsVerified()
        {
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DocumentedNode), typeof(MotionNodeAttribute));

            Assert.That(attr.IsVerified, Is.True);
        }

        [Test]
        public void NodeWithoutSample_IsNotVerified()
        {
            var attr = new MotionNodeAttribute { Name = "X", Category = "Y", Summary = "Z" };
            Assert.That(attr.IsVerified, Is.False, "작동 예시가 없으면 미검증이다");
        }

        [Test]
        public void NodeWithoutSummary_IsNotVerified()
        {
            var attr = new MotionNodeAttribute { Name = "X", Category = "Y", Sample = "S" };
            Assert.That(attr.IsVerified, Is.False, "설명이 없으면 미검증이다");
        }

        [Test]
        public void UndocumentedNode_HasNoAttribute()
        {
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(UndocumentedNode), typeof(MotionNodeAttribute));

            Assert.That(attr, Is.Null);
        }

        [Test]
        public void MotionSlotAttribute_CarriesRequiredType()
        {
            FieldInfo field = typeof(DocumentedNode).GetField("Target");
            var attr = (MotionSlotAttribute)Attribute.GetCustomAttribute(field, typeof(MotionSlotAttribute));

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void MotionParamAttribute_CarriesLabelAndTooltip()
        {
            FieldInfo field = typeof(DocumentedNode).GetField("Amplitude");
            var attr = (MotionParamAttribute)Attribute.GetCustomAttribute(field, typeof(MotionParamAttribute));

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.Label, Is.EqualTo("세기"));
            Assert.That(attr.Tooltip, Is.EqualTo("클수록 크게 움직인다."));
        }

        [Test]
        public void MotionNodeAttribute_IsNotInherited()
        {
            // 파생 노드가 부모의 설명을 물려받으면 Node Doctor가 거짓 통과를 낸다.
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DerivedNode), typeof(MotionNodeAttribute), false);

            Assert.That(attr, Is.Null);
        }

        private sealed class DerivedNode : MotionEffectNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionAttributeTests
```

기대: 컴파일 실패 — `The type or namespace name 'MotionNodeAttribute' could not be found`

- [ ] **Step 3: `MotionNodeAttribute` 작성**

`Runtime/Core/Authoring/MotionNodeAttribute.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드의 이름·분류·설명·작동 예시를 선언한다. 에디터의 노드 팔레트와 Node Doctor가 읽는다.
    ///
    /// 설명을 별도 문서가 아니라 여기에 두는 이유는 코드 옆에 있어야 썩지 않고,
    /// 툴이 기계적으로 검사할 수 있기 때문이다.
    ///
    /// <see cref="Inherited"/>가 false인 이유 — 파생 노드가 부모의 설명을 물려받으면
    /// Node Doctor가 거짓 통과를 낸다. 각 노드는 자기 설명을 스스로 달아야 한다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MotionNodeAttribute : Attribute
    {
        /// <summary>팔레트에 뜨는 이름. 예: "Scale Punch".</summary>
        public string Name { get; set; }

        /// <summary>팔레트 분류. 예: "Transform".</summary>
        public string Category { get; set; }

        /// <summary>이 노드가 무엇을 하고 언제 쓰는지 한두 문장.</summary>
        public string Summary { get; set; }

        /// <summary>
        /// 작동 예시 그래프의 이름. <c>Samples~/Nodes/&lt;Sample&gt;.motiongraph</c>를 가리킨다.
        /// 팔레트에서 이 노드에 호버하면 그 그래프가 그 자리에서 재생된다.
        /// </summary>
        public string Sample { get; set; }

        /// <summary>
        /// 설명과 작동 예시를 모두 갖췄는가. 아니면 팔레트의 "미검증" 섹션으로 격리되고
        /// CI 게이트에서 실패한다. 쓰는 것 자체를 막지는 않는다 — 실험을 막지 않기 위해서다.
        /// </summary>
        public bool IsVerified =>
            !string.IsNullOrWhiteSpace(Summary) && !string.IsNullOrWhiteSpace(Sample);
    }
}
```

- [ ] **Step 4: `MotionSlotAttribute`와 `MotionParamAttribute` 작성**

`Runtime/Core/Authoring/MotionSlotAttribute.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// <see cref="SlotRef"/> 필드가 요구하는 타입을 선언한다. 인스펙터가 이 타입으로
    /// 드래그 대상을 걸러 준다.
    ///
    /// 코어는 이 값을 검사하지 않고 나르기만 한다 — Unity 타입을 알 수 없기 때문이다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class MotionSlotAttribute : Attribute
    {
        public MotionSlotAttribute(Type requiredType)
        {
            RequiredType = requiredType;
        }

        public Type RequiredType { get; }
    }
}
```

`Runtime/Core/Authoring/MotionParamAttribute.cs`.

```csharp
using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 파라미터 필드의 표시 정보. UnityEngine의 <c>[Tooltip]</c>·<c>[Range]</c>를 코어에서
    /// 쓸 수 없으므로 이것이 대신한다. 그래프 창의 노드 인스펙터가 읽는다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class MotionParamAttribute : Attribute
    {
        /// <summary>인스펙터에 뜨는 이름. 비우면 필드 이름을 쓴다.</summary>
        public string Label { get; set; }

        public string Tooltip { get; set; }

        /// <summary>숫자 필드의 슬라이더 하한. <see cref="Max"/>와 함께 지정할 때만 슬라이더가 뜬다.</summary>
        public float Min { get; set; }

        public float Max { get; set; }

        public bool HasRange => Max > Min;
    }
}
```

- [ ] **Step 5: 테스트 실행해 통과 확인**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter MotionAttributeTests
```

기대: `Passed! - Failed: 0, Passed: 8`

- [ ] **Step 6: 기존 노드에 어트리뷰트 붙이기**

지금까지 만든 흐름 노드 6개에 설명을 단다. 각 파일의 클래스 선언 위에 붙인다.

`SequenceNode.cs`:
```csharp
    [MotionNode(Name = "Sequence", Category = "Flow",
        Summary = "자식을 차례로 실행한다. 앞의 자식이 완전히 끝나야 다음이 시작된다.",
        Sample = "Sequence")]
```

`ParallelNode.cs`:
```csharp
    [MotionNode(Name = "Parallel", Category = "Flow",
        Summary = "자식을 동시에 시작하고 전부 끝나야 완료한다.",
        Sample = "Parallel")]
```

`DelayNode.cs`:
```csharp
    [MotionNode(Name = "Delay", Category = "Flow",
        Summary = "정해진 시간을 기다린 뒤 자식으로 넘어간다.",
        Sample = "Delay")]
```

`RepeatNode.cs`:
```csharp
    [MotionNode(Name = "Repeat", Category = "Flow",
        Summary = "자식을 정해진 횟수만큼, 또는 무한히 반복한다. Count에 -1을 넣으면 무한이다.",
        Sample = "Repeat")]
```

`TriggerNode.cs`:
```csharp
    [MotionNode(Name = "Trigger", Category = "Flow",
        Summary = "트리거의 진입 표식. 실행에는 관여하지 않고 자식으로 흘려보낸다.",
        Sample = "Trigger")]
```

`StopTriggerNode.cs`:
```csharp
    [MotionNode(Name = "Stop Trigger", Category = "Flow",
        Summary = "다른 트리거를 멈춘다. 상시 도는 강조 연출을 특정 시점에 끌 때 쓴다.",
        Sample = "StopTrigger")]
```

`SubGraphNode`는 추상이라 붙이지 않는다 — Unity 계층의 파생이 붙인다.

샘플 그래프 에셋 자체는 `.motiongraph` 형식이 필요하므로 계획 2에서 만든다. 그때까지 이 노드들은 Node Doctor에서 "예시 없음"으로 뜬다.

- [ ] **Step 7: 전체 테스트 실행**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj
```

기대: 실패 0.

- [ ] **Step 8: 커밋**

```bash
git add Runtime/Core Tests~/dotnet/MotionAttributeTests.cs
git commit -m "feat: 노드 오서링 어트리뷰트 추가

설명과 작동 예시를 코드 옆에 두어 썩지 않게 하고 툴이 기계적으로 검사할 수 있게 한다.
MotionNodeAttribute는 상속되지 않는다 - 파생이 부모 설명을 물려받으면
Node Doctor가 거짓 통과를 낸다.
MotionParam이 UnityEngine의 Tooltip/Range를 대신해 코어의 무의존을 지킨다."
```

---

## Task 20: 통합 스모크와 문서

코어 전체가 실제 시나리오에서 맞물리는지 확인하고, 패키지 문서를 채운다.

**Files:**
- Create: `Tests~/dotnet/PopupScenarioTests.cs`
- Create: `README.md`
- Create: `CHANGELOG.md`

- [ ] **Step 1: 실패하는 통합 테스트 작성**

스펙 2절이 예시로 든 팝업 연출을 그대로 조립해 돌린다. 단위 테스트가 전부 통과해도 조합이 틀릴 수 있다.

```csharp
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class PopupScenarioTests
    {
        private FakeGraph _graph;
        private ExecutionLog _log;
        private MotionRuntime _runtime;

        /// <summary>
        /// 스펙의 예시 연출을 조립한다.
        ///   Start: [Punch 0.2s, Fade 0.15s] 동시  →  Loop 자동
        ///   Loop:  Float 1s 무한 반복
        ///   End:   Shrink 0.15s
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _graph = new FakeGraph { GraphName = "PopupOpen" };
            _log = new ExecutionLog();

            NodeId startEntry = _graph.Add(new ParallelNode());
            _graph.Link(startEntry, _graph.Add(new RecordingEffect("punch", 0.2f) { Log = _log }));
            _graph.Link(startEntry, _graph.Add(new RecordingEffect("fade", 0.15f) { Log = _log }));
            _graph.DeclareTrigger(MotionRuntime.StartTrigger, startEntry);

            NodeId loopEntry = _graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            _graph.Link(loopEntry, _graph.Add(new RecordingEffect("float", 1f)
            {
                Log = _log,
                RegisterRevert = true
            }));
            _graph.DeclareTrigger(MotionRuntime.LoopTrigger, loopEntry);

            NodeId endEntry = _graph.Add(new RecordingEffect("shrink", 0.15f) { Log = _log });
            _graph.DeclareTrigger(MotionRuntime.EndTrigger, endEntry);

            _runtime = new MotionRuntime(_graph, new FakeSlotResolver(), new FakeLog());
        }

        [Test]
        public void OpenSequence_PlaysStartThenLoops()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);

            _runtime.Tick(0.15f);
            Assert.That(_log.Entries, Is.EqualTo(new[] { "fade:end" }));
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);

            _runtime.Tick(0.05f);
            Assert.That(_log.Entries, Is.EqualTo(new[] { "fade:end", "punch:end" }));
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True,
                "Start가 끝나면 Loop가 자동으로 이어진다");
        }

        [Test]
        public void LoopKeepsRunning_Indefinitely()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            for (int i = 0; i < 100; i++)
            {
                _runtime.Tick(1f);
            }

            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);
        }

        [Test]
        public void CloseSequence_StopsLoopAndRevertsIt()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);
            _runtime.Tick(0.5f);

            _runtime.Fire(MotionRuntime.EndTrigger);

            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
            Assert.That(_log.Entries, Does.Contain("float:revert"),
                "유지 연출이 끊기면 원래 위치로 되돌아가야 한다");
        }

        [Test]
        public void CloseSequence_HostCanWaitForEnd()
        {
            // 브릿지가 CloseTransitionTask로 나르는 흐름이다.
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            bool closed = false;
            _runtime.Fire(MotionRuntime.EndTrigger);
            _runtime.WaitFor(MotionRuntime.EndTrigger, () => closed = true);

            _runtime.Tick(0.1f);
            Assert.That(closed, Is.False, "닫힘 연출이 도는 동안은 기다린다");

            _runtime.Tick(0.05f);
            Assert.That(closed, Is.True);
            Assert.That(_log.Entries, Does.Contain("shrink:end"));
        }

        [Test]
        public void ReopenWhileClosing_RestartsCleanly()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);
            _runtime.Fire(MotionRuntime.EndTrigger);
            _runtime.Tick(0.05f);

            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);

            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.True);
        }

        [Test]
        public void StopAll_LeavesNothingRunning()
        {
            _runtime.Fire(MotionRuntime.StartTrigger);
            _runtime.Tick(0.2f);
            _runtime.Tick(0.5f);

            _runtime.StopAll();

            Assert.That(_runtime.IsPlaying(MotionRuntime.StartTrigger), Is.False);
            Assert.That(_runtime.IsPlaying(MotionRuntime.LoopTrigger), Is.False);
            Assert.That(_runtime.IsPlaying(MotionRuntime.EndTrigger), Is.False);
            Assert.That(_log.Entries, Does.Contain("float:revert"),
                "전부 멈추면 유지 연출의 복구도 실행된다");
        }
    }
}
```

- [ ] **Step 2: 테스트 실행**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --filter PopupScenarioTests
```

기대: `Passed! - Failed: 0, Passed: 6`

이 시점에서 실패가 나오면 조합에서만 드러나는 결함이다. 해당 단위 테스트로 내려가 재현 케이스를 먼저 추가한 뒤 고친다.

- [ ] **Step 3: `README.md` 작성**

```markdown
# UI Motion (com.juahn.v2.uimotion)

JuahnFrameworkV2 스택의 UI 연출 패키지. 노드 그래프로 UI 움직임을 조립하고,
그 결과를 에셋 하나로 저장해 어디에든 재사용한다.

## 어셈블리 두 개

| 어셈블리 | 의존 | 내용 |
|---|---|---|
| `juahn.v2.UiMotion.Core` | 없음 (`noEngineReferences`) | 그래프 실행 엔진 · 스코프 · 흐름 노드 · 이징 · 순환 검출 |
| `juahn.v2.UiMotion` | Core | ScriptableObject · MonoBehaviour · 트윈 러너 · 효과 노드 |

코어가 UnityEngine을 참조하지 않는 덕에 `dotnet test`로 Unity 없이 검증된다.
그 결과 취소 · 원상 복구 · 스코프 생명주기처럼 버그가 가장 많이 나는 부분이
모든 푸시마다 CI에서 검사된다.

## 위상

| 트리거 | 의미 |
|---|---|
| `Start` | 진입 시 1회. 자연 완료하면 `Loop`를 자동 발사한다 |
| `Loop` | 무한 반복 허용. `End`가 발사되면 자동 취소된다 |
| `End` | 완료될 때까지 호스트가 기다린다 |
| 임의 이름 | `Click` · `Denied` · `Reward` … 서로 독립 스코프로 동시에 돈다 |

## 설치

Unity Package Manager → Add package from git URL:

```
https://github.com/armadimon/com.juahn.v2.uimotion.git
```

## 코어 테스트 실행

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj
```

## 노드 추가하기

세 가지를 갖추면 어떤 노드든 추가할 수 있다.

1. `MotionEffectNode`(무언가를 움직인다) 또는 `MotionFlowNode`(실행 순서를 정한다)를 상속하고 `OnPlay`를 채운다
2. `[MotionNode]`로 이름 · 분류 · 설명을 단다
3. `Sample`이 가리키는 작동 예시 그래프를 `Samples~/Nodes/`에 둔다

설명이나 예시가 없으면 팔레트의 "미검증" 섹션으로 격리된다. 쓰는 것 자체는 막지 않지만
CI 게이트에서 실패한다 — 배포된 것은 100% 문서화됨을 보장하기 위해서다.

```csharp
[MotionNode(
    Name     = "Scale Punch",
    Category = "Transform",
    Summary  = "대상을 잠깐 부풀렸다 되돌린다. 획득·강조 순간에 쓴다.",
    Sample   = "ScalePunch")]
[Serializable]
public sealed class ScalePunchNode : MotionEffectNode
{
    [MotionSlot(typeof(RectTransform))]
    public SlotRef Target = SlotRef.Self;

    [MotionParam(Label = "세기", Min = 0f, Max = 1f)]
    public float Amplitude = 0.2f;

    public override bool Reverts => true;

    protected override IMotionHandle OnPlay(IMotionContext ctx)
    {
        // ...
    }
}
```

## 설계 문서

`docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`
```

- [ ] **Step 4: `CHANGELOG.md` 작성**

```markdown
# Changelog

## [0.1.0] - 미출시

### 추가

- 순수 C# 코어 실행 엔진 (`juahn.v2.UiMotion.Core`)
  - `IMotionGraphView` — 실행기가 그래프를 보는 유일한 창구
  - `MotionScope` — 트리거 1회 발사의 실행 단위. 취소 시 등록의 역순으로 원상 복구
  - `NodeRun` — 노드 하나와 자식 전파
  - `MotionRuntime` — 트리거 발사 창구와 `Start`/`Loop`/`End` 위상 규약
  - `TriggerRunner` — `Restart`/`Ignore`/`Queue` 재발사 정책
  - 흐름 노드 — `Trigger` · `Sequence` · `Parallel` · `Delay` · `Repeat` · `StopTrigger` · `SubGraph`
  - `GraphCycleDetector` — 서브그래프 순환 검출
  - `EaseLibrary` — 12종 이징
  - `OnceLogger` — 같은 경고를 한 번만
  - 오서링 어트리뷰트 — `[MotionNode]` · `[MotionSlot]` · `[MotionParam]`
- `dotnet test` 기반 CI 게이트. Unity 라이선스 없이 모든 푸시마다 코어를 검증한다
```

- [ ] **Step 5: 전체 검증**

```bash
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj
grep -rn "using UnityEngine" Runtime/Core/ ; echo "purity exit=$?"
node -e "JSON.parse(require('fs').readFileSync('package.json','utf8')); console.log('package.json OK')"
```

기대:
- 테스트 실패 0
- `purity exit=1` (grep이 못 찾음 = 정상)
- `package.json OK`

- [ ] **Step 6: 커밋**

```bash
git add Tests~/dotnet/PopupScenarioTests.cs README.md CHANGELOG.md
git commit -m "docs: README와 CHANGELOG 추가, 팝업 시나리오 통합 테스트 추가

스펙의 예시 연출(Start 병렬 → Loop 무한 → End 대기)을 그대로 조립해
단위 테스트가 놓치는 조합 결함을 잡는다."
```

- [ ] **Step 7: Unity 에디터로 한 번 열기**

`.meta` 파일을 생성하고 어셈블리 두 개가 Unity에서도 컴파일되는지 확인한다.
`dotnet`이 통과해도 Unity의 C# 버전이나 `noEngineReferences` 제약에서 깨질 수 있다.

1. 아무 Unity 6000.0 프로젝트의 `Packages/manifest.json`에 로컬 경로로 추가한다:
   ```json
   "com.juahn.v2.uimotion": "file:../../JuahnFrameworkV2/com.juahn.v2.uimotion"
   ```
2. 에디터를 열고 콘솔에 컴파일 오류가 없는지 본다.
3. 생성된 `.meta` 파일을 커밋한다:
   ```bash
   git add -A
   git commit -m "chore: Unity가 생성한 meta 파일 추가"
   ```

오류가 나면 대부분 C# 문법 버전 문제다. `Tests~/dotnet/UiMotion.Core.Tests.csproj`의
`LangVersion`이 `9.0`인지 확인한다.

---

## 완료 기준

- [ ] `dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj` 실패 0
- [ ] `grep -rn "using UnityEngine" Runtime/Core/`가 아무것도 찾지 못함
- [ ] Unity 에디터에서 컴파일 오류 없음
- [ ] GitHub Actions 워크플로가 통과
- [ ] 코드로 그래프를 조립해 `Start` → `Loop` → `End`가 도는 것을 통합 테스트가 증명

이 다섯 가지가 만족되면 계획 2(Unity 런타임 계층)로 넘어간다.
