# UI Motion — 어댑터 두 개 구현 계획 (계획 4)

> **에이전트 작업자에게:** REQUIRED SUB-SKILL — `superpowers:subagent-driven-development`로 태스크 단위로 실행한다. 스텝은 체크박스(`- [ ]`)다.

**목표:** 완성된 UI Motion을 기존 프레임워크와 유료 에셋에 붙인다. 코어의 "참조 0"을 깨지 않으면서.

**아키텍처:** 어댑터는 각각 독립 패키지이고 파일 두세 개다. 코어와 런타임 패키지는 이들을 전혀 모른다 — 의존은 한 방향으로만 흐른다.

**기술 스택:** Unity 6000.x · UniTask · DOTween · netstandard2.1 · C# 9.0

---

## 저장소가 둘 (모두 새로 만든다)

| 태스크 | 저장소 | 무엇 |
|---|---|---|
| 1 ~ 3 | `com.juahn.v2.uimotion.uiservice` | `MotionGraphFeature : PresenterFeatureBase, ITransitionFeature` |
| 4 ~ 7 | `com.juahn.v2.uimotion.dotween` | `DoTweenRunner : IMotionTweenRunner` |
| 8 | 두 저장소 + 런타임 | 문서와 스펙 정리 |

둘 다 `/Users/teamsparta/UnityProject/JuahnFrameworkV2/` 아래에 만든다.

## 실측으로 확인한 것 (전부 검증됨)

**컴파일 검증에 필요한 것이 전부 있다.** `2026Template` 프로젝트 하나가 두 어댑터의 참조 환경이 된다.

| 필요한 것 | 실제 경로 |
|---|---|
| `DOTween.dll` | `2026Template/Assets/Plugins/Demigiant/DOTween/DOTween.dll` |
| `UniTask.dll` | `2026Template/Library/ScriptAssemblies/UniTask.dll` |
| `juahn.UiService.dll` | `2026Template/Library/ScriptAssemblies/juahn.UiService.dll` |
| `UnityEngine.dll` (통짜 파사드) | Unity 설치의 매니지드 폴더. **DOTween이 이것을 요구한다** — `SetEase` 오버로드 중 하나가 `AnimationCurve`를 받는데 그 타입이 통짜 어셈블리에 있다 |

**DOTween의 API 표면**(실제로 컴파일해 확인함):

```csharp
Tweener t = DOVirtual.Float(0f, 1f, duration, v => onEased(v))
    .SetEase(Ease.OutCubic)
    .SetUpdate(true);            // true = unscaled
bool done = !t.IsActive() || t.IsComplete();
t.Kill(false);
```

`EaseKind` 13개가 전부 `DG.Tweening.Ease`에 대응한다 — `Linear` · `In/Out/InOutQuad` · `In/Out/InOutCubic` · `In/Out/InOutSine` · `OutBack` · `OutElastic` · `OutBounce`.

**UiPresenter의 생명주기**(`com.juahn.uiservice/Runtime/UiPresenter.cs` 실측):

```
UiService.Load  -> ui.Init()        -> InitializeFeatures() -> OnPresenterInitialized
UiService.OpenUi -> InternalOpenProcessAsync
  1. _openTransitionCompletion = new
  2. NotifyFeaturesOpening()          <- GameObject는 아직 비활성
  3. gameObject.SetActive(true)       <- MotionPlayer.OnEnable이 여기서 돈다
  4. OnOpened() / NotifyFeaturesOpened()
  5. await WaitForOpenTransitionsAsync()
  6. OnOpenTransitionCompleted()

InternalCloseProcessAsync
  1. _closeTransitionCompletion = new
  2. NotifyFeaturesClosing()          <- 여기서 End를 발사한다
  3. OnClosed() / NotifyFeaturesClosed()
  4. await WaitForCloseTransitionsAsync()   <- 오브젝트는 아직 활성
  5. gameObject.SetActive(false)
  6. 필요하면 파괴
```

## 이 계획이 확정하는 것

### 스펙 6절의 매핑은 틀렸다 — 고친 것을 쓴다

스펙은 `OnPresenterOpening()`에서 `Fire("Start")`라고 적었지만 그 시점에는 오브젝트가 **비활성**이다. 펌프에 등록되지 않아 아무도 틱하지 않고, 곧이어 `SetActive(true)`가 `MotionPlayer.OnEnable`을 돌려 `PlayOnEnable`이 `Start`를 한 번 더 발사한다.

| UiPresenter 생명주기 | 브릿지 |
|---|---|
| `OnPresenterInitialized` | `ClaimTriggerOwnership()` |
| `OnPresenterOpened` | 열기 완료원 생성 · `Fire("Start")` · `WaitFor("Start", 완료)` |
| `OnPresenterClosing` | 닫기 완료원 생성 · `Fire("End")` · `WaitFor("End", 완료)` |

**완료원은 프리젠터가 `await`하기 전에 만들어져야 한다.** `WaitForOpenTransitionsAsync`가 이미 완료된 태스크를 건너뛰기 때문이다. 기존 `AnimationDelayFeature`가 `OnPresenterOpened`에서 만드는 이유가 이것이다.

**순서가 어긋나도 안전하다.** 프리팹이 활성 상태로 인스턴스화돼 `OnEnable`이 `Init`보다 먼저 돌더라도, `ClaimTriggerOwnership()`이 이미 시작된 것을 걷어낸다(계획 2에서 그렇게 만들었다). 그 뒤 `OnPresenterOpened`가 제대로 발사한다.

### DOTween 백엔드는 에디터 프리뷰에서 쓰지 않는다

DOTween의 업데이트 루프는 런타임에 만들어지는 MonoBehaviour다. 에디트 모드에서는 돌지 않으므로, DOTween 핸들은 `IsDone`이 영원히 false가 되어 **인에디터 프리뷰가 멈춘 채로 걸린다.**

그래서 부트스트랩은 **플레이 모드에서만** 러너를 꽂는다. 에디터 프리뷰는 언제나 내장 러너를 쓴다. 이것은 제약이 아니라 올바른 동작이다 — 프리뷰의 목적은 타이밍 확인이고 두 러너는 같은 이징을 쓴다.

### DOTween이 없으면 어셈블리 자체가 컴파일되지 않는다

DOTween은 에셋스토어 플러그인이라 UPM 패키지가 아니다. `versionDefines`는 패키지 이름에 걸리므로 동작하지 않는다. 대신 `defineConstraints`를 쓰고 사용자가 스크립팅 심볼 하나를 추가한다. 명시적이고, 없으면 조용히 컴파일에서 빠진다.

## 검증 축

| 대상 | 방법 |
|---|---|
| 두 어댑터 | `Tools~/compile-check/run.sh` (로컬, DOTween·UniTask·UiService DLL 참조) |
| `.meta` 누락·GUID 중복 | CI |
| 동작 | Unity에서 손으로 (각 저장소의 `docs/unity-verification.md`) |

CI에서 컴파일할 수 없는 이유가 하나 더 있다 — DOTween은 유료 에셋이고 UiService의 컴파일 결과물은 프로젝트에만 있다.

## 절대 규칙

1. **어댑터는 코어를 바꾸지 않는다.** 코어에 손이 필요하면 그것은 설계가 틀렸다는 신호다. 멈추고 보고한다.
2. LINQ 금지. C# 9.0.
3. 코드·주석·문서에 이모지 금지. 주석은 한국어로 쓴다.
4. **`.meta` 파일을 처음부터 만든다.**
5. 커밋마다 `CHANGELOG.md`를 갱신한다.

---

## 저장소 1 — `com.juahn.v2.uimotion.uiservice`

### Task 1: 패키지 뼈대와 컴파일 게이트

**Files:** `package.json` · `.gitignore` · `README.md` · `CHANGELOG.md` · `LICENSE.md` · `Runtime/juahn.v2.UiMotion.UiService.asmdef` · `Runtime/MotionUiServiceLog.cs` · `Tools~/compile-check/{UiMotion.UiService.Compile.csproj,run.sh}` · `.github/workflows/ci.yml` · 전부의 `.meta`

- [ ] **Step 1: 저장소를 만든다**

```bash
cd /Users/teamsparta/UnityProject/JuahnFrameworkV2
mkdir com.juahn.v2.uimotion.uiservice && cd com.juahn.v2.uimotion.uiservice
git init && git checkout -b main
```

- [ ] **Step 2: `package.json`**

```json
{
  "name": "com.juahn.v2.uimotion.uiservice",
  "displayName": "UI Motion for UiService",
  "author": "armadimon",
  "version": "0.1.0",
  "unity": "6000.0",
  "license": "MIT",
  "type": "library",
  "description": "Bridge between UI Motion and juahn's UiService. Attach MotionGraphFeature next to a MotionPlayer on a UiPresenter and the presenter waits for the Start graph before it reports opened, and for the End graph before it deactivates. UiService must be installed by the consumer.",
  "dependencies": {
    "com.juahn.v2.uimotion": "0.1.0"
  }
}
```

`com.juahn.uiservice`를 의존성에 **넣지 않는다.** `com.juahn.v2.vcontainer`가 VContainer에 대해 그렇게 하는 것과 같은 이유다 — 소비자가 따로 설치하는 외부 패키지이고, 여기에 적으면 버전이 어긋났을 때 UPM이 해석에 실패한다. asmdef가 이름으로 참조한다.

- [ ] **Step 3: `.gitignore`**

`com.juahn.v2.uimotion.editor`의 것을 그대로 쓴다 (`Tools~/compile-check/*.csproj` 예외 포함).

- [ ] **Step 4: asmdef**

`Runtime/juahn.v2.UiMotion.UiService.asmdef`:

```json
{
    "name": "juahn.v2.UiMotion.UiService",
    "rootNamespace": "Juahn.UiMotion.UiService",
    "references": [
        "juahn.v2.UiMotion",
        "juahn.v2.UiMotion.Core",
        "juahn.UiService",
        "UniTask"
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

- [ ] **Step 5: 첫 파일 — 로그 어댑터**

게이트가 검증할 대상이 하나는 있어야 한다.

`Runtime/MotionUiServiceLog.cs`:

```csharp
using UnityEngine;

namespace Juahn.UiMotion.UiService
{
    /// <summary>
    /// 브릿지의 진단. 코어의 <see cref="IMotionLog"/>와 같은 이유로 접두어를 붙인다 —
    /// 콘솔에서 어디서 나온 경고인지 바로 알 수 있어야 한다.
    /// </summary>
    internal static class MotionUiServiceLog
    {
        public static void Warn(string message, Object context)
        {
            Debug.LogWarning("[UiMotion/UiService] " + message, context);
        }
    }
}
```

- [ ] **Step 6: 컴파일 게이트**

`Tools~/compile-check/UiMotion.UiService.Compile.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    브릿지를 Unity 에디터 없이 컴파일한다.

    런타임 패키지의 소스와, 소비자 프로젝트가 만든 UiService/UniTask 어셈블리를 참조한다.
    그 어셈블리들은 재배포할 수 없으므로 이 검사는 CI가 아니라 로컬 게이트다.

    UnityManaged / RuntimePackage / RefProject 경로는 run.sh가 넘긴다.
  -->

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>UiMotion.UiService.CompileCheck</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0649;CS0169;CS0414</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="$(RuntimePackage)/Runtime/Core/**/*.cs" LinkBase="Core" />
    <Compile Include="$(RuntimePackage)/Runtime/Unity/**/*.cs" LinkBase="Runtime" />
    <Compile Include="../../Runtime/**/*.cs" LinkBase="Bridge" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Unity.Scripting">
      <HintPath>$(UnityManaged)/Unity.Scripting.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(UnityManaged)/UnityEngine.CoreModule.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIModule">
      <HintPath>$(UnityManaged)/UnityEngine.UIModule.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.AnimationModule">
      <HintPath>$(UnityManaged)/UnityEngine.AnimationModule.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>$(UnityManaged)/UnityEngine.TextRenderingModule.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(UnityManaged)/UnityEngine.IMGUIModule.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UI">
      <HintPath>$(UnityUgui)</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UniTask">
      <HintPath>$(RefProject)/Library/ScriptAssemblies/UniTask.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="juahn.UiService">
      <HintPath>$(RefProject)/Library/ScriptAssemblies/juahn.UiService.dll</HintPath><Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

`Tools~/compile-check/run.sh` — 에디터 패키지의 것을 베끼되 참조 프로젝트를 하나 더 찾는다:

```bash
#!/usr/bin/env bash
#
# UiService 브릿지 컴파일 게이트.
#
#   ./Tools~/compile-check/run.sh
#   REF_PROJECT=/path/to/UnityProject ./Tools~/compile-check/run.sh
#
# REF_PROJECT는 UiService와 UniTask가 이미 컴파일돼 있는 Unity 프로젝트여야 한다.
# 그 어셈블리는 재배포할 수 없어 이 저장소에 넣을 수 없다.
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "${HERE}/../.." && pwd)"

if [ -z "${RUNTIME_PACKAGE:-}" ]; then
  RUNTIME_PACKAGE="$(cd "${PACKAGE_ROOT}/../com.juahn.v2.uimotion" 2>/dev/null && pwd || true)"
fi

if [ -z "${RUNTIME_PACKAGE}" ] || [ ! -d "${RUNTIME_PACKAGE}/Runtime/Core" ]; then
  echo "런타임 패키지를 찾지 못했습니다. RUNTIME_PACKAGE를 지정하세요." >&2
  exit 2
fi

# --- UiService와 UniTask가 컴파일된 프로젝트를 찾는다 -------------------
if [ -z "${REF_PROJECT:-}" ]; then
  for candidate in "${PACKAGE_ROOT}"/../../*/ ; do
    if [ -f "${candidate}Library/ScriptAssemblies/juahn.UiService.dll" ] \
       && [ -f "${candidate}Library/ScriptAssemblies/UniTask.dll" ]; then
      REF_PROJECT="$(cd "${candidate}" && pwd)"
      break
    fi
  done
fi

if [ -z "${REF_PROJECT:-}" ]; then
  echo "UiService와 UniTask가 컴파일된 Unity 프로젝트를 찾지 못했습니다." >&2
  echo "그 프로젝트를 한 번 열어 컴파일한 뒤 REF_PROJECT로 지정하세요." >&2
  echo "  예: REF_PROJECT=/Users/me/UnityProject/2026Template $0" >&2
  exit 2
fi

# --- Unity 설치 ---------------------------------------------------------
if [ -z "${UNITY_ROOT:-}" ]; then
  UNITY_ROOT="$(ls -d /Applications/Unity/Hub/Editor/*/ 2>/dev/null | sort -V | tail -1 || true)"
fi

UNITY_MANAGED="${UNITY_ROOT}/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine"
if [ ! -d "${UNITY_MANAGED}" ]; then
  echo "Unity 매니지드 폴더를 찾지 못했습니다: ${UNITY_MANAGED}" >&2
  exit 2
fi

if [ -z "${UNITY_UGUI:-}" ]; then
  UNITY_UGUI="$(ls "${UNITY_ROOT}"/Unity.app/Contents/Resources/PackageManager/ProjectTemplates/libcache/*/ScriptAssemblies/UnityEngine.UI.dll 2>/dev/null | head -1 || true)"
fi

echo "Unity:    ${UNITY_ROOT}"
echo "런타임:   ${RUNTIME_PACKAGE}"
echo "참조 or:  ${REF_PROJECT}"
echo

dotnet build "${HERE}/UiMotion.UiService.Compile.csproj" \
  -p:UnityManaged="${UNITY_MANAGED}" \
  -p:UnityUgui="${UNITY_UGUI}" \
  -p:RuntimePackage="${RUNTIME_PACKAGE}" \
  -p:RefProject="${REF_PROJECT}" \
  -v quiet --nologo

echo
echo "브릿지 컴파일 통과."
```

```bash
chmod +x Tools~/compile-check/run.sh
```

- [ ] **Step 7: CI**

에디터 패키지의 `ci.yml`을 베끼되 다음을 바꾼다:
- asmdef 검사에서 `includePlatforms`가 `Editor`인지 보지 않는다 (여기는 런타임 어셈블리다).
- `Editor` 폴더가 없어야 한다는 검사 대신 **`Runtime` 폴더가 있어야 한다**로 바꾼다.
- `package.json`이 `com.juahn.v2.uimotion`에 의존하는지 확인한다.
- `.meta` 누락과 GUID 중복 검사는 그대로 가져온다.
- 컴파일 게이트가 로컬 전용이라는 사실과 `run.sh` 존재 확인도 그대로.

- [ ] **Step 8: `.meta` 파일**

`.github`와 `Tools~`를 제외한 모든 파일과 폴더에. 형식은 `com.juahn.v2.uimotion.editor`의 것과 같다.

- [ ] **Step 9: 게이트가 통과하고 실제로 오류를 잡는지 확인한다**

```bash
./Tools~/compile-check/run.sh
```

일부러 깨뜨려 본다:

```bash
echo 'using Cysharp.Threading.Tasks; class B { UniTask X() { return "nope"; } }' > Runtime/_Broken.cs
./Tools~/compile-check/run.sh; echo "exit=$?"
rm Runtime/_Broken.cs
./Tools~/compile-check/run.sh
```

기대: `error CS0029`와 `exit=1`, 되돌린 뒤 통과.

게이트가 런타임 패키지까지 실제로 훑는지 확인한다 — 항목이 3개라는 것만으로는 글롭이 0개를 잡아도 모른다:

```bash
dotnet msbuild Tools~/compile-check/UiMotion.UiService.Compile.csproj \
  -getItem:Compile -p:UnityManaged=... -p:RuntimePackage=... -p:RefProject=... \
  | grep -c '\.cs'
```

기대: 70개 이상 (코어 43 + 런타임 28 + 브릿지).

- [ ] **Step 10: 커밋**

```bash
git add -A
git commit -m "build: UiService 브릿지 패키지 뼈대와 컴파일 게이트 추가"
```

---

### Task 2: MotionGraphFeature

브릿지의 전부다. 파일 하나.

**Files:** `Runtime/MotionGraphFeature.cs`

- [ ] **Step 1: 구현한다**

```csharp
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Juahn.UiMotion.UiService
{
    /// <summary>
    /// UiPresenter의 열기·닫기 전이를 <see cref="MotionPlayer"/>의 그래프에 연결한다.
    ///
    /// <b>이 파일 하나가 "병합"의 전부다.</b> UiService의 <c>UiPresenter</c>는 이미
    /// <c>ITransitionFeature</c>를 <c>await</c>한 뒤에 비활성화·파괴하므로, 별도의
    /// 숨기기 계약을 새로 만들 필요가 없다.
    ///
    /// <see cref="MotionPlayer"/>와 같은 오브젝트에 붙인다.
    /// </summary>
    [AddComponentMenu("Juahn/UI Motion/Motion Graph Feature")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MotionPlayer))]
    public sealed class MotionGraphFeature : PresenterFeatureBase, ITransitionFeature
    {
        [SerializeField]
        [Tooltip("비워 두면 같은 오브젝트에서 찾는다.")]
        private MotionPlayer _player;

        [SerializeField]
        [Tooltip("켜면 Start 그래프가 끝날 때까지 프리젠터가 열림 완료를 미룬다.")]
        private bool _waitForStart = true;

        [SerializeField]
        [Tooltip("켜면 End 그래프가 끝날 때까지 프리젠터가 비활성화를 미룬다.")]
        private bool _waitForEnd = true;

        [NonSerialized] private UniTaskCompletionSource _openCompletion;
        [NonSerialized] private UniTaskCompletionSource _closeCompletion;

        /// <inheritdoc />
        public UniTask OpenTransitionTask => _openCompletion?.Task ?? UniTask.CompletedTask;

        /// <inheritdoc />
        public UniTask CloseTransitionTask => _closeCompletion?.Task ?? UniTask.CompletedTask;

        /// <summary>연결된 플레이어. 없으면 null.</summary>
        public MotionPlayer Player => _player;

        /// <summary>
        /// 임의 트리거를 발사한다. 프리젠터 코드가 <c>Click</c>·<c>Reward</c> 같은
        /// 연출을 부를 때 쓴다. 브릿지를 거치므로 프리젠터가 <c>MotionPlayer</c>를
        /// 직접 알 필요가 없다.
        /// </summary>
        public void Fire(string trigger)
        {
            if (_player != null)
            {
                _player.Fire(trigger);
            }
        }

        public void Stop(string trigger)
        {
            if (_player != null)
            {
                _player.Stop(trigger);
            }
        }

        private void Reset()
        {
            _player = GetComponent<MotionPlayer>();
        }

        private void OnValidate()
        {
            if (_player == null)
            {
                _player = GetComponent<MotionPlayer>();
            }
        }

        /// <inheritdoc />
        public override void OnPresenterInitialized(UiPresenter presenter)
        {
            base.OnPresenterInitialized(presenter);

            if (_player == null)
            {
                _player = GetComponent<MotionPlayer>();
            }

            if (_player == null)
            {
                MotionUiServiceLog.Warn(
                    "MotionGraphFeature에 MotionPlayer가 없습니다. 전이가 즉시 완료됩니다.", this);
                return;
            }

            // 트리거 발사를 이쪽이 전담한다고 선언한다. 이것이 없으면 PlayOnEnable이
            // SetActive(true) 순간에 Start를 한 번 더 발사한다. 재발사 정책이 Restart면
            // 우연히 무해하지만 Ignore나 Queue인 그래프에서는 실제 버그가 된다.
            //
            // 프리팹이 활성 상태로 인스턴스화돼 OnEnable이 여기보다 먼저 돌았더라도
            // 안전하다 — ClaimTriggerOwnership이 이미 시작된 것을 걷어낸다.
            _player.ClaimTriggerOwnership();
        }

        /// <inheritdoc />
        public override void OnPresenterOpened()
        {
            // Opening이 아니라 Opened에서 하는 이유가 둘이다.
            //
            // 하나, Opening 시점에는 GameObject가 아직 비활성이라 펌프에 등록되지 않았다.
            // 발사해도 아무도 틱하지 않는다.
            //
            // 둘, 프리젠터는 이 콜백 직후에 전이 태스크를 await한다. 그때 이미 완료된
            // 태스크는 건너뛰므로, 완료원은 반드시 그 전에 만들어져야 한다.
            if (!_waitForStart || _player == null)
            {
                FireWithoutWaiting(MotionRuntime.StartTrigger);
                return;
            }

            _openCompletion = new UniTaskCompletionSource();

            _player.Fire(MotionRuntime.StartTrigger);
            _player.WaitFor(MotionRuntime.StartTrigger, CompleteOpen);
        }

        /// <inheritdoc />
        public override void OnPresenterClosing()
        {
            // 여기서 하는 이유 — 프리젠터는 이 콜백 다음에 CloseTransitionTask를 await하고,
            // 그것이 끝나야 비활성화한다. 그래서 End 연출이 화면에 보이는 채로 끝까지 돈다.
            if (!_waitForEnd || _player == null)
            {
                FireWithoutWaiting(MotionRuntime.EndTrigger);
                return;
            }

            _closeCompletion = new UniTaskCompletionSource();

            // Fire("End")는 Loop를 먼저 멈춘다. 닫히는 중에 계속 떠다니면 안 되기 때문이다.
            _player.Fire(MotionRuntime.EndTrigger);
            _player.WaitFor(MotionRuntime.EndTrigger, CompleteClose);
        }

        /// <inheritdoc />
        public override void OnPresenterClosed()
        {
            // 닫힘이 끝났는데 대기자가 남아 있으면 프리젠터가 영영 비활성화되지 않는다.
            // WaitFor는 취소된 경우에도 콜백을 부르므로 정상 경로에서는 이미 풀렸지만,
            // 플레이어가 통째로 파괴되는 경우까지 여기서 막는다.
            CompleteClose();
        }

        private void OnDisable()
        {
            // 프리젠터가 아니라 다른 경로로 비활성화됐을 수 있다. 붙잡아 두지 않는다.
            CompleteOpen();
            CompleteClose();
        }

        private void OnDestroy()
        {
            CompleteOpen();
            CompleteClose();
        }

        private void FireWithoutWaiting(string trigger)
        {
            if (_player != null)
            {
                _player.Fire(trigger);
            }
        }

        private void CompleteOpen()
        {
            // TrySetResult라 두 번 불려도 안전하다.
            if (_openCompletion != null)
            {
                _openCompletion.TrySetResult();
            }
        }

        private void CompleteClose()
        {
            if (_closeCompletion != null)
            {
                _closeCompletion.TrySetResult();
            }
        }
    }
}
```

- [ ] **Step 2: 게이트를 돌린다**

```bash
./Tools~/compile-check/run.sh
```

- [ ] **Step 3: 대기자가 영영 안 풀리는 경로가 없는지 손으로 확인한다**

이 브릿지가 잘못되면 **팝업이 닫히지 않는다.** 코드를 읽으며 다음을 각각 짚는다. 고칠 것이 있으면 보고한다.

| 상황 | 닫힘 완료원이 풀리는 경로 |
|---|---|
| End 그래프가 정상 완료 | `WaitFor` 콜백 |
| End 그래프가 취소됨 | `WaitFor`는 취소에도 콜백을 부른다 |
| 그래프에 `End` 트리거가 없음 | `Fire`가 경고 1회 후 `ReleaseWaiters`, `WaitFor`가 즉시 콜백 |
| `MotionPlayer`에 그래프가 없음 | `Fire`가 no-op, `WaitFor`가 즉시 콜백 |
| 닫히는 중에 오브젝트가 파괴됨 | `MotionPlayer.OnDestroy` -> `StopAll` -> `ReleaseWaiters`, 그리고 이 컴포넌트의 `OnDestroy` |
| 닫히는 중에 비활성화됨 | 이 컴포넌트의 `OnDisable` |
| 같은 프리젠터를 다시 열었다 닫음 | 열 때마다 완료원을 새로 만든다 |

- [ ] **Step 4: `.meta`를 만들고 커밋한다**

```bash
git add -A
git commit -m "feat: UiPresenter 전이를 그래프에 연결하는 MotionGraphFeature 추가"
```

---

### Task 3: 브릿지 문서

**Files:** `README.md` · `docs/unity-verification.md` (+ `.meta`)

- [ ] **Step 1: README**

담을 것:
- 설치 순서 — 런타임 패키지, UiService, UniTask가 먼저 있어야 한다
- 붙이는 법 — 프리팹에 `MotionPlayer`와 `MotionGraphFeature`를 함께
- 그래프가 선언해야 하는 트리거 — `Start` · `Loop`(선택) · `End`
- `_waitForStart` / `_waitForEnd`를 끄면 무엇이 달라지는지
- **왜 `OnPresenterOpening`이 아니라 `OnPresenterOpened`인지** — 짧게. 나중에 누가 "더 일찍 발사하는 게 낫지 않나"로 되돌리는 것을 막는다
- 검증 명령

- [ ] **Step 2: Unity 확인 목록**

`docs/unity-verification.md`. 최소한 다음을 담는다:

- [ ] 팝업이 열릴 때 `Start` 연출이 끝난 뒤에 `OnOpenTransitionCompleted`가 온다
- [ ] `Start`가 끝나면 `Loop`가 자동으로 이어진다
- [ ] 닫을 때 `End` 연출이 **화면에 보이는 채로** 끝까지 돌고 그 뒤에 사라진다
- [ ] `PlayOnEnable`이 켜져 있어도 `Start`가 두 번 발사되지 않는다 (연출이 튀지 않는다)
- [ ] 그래프에 `End`가 없는 프리젠터도 정상적으로 닫힌다
- [ ] `MotionPlayer`에 그래프가 없어도 정상적으로 열리고 닫힌다
- [ ] 같은 팝업을 빠르게 여러 번 열고 닫아도 걸리지 않는다
- [ ] 닫히는 도중에 씬을 바꿔도 예외가 나지 않는다

- [ ] **Step 3: 커밋**

---

## 저장소 2 — `com.juahn.v2.uimotion.dotween`

### Task 4: 패키지 뼈대와 컴파일 게이트

**Files:** `package.json` · `.gitignore` · `README.md` · `CHANGELOG.md` · `LICENSE.md` · `Runtime/juahn.v2.UiMotion.DoTween.asmdef` · `Runtime/DoTweenEase.cs` · `Tools~/compile-check/{UiMotion.DoTween.Compile.csproj,run.sh}` · `.github/workflows/ci.yml` · 전부의 `.meta`

- [ ] **Step 1: 저장소와 `package.json`**

```json
{
  "name": "com.juahn.v2.uimotion.dotween",
  "displayName": "UI Motion DOTween Backend",
  "author": "armadimon",
  "version": "0.1.0",
  "unity": "6000.0",
  "license": "MIT",
  "type": "library",
  "description": "Optional DOTween backend for UI Motion. Swaps the built-in tween runner for DOTween's pooled tweens. Requires DOTween and the UIMOTION_DOTWEEN scripting define; without them this assembly compiles to nothing and UI Motion keeps using its built-in runner.",
  "dependencies": {
    "com.juahn.v2.uimotion": "0.1.0"
  }
}
```

- [ ] **Step 2: asmdef**

`Runtime/juahn.v2.UiMotion.DoTween.asmdef`:

```json
{
    "name": "juahn.v2.UiMotion.DoTween",
    "rootNamespace": "Juahn.UiMotion.DoTween",
    "references": [
        "juahn.v2.UiMotion",
        "juahn.v2.UiMotion.Core"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "DOTween.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UIMOTION_DOTWEEN"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**왜 `versionDefines`가 아니라 `defineConstraints`인가** — DOTween은 에셋스토어 플러그인이라 UPM 패키지가 아니다. `versionDefines`는 패키지 이름에 걸리므로 아예 발동하지 않는다. 사용자가 Player Settings에 `UIMOTION_DOTWEEN`을 한 번 추가하면 이 어셈블리가 켜지고, 없으면 통째로 컴파일에서 빠진다 — 스펙 3.4가 요구한 동작 그대로다.

`overrideReferences: true` + `precompiledReferences: ["DOTween.dll"]`는 플러그인 DLL을 명시적으로 참조하기 위한 것이다. 이것이 없으면 DOTween이 자동 참조되는지가 프로젝트 설정에 따라 달라진다.

- [ ] **Step 3: 이징 매핑**

`Runtime/DoTweenEase.cs`:

```csharp
using DG.Tweening;

namespace Juahn.UiMotion.DoTween
{
    /// <summary>
    /// 코어의 <see cref="EaseKind"/>를 DOTween의 <see cref="Ease"/>로 옮긴다.
    ///
    /// 두 백엔드가 같은 이징 이름에 대해 <b>눈에 띄게 다른 곡선</b>을 그리면
    /// 백엔드를 갈아 끼우는 순간 프로젝트 전체의 연출이 미묘하게 바뀐다.
    /// 다행히 13개가 전부 1:1로 대응한다.
    /// </summary>
    internal static class DoTweenEase
    {
        /// <summary>
        /// 모르는 값은 <see cref="Ease.Linear"/>다. 코어의
        /// <c>EaseLibrary.Evaluate</c>가 같은 규칙을 쓴다 — 두 백엔드가 같은
        /// 그래프에 대해 다르게 실패하면 안 된다.
        /// </summary>
        public static Ease Map(EaseKind kind)
        {
            switch (kind)
            {
                case EaseKind.Linear:     return Ease.Linear;

                case EaseKind.InQuad:     return Ease.InQuad;
                case EaseKind.OutQuad:    return Ease.OutQuad;
                case EaseKind.InOutQuad:  return Ease.InOutQuad;

                case EaseKind.InCubic:    return Ease.InCubic;
                case EaseKind.OutCubic:   return Ease.OutCubic;
                case EaseKind.InOutCubic: return Ease.InOutCubic;

                case EaseKind.InSine:     return Ease.InSine;
                case EaseKind.OutSine:    return Ease.OutSine;
                case EaseKind.InOutSine:  return Ease.InOutSine;

                case EaseKind.OutBack:    return Ease.OutBack;
                case EaseKind.OutElastic: return Ease.OutElastic;
                case EaseKind.OutBounce:  return Ease.OutBounce;

                default:                  return Ease.Linear;
            }
        }
    }
}
```

- [ ] **Step 4: 컴파일 게이트**

`UiMotion.DoTween.Compile.csproj`는 UiService 브릿지의 것과 같되 참조가 다르다:

- `UniTask`와 `juahn.UiService`를 **빼고**
- `DOTween`을 넣는다: `<HintPath>$(DoTweenDll)</HintPath>`
- **`UnityEngine`(통짜 파사드)과 `UnityEngine.AnimationModule`을 넣는다.** DOTween의 `SetEase` 오버로드 하나가 `AnimationCurve`를 받는데 그 타입이 통짜 어셈블리에 있어서, 없으면 `CS0012`로 깨진다. 실제로 확인한 사항이다.
- `DefineConstants`에 `UIMOTION_DOTWEEN`을 넣는다 — 그러지 않으면 asmdef의 제약과 달리 소스가 그냥 컴파일되므로, 게이트와 Unity의 조건이 어긋난다. 이 프로젝트는 asmdef를 읽지 않으므로 명시해야 한다.

```xml
  <PropertyGroup>
    ...
    <DefineConstants>$(DefineConstants);UIMOTION_DOTWEEN</DefineConstants>
  </PropertyGroup>
```

`run.sh`는 `DOTween.dll`을 찾는다. 형제 프로젝트들의 `Assets/Plugins/Demigiant/DOTween/DOTween.dll`을 훑고, 못 찾으면 `DOTWEEN_DLL`로 지정하라고 알려 준다:

```bash
if [ -z "${DOTWEEN_DLL:-}" ]; then
  DOTWEEN_DLL="$(ls "${PACKAGE_ROOT}"/../../*/Assets/Plugins/Demigiant/DOTween/DOTween.dll 2>/dev/null | head -1 || true)"
fi

if [ -z "${DOTWEEN_DLL}" ] || [ ! -f "${DOTWEEN_DLL}" ]; then
  echo "DOTween.dll을 찾지 못했습니다. DOTWEEN_DLL로 지정하세요." >&2
  echo "  예: DOTWEEN_DLL=/path/to/Assets/Plugins/Demigiant/DOTween/DOTween.dll $0" >&2
  exit 2
fi
```

- [ ] **Step 5: CI**

UiService 브릿지의 것과 같되, `defineConstraints`에 `UIMOTION_DOTWEEN`이 들어 있는지 확인하는 스텝을 더한다 — 그것이 빠지면 DOTween이 없는 프로젝트에서 컴파일이 깨진다.

- [ ] **Step 6: `.meta`를 만들고 게이트를 확인한 뒤 커밋한다**

일부러 깨뜨려 `exit=1`이 나오는 것까지 확인한다.

```bash
git commit -m "build: DOTween 백엔드 패키지 뼈대와 컴파일 게이트 추가"
```

---

### Task 5: DoTweenRunner

**Files:** `Runtime/DoTweenRunner.cs`

- [ ] **Step 1: 구현한다**

```csharp
using System;
using DG.Tweening;

namespace Juahn.UiMotion.DoTween
{
    /// <summary>
    /// DOTween을 시간 공급자로 쓰는 트윈 러너.
    ///
    /// <b>내장 러너와 근본적으로 다른 점</b> — DOTween은 자기 업데이트 루프에서 스스로
    /// 진행한다. 스코프가 넣어 주는 델타를 쓰지 않는다. 그래서 이 러너의 핸들은
    /// <c>Tick</c>에서 아무것도 하지 않고 <c>IsDone</c>으로 DOTween의 상태를 되비칠 뿐이다.
    ///
    /// 그 대가로 얻는 것은 DOTween의 트윈 풀링이다. 유한 트윈을 초당 여러 번 재발사하는
    /// 연출이 많으면 할당이 줄어든다. 유지 연출(<c>Float</c>·<c>Bounce</c>)은 내장 러너도
    /// 이미 프레임당 할당이 0이라 차이가 없다.
    ///
    /// <b>시간 스케일은 인스턴스가 정한다.</b> 러너 인터페이스가 그것을 나르지 않고,
    /// DOTween은 스코프의 델타를 보지 않기 때문이다. 부트스트랩이 그래프의
    /// <c>UseUnscaledTime</c>에 맞는 인스턴스를 꽂는다.
    /// </summary>
    public sealed class DoTweenRunner : IMotionTweenRunner
    {
        /// <summary>UI 기본값 — 일시정지 중에도 팝업은 열리고 닫혀야 한다.</summary>
        public static readonly DoTweenRunner Unscaled = new DoTweenRunner(true);

        public static readonly DoTweenRunner Scaled = new DoTweenRunner(false);

        private readonly bool _unscaled;

        public DoTweenRunner(bool useUnscaledTime)
        {
            _unscaled = useUnscaledTime;
        }

        public bool UseUnscaledTime => _unscaled;

        public IMotionHandle Run(float duration, EaseKind ease, Action<float> onEased)
        {
            // 지속시간 0은 "즉시"다. DOTween에 0짜리 트윈을 맡기면 콜백이 이번 프레임에
            // 오지 않거나 아예 오지 않을 수 있다. 계약은 "즉시 1을 한 번 보내고 끝난다"이므로
            // 여기서 직접 처리한다.
            if (duration <= 0f)
            {
                if (onEased != null)
                {
                    onEased(1f);
                }

                return MotionHandle.Completed;
            }

            if (onEased == null)
            {
                // 진행률이 필요 없는 경우(Delay 등). 그래도 시간은 흘러야 한다.
                Tweener empty = DOVirtual.Float(0f, 1f, duration, DoNothing)
                    .SetEase(Ease.Linear)
                    .SetUpdate(_unscaled);

                return new DoTweenHandle(empty);
            }

            Action<float> captured = onEased;

            // DOVirtual.Float은 이징이 적용된 값을 준다. 오버슈트하는 이징(OutBack,
            // OutElastic)에서는 1을 넘는 값이 나오고, 끝값은 정확히 1이다 —
            // 내장 러너와 같은 계약이다.
            Tweener tween = DOVirtual.Float(0f, 1f, duration, delegate(float eased) { captured(eased); })
                .SetEase(DoTweenEase.Map(ease))
                .SetUpdate(_unscaled);

            return new DoTweenHandle(tween);
        }

        private static void DoNothing(float value)
        {
        }

        /// <summary>
        /// DOTween 트윈 하나를 코어의 핸들 계약으로 감싼다.
        /// </summary>
        private sealed class DoTweenHandle : IMotionHandle
        {
            private readonly Tween _tween;

            public DoTweenHandle(Tween tween)
            {
                _tween = tween;
            }

            /// <summary>
            /// DOTween이 끝냈거나, 죽었거나, 우리가 죽였으면 끝이다.
            ///
            /// <c>IsActive</c>를 먼저 보는 이유 — 죽은 트윈에 <c>IsComplete</c>를 부르면
            /// DOTween이 경고를 낸다. 방치형에서 그 경고가 쌓이면 콘솔을 덮는다.
            /// </summary>
            public bool IsDone => _tween == null || !_tween.IsActive() || _tween.IsComplete();

            /// <summary>
            /// 아무것도 하지 않는다. DOTween이 자기 루프에서 이미 진행시켰다.
            ///
            /// 스코프가 넣어 주는 델타를 여기에 더하면 <b>시간이 두 번 흐른다.</b>
            /// </summary>
            public void Tick(float deltaSeconds)
            {
            }

            public void Cancel()
            {
                if (_tween != null && _tween.IsActive())
                {
                    // complete: false — 취소는 "중간에 끊겼다"이지 "끝났다"가 아니다.
                    // true로 두면 마지막 값이 한 번 더 적용돼 원상 복구와 싸운다.
                    _tween.Kill(false);
                }
            }
        }
    }
}
```

- [ ] **Step 2: 게이트를 돌린다**

- [ ] **Step 3: 계약을 하나씩 대조한다**

`IMotionTweenRunner.Run`의 XML 주석에 적힌 계약 세 줄을 이 구현이 지키는지 짚는다. 어기는 것이 있으면 보고한다.

| 계약 | 이 구현 |
|---|---|
| 끝날 때 정확히 1을 한 번 보낸다 | `DOVirtual.Float(0, 1, ...)`의 끝값이 정확히 1이다 |
| `duration <= 0`이면 즉시 1을 한 번 | 직접 처리한다 |
| 이징이 1을 넘을 수 있다 | `OutBack`·`OutElastic`이 그렇다. 노드가 `LerpUnclamped`를 쓴다 |

- [ ] **Step 4: 커밋**

---

### Task 6: 부트스트랩

러너를 만들어 두는 것만으로는 아무 일도 일어나지 않는다. 플레이어에 꽂아야 한다.

**Files:** `Runtime/MotionDoTweenBootstrap.cs`

- [ ] **Step 1: 구현한다**

```csharp
using UnityEngine;

namespace Juahn.UiMotion.DoTween
{
    /// <summary>
    /// <see cref="MotionPlayer"/>에 DOTween 러너를 꽂는다.
    ///
    /// <b>플레이 모드에서만 꽂는다.</b> DOTween의 업데이트 루프는 런타임에 만들어지는
    /// MonoBehaviour라 에디트 모드에서 돌지 않는다. 에디터 프리뷰에 DOTween 핸들이 걸리면
    /// <c>IsDone</c>이 영원히 false가 되어 <b>프리뷰가 멈춘 채 걸린다.</b>
    /// 프리뷰는 언제나 내장 러너를 쓴다 — 두 러너가 같은 이징 표를 쓰므로 타이밍 확인에는
    /// 차이가 없다.
    /// </summary>
    public static class MotionDoTweenBootstrap
    {
        /// <summary>
        /// 플레이어 하나에 러너를 꽂는다. 그래프의 시간 스케일에 맞는 인스턴스를 고른다.
        ///
        /// 그래프를 바꾼 뒤에는 다시 불러야 한다 — 시간 스케일이 그래프마다 다를 수 있다.
        /// </summary>
        public static void Apply(MotionPlayer player)
        {
            if (player == null || !Application.isPlaying)
            {
                return;
            }

            bool unscaled = player.Graph == null || player.Graph.UseUnscaledTime;
            player.TweenRunner = unscaled ? DoTweenRunner.Unscaled : DoTweenRunner.Scaled;
        }

        /// <summary>
        /// 씬에 있는 모든 플레이어에 꽂는다. 프로젝트 부트스트랩에서 씬 로드 뒤에 부른다.
        ///
        /// <b>나중에 만들어지는 플레이어는 잡지 못한다.</b> 풀에서 꺼내거나 UiService가
        /// 로드하는 팝업이 그렇다. 그런 경우는 만들어지는 자리에서 <see cref="Apply"/>를
        /// 부르거나, <see cref="MotionDoTweenScope"/>를 프리팹에 붙인다.
        /// </summary>
        public static int ApplyToLoadedScenes()
        {
            if (!Application.isPlaying)
            {
                return 0;
            }

            MotionPlayer[] players = Object.FindObjectsByType<MotionPlayer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < players.Length; i++)
            {
                Apply(players[i]);
            }

            return players.Length;
        }
    }

    /// <summary>
    /// 프리팹에 붙여 두면 그 플레이어가 활성화될 때마다 러너를 꽂는다.
    ///
    /// 전역 부트스트랩이 잡지 못하는 것들 — 풀에서 꺼내는 아이템, UiService가 나중에
    /// 로드하는 팝업 — 을 위한 것이다. 붙이는 비용이 <c>OnEnable</c> 한 번이라
    /// 전역 순회보다 오히려 싸다.
    /// </summary>
    [AddComponentMenu("Juahn/UI Motion/Motion DOTween Scope")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MotionPlayer))]
    public sealed class MotionDoTweenScope : MonoBehaviour
    {
        [SerializeField] private MotionPlayer _player;

        private void Reset()
        {
            _player = GetComponent<MotionPlayer>();
        }

        private void OnEnable()
        {
            if (_player == null)
            {
                _player = GetComponent<MotionPlayer>();
            }

            // MotionPlayer.OnEnable보다 먼저 돌 수도, 나중에 돌 수도 있다. 순서는
            // 컴포넌트 순서에 달렸고 보장되지 않는다.
            //
            // 그래도 문제가 없는 이유 — 러너는 노드가 Play될 때 ctx.Tween()으로 읽힌다.
            // 그 시점은 첫 Tick이고, OnEnable은 전부 그 전에 끝난다.
            MotionDoTweenBootstrap.Apply(_player);
        }
    }
}
```

- [ ] **Step 2: 게이트를 돌리고 커밋한다**

---

### Task 7: DOTween 백엔드 문서

**Files:** `README.md` · `docs/unity-verification.md`

- [ ] **Step 1: README**

담을 것:
- **설치가 두 단계다** — 패키지를 넣고, Player Settings에 `UIMOTION_DOTWEEN`을 추가한다. 두 번째를 빼먹으면 아무 일도 일어나지 않고 오류도 나지 않는다. 맨 위에 굵게 적는다.
- 러너를 꽂는 세 가지 방법 — 전역 부트스트랩, `MotionDoTweenScope` 컴포넌트, 직접 `Apply`
- **에디터 프리뷰는 내장 러너를 쓴다**는 것과 그 이유
- **언제 이것을 쓰는가** — 계획 2의 실측을 인용한다. 유지 연출은 내장 러너도 프레임당 할당이 0이므로 차이가 없다. 유한 트윈을 초당 여러 번 재발사하는 연출이 많을 때만 의미가 있다. 그냥 켜는 것이 아니라 측정하고 켠다.
- 시간 스케일이 인스턴스로 갈리는 이유

- [ ] **Step 2: Unity 확인 목록**

`docs/unity-verification.md`:

- [ ] `UIMOTION_DOTWEEN`이 없으면 패키지가 컴파일에서 빠지고 연출은 그대로 동작한다
- [ ] 정의를 추가하고 부트스트랩을 부르면 연출이 여전히 같은 타이밍으로 돈다
- [ ] `Time.timeScale = 0`에서 UI 연출이 계속 돈다 (unscaled 인스턴스)
- [ ] 그래프의 `UseUnscaledTime`을 끄면 `timeScale`을 따른다
- [ ] 연출 중에 오브젝트를 파괴해도 DOTween 경고가 나오지 않는다 (`Kill`이 제대로 불린다)
- [ ] **에디터 프리뷰가 여전히 동작한다** (내장 러너로 폴백)
- [ ] 씬을 바꿔도 트윈이 남지 않는다
- [ ] 원상 복구가 여전히 동작한다 — 연출 도중 팝업을 닫으면 대상이 원래 값으로 돌아온다

- [ ] **Step 3: 커밋**

---

### Task 8: 마무리 — 스펙과 번들 문서

**Files:** 런타임 저장소의 스펙과 README, 두 어댑터의 CHANGELOG

- [ ] **Step 1: 스펙 6절을 실제 구현으로 고친다**

`com.juahn.v2.uimotion/docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`의 6절이 이미 한 번 고쳐졌다면 구현과 일치하는지만 확인한다. 아직 `OnPresenterOpening`으로 남아 있으면 이 계획 앞부분의 표로 바꾼다.

- [ ] **Step 2: 스펙 3.4절에 `defineConstraints` 결정을 적는다**

지금은 "`versionDefines`로 DOTween이 없으면 컴파일에서 제외된다"고 적혀 있는데 **그것은 동작하지 않는다.** DOTween이 UPM 패키지가 아니기 때문이다. 실제로 쓴 방법(`defineConstraints` + 사용자가 추가하는 스크립팅 심볼)과 그 이유로 바꾼다.

- [ ] **Step 3: 런타임 패키지 README에 네 패키지 지도를 넣는다**

지금은 툴 패키지만 안내한다. 넷을 한 표로 정리한다 — 무엇이 필수이고 무엇이 선택인지, 각각 무엇을 요구하는지.

| 패키지 | 필요 | 요구 |
|---|---|---|
| `com.juahn.v2.uimotion` | 필수 | com.unity.ugui |
| `com.juahn.v2.uimotion.editor` | 그래프를 GUI로 만들려면 | 위 패키지 |
| `com.juahn.v2.uimotion.uiservice` | UiService를 쓰면 | UiService · UniTask |
| `com.juahn.v2.uimotion.dotween` | 측정 결과 필요하면 | DOTween · `UIMOTION_DOTWEEN` |

- [ ] **Step 4: 네 저장소의 게이트를 전부 돌린다**

```bash
cd com.juahn.v2.uimotion
dotnet test Tests~/dotnet/UiMotion.Core.Tests.csproj --verbosity minimal
grep -rnE "using UnityEngine|UnityEngine\." Runtime/Core/ ; echo "purity exit=$?"
./Tools~/compile-check/run.sh

for p in com.juahn.v2.uimotion.editor com.juahn.v2.uimotion.uiservice com.juahn.v2.uimotion.dotween; do
  (cd ../$p && ./Tools~/compile-check/run.sh)
done
```

각 저장소에서 `.meta` 누락과 GUID 중복도 확인한다.

- [ ] **Step 5: 커밋**

---

## 이것으로 스펙이 닫힌다

스펙 3절이 정의한 네 패키지가 전부 존재하고, 11절의 열린 질문 셋이 전부 답을 얻는다.

남는 것은 **Unity에서 손으로 확인하는 일**뿐이다. 네 저장소의 `docs/unity-verification.md`가 그 목록이다. 그 확인이 끝나기 전까지 이 시스템은 "컴파일되고 순수 로직이 검증된 상태"이지 "동작이 확인된 상태"가 아니다.
