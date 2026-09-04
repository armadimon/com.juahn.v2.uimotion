# UI Motion Graph — 설계 문서

작성일: 2026-09-04
상태: 승인됨 (구현 계획 대기)

## 1. 배경과 목표

IdlePaori는 UI 연출을 MonoBehaviour "모듈"로 조립한다. 세 계열이 있다.

- `UIButtonModule` — 입력 축. `UIButton`의 `Pressed`/`Clicked`/`Hover` 이벤트에 반응한다.
- `UIToggleModule` — 상태 축. `UIToggle.StateChanged`를 그린다.
- `UIAnimModule` — 재생 축. `Play`/`Stop`/`IsPlaying`/`Finished` 계약을 공유한다.

이 구조는 잘 작동하지만 한계가 있다. 연출 하나를 만들려면 프리팹에 컴포넌트를 여러 개 붙이고 인스펙터를 각각 채워야 하고, 완성된 연출을 다른 화면에 그대로 옮길 방법이 없다. 타이밍(무엇이 언제 시작해 언제까지 유지되는가)은 각 모듈의 `PlaysOnEnable`과 소유 화면의 명령형 코드에 흩어져 있어 전체를 한눈에 볼 수 없다.

이 문서는 그 조립을 **GUI 그래프**로 옮기고, 완성된 연출을 **에셋 하나로 재사용**하며, 전체를 **JuahnFramework에 독립 패키지로 편입**하는 설계를 정의한다.

### 목표

1. 쉐이더 그래프처럼 GUI로 UI 연출을 조립한다.
2. `Start` / `Loop` / `End`로 나눠 각 효과가 언제 활성화되고 어떻게 유지되는지를 그래프 위에서 조절한다.
3. 완성된 움직임을 프리셋(에셋)으로 저장해 어디에든 한 번에 적용한다.
4. 모듈(노드)과 관리 툴을 별도 패키지로 분리한다.
5. JuahnFramework의 번들 패키지로 관리하되, **어떤 프로젝트에든 단독으로 붙여 쓸 수 있다.**
6. 정해진 인터페이스를 구현하고 설명과 작동 예시를 포함하면 누구든 노드를 추가할 수 있다.

### 범위 밖 (명시)

- **IdlePaori 코드 변경.** IdlePaori는 "어떤 노드가 필요한가"를 정하는 **참조 원본**으로만 쓴다. 이식 여부는 패키지 완성 후 별도로 결정한다.
- **값 배선(데이터 플로우).** 노드 간 배선은 실행 흐름만 나른다. 커브·랜덤·게임 데이터가 파라미터로 흘러드는 기능은 2차로 미룬다. 효과 노드에 입력 핀을 추가하는 방식이라 데이터 포맷을 깨지 않고 얹을 수 있다.
- **변주 프리셋(파라미터 오버라이드 에셋).** 값 배선과 기능이 크게 겹치므로 값 배선을 도입할 때 함께 판단한다.
- **사운드·햅틱 노드.** 아래 8절 참조.
- **로컬라이즈·게임 데이터 바인딩 노드.** 코어의 "참조 0"을 깨므로 제외한다.

## 2. 확정된 설계 결정

| # | 결정 | 선택 | 이유 |
|---|------|------|------|
| 1 | 그래프의 결과물 | **데이터 그래프** — 그래프가 `.asset`을 굽고 런타임 플레이어 1개가 해석 | 프리셋 재사용이 목표의 핵심. 프리팹 diff가 깨끗하고 툴이 그래프 하나만 알면 된다 |
| 2 | 그래프의 문법 | **노드 그래프, 실행 흐름만 배선** | 조건 분기와 임의 중첩이 되면서 값 배선의 복잡도는 피한다. 나중에 입력 핀으로 확장 가능 |
| 3 | 위상 트리거 | **이름 있는 트리거 채널**, `Start`/`Loop`/`End`는 예약 이름 | 생명주기 고정으로는 클릭·보상 같은 이벤트 연출을 표현할 수 없다 |
| 4 | 타겟 지정 | **슬롯 바인딩** (`Self` 예약 + 이름 슬롯) + 이름 규약 자동 바인딩 | 한 그래프가 계층 전체의 합주를 표현하면서도 다른 프리팹에 재사용된다 |
| 5 | 패키징 | **독립 코어 + 어댑터 브릿지** (4패키지) | `com.juahn.v2.vcontainer`가 이미 쓰는 관례. "단독 사용"의 유일한 보증 |
| 6 | 트윈 백엔드 | **코어 내장 러너(의존성 0) + 선택적 어댑터** | DOTween은 유료 에셋이라 코어에 넣으면 MIT 배포가 막힌다 |
| 7 | 프리셋 계층 | **그래프 에셋 + 서브그래프** | 재사용 관용구를 한 곳에서 고칠 수 있어야 복붙 그래프가 쌓이지 않는다 |
| 8 | 모듈 계약 강제 | **중간** — 미검증 격리 + 빌드/CI 게이트 | 실험은 막지 않되 배포된 것은 100% 문서화됨을 보장 |
| 9 | IdlePaori | **참조 원본만** | 범위가 명확하고 운영 중인 게임에 리스크가 0 |
| 10 | 검증 | **순수 C# 어셈블리 분리 + `dotnet test` CI 게이트**, Unity 계층은 컴파일 + 스모크 | 사람이 기억해서 로컬에서 돌려야 하는 테스트는 썩는다. 버그가 가장 많이 날 곳이 마침 순수 로직이다 |

## 3. 패키지 구성

각 패키지는 v2 관례대로 **독립 git 저장소**다 (`com.juahn.v2.di` 등과 동일).

### 3.1 `com.juahn.v2.uimotion` — 코어 (어셈블리 2개)

패키지는 하나지만 어셈블리를 둘로 나눈다. **경계 기준은 UnityEngine 의존 여부**다.

이 분리의 목적은 두 가지다. 첫째, 버그가 가장 많이 날 부분(취소·원상복구·스코프 생명주기·순환 검출)을 UnityEngine 없이 짜서 `dotnet test`로 **CI에서 무료로** 검증할 수 있게 한다. 둘째, 같은 규약을 이미 `noEngineReferences: true`인 `com.juahn.v2.architecture` · `com.juahn.v2.sim`에 그대로 확산시킬 수 있다.

**`juahn.v2.UiMotion.Core`** — `noEngineReferences: true`. 참조 없음. 순수 C#.

```
Runtime/Core/
  Graph/     NodeId · NodeLink · TriggerDeclaration · SlotDeclaration
             IMotionGraphView (실행기가 보는 그래프의 유일한 창구)
  Authoring/ MotionNodeAttribute · MotionSlotAttribute · MotionParamAttribute
             MotionNodeBase · MotionFlowNode · MotionEffectNode
             IMotionContext · ISlotResolver
  Nodes/     TriggerNode · SequenceNode · ParallelNode · DelayNode
             RepeatNode · StopTriggerNode · SubGraphNode(추상)
  Exec/      MotionRuntime · MotionScope · IMotionScope · MotionContext
             NodeRun · TriggerRunner · ITriggerSink
             GraphCycleDetector · MotionTimer · IMotionHandle · MotionHandle
  Easing/    EaseKind · EaseLibrary
  Diag/      IMotionLog · OnceLogger
```

**`juahn.v2.UiMotion`** — UnityEngine 의존. 참조: `juahn.v2.UiMotion.Core`.

```
Runtime/Unity/
  Graph/     MotionGraph(ScriptableObject, IMotionGraphView 구현)
  Runtime/   MotionPlayer(MonoBehaviour) · SlotBinding · SlotTable
             UnityMotionContext · UnityMotionLog
  Tween/     IMotionTweenRunner · BuiltinTweenRunner · MotionPump
  Nodes/     MoveNode · ScaleNode · RotateNode · FadeNode · ColorNode
             PunchScaleNode · ShakeNode · FloatNode · BounceNode
             FlyAcrossNode · SetActiveNode · PlayAnimatorNode · SendSignalNode
```

**노드 클래스가 순수 쪽에 있어도 되는 이유** — Unity 직렬화에 필요한 `System.SerializableAttribute`와 public 필드는 순수 C#이다. UnityEngine이 필요한 것은 `[SerializeField]`·`[Tooltip]`·`[Range]` 같은 인스펙터 어트리뷰트뿐인데, 어차피 그래프 창이 자체 인스펙터를 그리므로 `[MotionParam]`으로 대체한다. 대상을 실제로 만지는 효과 노드만 Unity 쪽에 둔다.

**"참조 0"은 여전히 유효하다.** 두 어셈블리 중 어느 것도 UiService·DOTween·UniTask를 참조하지 않는다. 목표 5는 그대로다.

### 3.2 `com.juahn.v2.uimotion.editor` — 관리 툴

참조: `juahn.v2.UiMotion`. `includePlatforms: [Editor]`.

- `MotionGraphWindow` — GraphView 기반 그래프 편집 창
- `NodePalette` — 리플렉션으로 노드를 스캔해 카테고리별로 나열, 미검증 노드는 별도 섹션으로 격리
- `MotionPlayerInspector` — 슬롯 목록 자동 동기화, 자동 바인딩 버튼, 트리거 테스트 재생
- `NodeDoctorWindow` — 설명/예시 누락 검사표. `-executeMethod`로 배치 실행 가능
- `MotionPreviewDriver` — 플레이 모드 없이 `EditorApplication.update`로 그래프를 실제 프리팹 위에서 재생
- `PresetBrowser` — 그래프 에셋 목록과 프리뷰

### 3.3 `com.juahn.v2.uimotion.uiservice` — UiService 브릿지

참조: `juahn.v2.UiMotion`, `juahn.UiService`. 파일 1~2개.

`MotionGraphFeature : PresenterFeatureBase, ITransitionFeature` 하나가 병합의 전부다. 매핑은 6절 참조.

### 3.4 `com.juahn.v2.uimotion.dotween` — 선택 백엔드

참조: `juahn.v2.UiMotion`, DOTween. `versionDefines`로 DOTween이 없으면 컴파일에서 제외된다.

`DoTweenRunner : IMotionTweenRunner`. PrimeTween 백엔드도 같은 자리에 별도 패키지로 추가할 수 있다.

## 4. 런타임 아키텍처

### 4.1 불변식 — 그래프는 상태를 갖지 않는다

`MotionGraph`(ScriptableObject)는 노드 배열, 연결, 선언된 슬롯 목록, 트리거 목록만 담는 **불변 데이터**다. 실행 중 어떤 필드도 쓰지 않는다.

이것은 타협 불가능한 불변식이다. 같은 프리셋 에셋을 인벤토리 슬롯 100개가 동시에 쓰는 것이 정상적인 사용법이며, 그래프가 실행 상태를 조금이라도 들고 있으면 그 순간 전부 깨진다.

실행 상태는 전부 `MotionScope`가 소유한다.

### 4.2 구성요소

| 구성요소 | 역할 |
|---|---|
| `MotionGraph` | 불변 데이터. 노드 · 연결 · 슬롯 선언 · 트리거 선언 |
| `MotionPlayer` | 프리팹에 붙는 유일한 컴포넌트. 그래프 참조 + 슬롯 바인딩 배열을 들고 `Fire` / `Stop` / `WaitFor`를 노출 |
| `MotionRuntime` | 그래프를 해석해 노드를 실행. 플레이어당 1개 |
| `MotionScope` | 트리거 1회 발사가 만드는 실행 단위. 자기 트윈·타이머·원본값 스냅샷을 전부 소유하고, 취소되면 전부 정리 |
| `MotionContext` | 스코프가 노드에 넘기는 것 — 슬롯 테이블, 시간 스케일, 취소 토큰, 트윈 러너 |
| `SlotTable` | `Self`(예약) + 이름 슬롯 → 실제 오브젝트 해석 |

### 4.3 MotionPlayer 공개 API

```csharp
public sealed class MotionPlayer : MonoBehaviour
{
    public MotionGraph Graph { get; }

    public void Fire(string trigger);
    public void Stop(string trigger);
    public void StopAll();
    public bool IsPlaying(string trigger);

    // 브릿지와 자체 UI 베이스가 쓴다. 코어는 UniTask를 참조하지 않으므로
    // 콜백 기반 시그니처를 제공하고, 어댑터 쪽에서 UniTask로 감싼다.
    public void WaitFor(string trigger, Action onCompleted);

    // 코드 없이 단독으로 쓰기 위한 옵션. 기본 켬.
    // 호스트가 트리거를 직접 몰아주는 경우(6절 브릿지 등)에는 무시된다 — 아래 참조.
    [SerializeField] private bool _playOnEnable = true;
}
```

**`PlayOnEnable`과 호스트의 충돌 방지.** `PlayOnEnable`이 켜진 채로 UiService 브릿지까지 붙으면 `Start`가 두 번 발사된다. 이를 막기 위해 `MotionPlayer`는 "트리거를 몰아주는 주인"이 있는지 여부를 갖는다. 브릿지 같은 호스트 어댑터가 `ClaimTriggerOwnership()`을 호출하면 `PlayOnEnable`은 무시된다. 재발사 정책 `Restart`로 우연히 무해해지는 데 기대지 않는다 — `Ignore`나 `Queue`를 쓰는 그래프에서는 실제 버그가 되기 때문이다.

**시간 스케일.** UI 연출은 기본적으로 `Time.timeScale`을 무시한다(unscaled). 일시정지 중에도 팝업은 열리고 닫혀야 하기 때문이다. 그래프 단위로 이 기본값을 뒤집는 옵션을 두되, 노드 단위 설정은 두지 않는다 — 한 연출 안에서 노드마다 시간축이 다르면 타이밍을 추론할 수 없게 된다.

### 4.4 위상 규약

| 트리거 | 의미 |
|---|---|
| `Start` | 진입 시 1회. 완료되면 `Loop`를 자동 발사한다 (옵션, 기본 켬) |
| `Loop` | 무한 반복 허용. `End`가 발사되면 자동 취소된다 |
| `End` | 완료될 때까지 호스트가 기다린다 |
| 임의 이름 | `Click` · `Denied` · `Reward` … 서로 독립 스코프. 위 3개와 동시에 돈다 |

같은 트리거를 재발사했을 때의 정책은 **트리거별로** 고른다.

- `Restart` — 도는 중이면 끊고 처음부터 (기본값)
- `Ignore` — 도는 중이면 무시
- `Queue` — 현재 스코프가 끝난 뒤 실행

### 4.5 원상 복구 (Revert)

효과 노드는 시작할 때 대상의 원래 값을 `ctx.Scope.Remember(...)`로 등록한다. 스코프가 **취소될 때** 등록분을 **역순으로** 되돌린다.

**자연 완료 시에는 되돌리지 않는다.** 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다. 되돌림은 "중간에 끊겼다"의 처리이지 "끝났다"의 처리가 아니다. 완료 시점에 원래 값으로 돌아와야 하는 연출(펀치·흔들림)은 그 자체가 왕복이므로 애초에 제자리에서 끝난다.

노드의 `Reverts` 속성은 엔진이 읽지 않는다 — 되돌릴지는 노드가 `Remember`를 부르는지로 정해진다. 이 속성은 에디터와 Node Doctor가 "이 노드는 중단되면 원상 복구합니다"를 표시하는 용도다.

방치형 게임에서 앱은 몇 시간씩 켜져 있다. 꺼지지 않은 트윈 하나가 계속 돌고, 부유 연출이 만든 중간 위치를 다음 재생이 기준으로 삼으면 오브젝트가 갈수록 밀린다. IdlePaori의 `UIFloatingModule`은 이 문제를 `_origin` 캡처와 `OnStop`의 복원으로 손수 해결하고 있다. 그 처리를 프레임워크가 모든 노드에 대해 보장하도록 승격한다.

`OnDisable` / `OnDestroy`에서 `MotionPlayer`는 모든 스코프를 취소한다. 트윈 누수는 0이어야 한다.

## 5. 슬롯 바인딩

### 5.1 선언과 해석

- 그래프의 선언 슬롯 목록은 노드들이 참조하는 슬롯 이름의 합집합이다.

**슬롯 목록은 저작하는 값이 아니라 파생되는 값이다.** 사람이 인스펙터에서 슬롯을 추가하거나 타입을 고르지 않는다. 그래프가 자기 노드들을 훑어 `SlotRef` 필드와 그 필드에 붙은 `[MotionSlot(typeof(...))]`을 모아 목록을 만든다. 노드를 지우면 그 슬롯도 목록에서 사라진다.

이 구분이 중요한 이유는 `SlotDeclaration.RequiredType`이 `System.Type`이라 Unity가 직렬화할 수 없기 때문이다. 파생 값이므로 저장할 필요가 없고, 도메인 리로드 후 다시 계산하면 된다. 반대로 이걸 사람이 고르는 저장 값으로 설계하면 에디터를 다시 열 때마다 타입이 `null`로 리셋되는 버그가 된다.

**저장되는 것은 슬롯 이름 → 오브젝트 바인딩뿐이고, 그건 그래프가 아니라 프리팹의 `MotionPlayer`가 갖는다.**
- `Self`는 예약 슬롯으로 항상 `MotionPlayer`의 `Transform`이다.
- 슬롯은 요구 타입을 갖는다(`Transform` / `RectTransform` / `Graphic` / `CanvasGroup` / `GameObject`). 인스펙터가 그 타입으로 필터링한다.
- 미할당 슬롯을 참조하는 노드는 **건너뛰고 경고를 인스턴스당 1회** 남긴다. 게임이 죽지 않는다.

### 5.2 자동 바인딩

`MotionPlayer` 인스펙터의 버튼을 누르면, 슬롯 이름과 같은 이름의 자식을 계층에서 찾아 한 번에 채운다.

이 탐색은 **에디터에서 버튼을 눌렀을 때만** 실행된다. 결과가 인스펙터에 그대로 박히므로, 나중에 오브젝트 이름이 바뀌어도 연출이 조용히 사라지지 않고 다음에 인스펙터를 열었을 때 눈에 보인다. 런타임 경로 탐색을 채택하지 않은 이유가 이것이다.

### 5.3 그래프 교체

그래프를 바꾸면 슬롯 목록을 다시 계산하되 **이름이 같은 기존 바인딩은 보존**한다. 새 그래프에 없는 슬롯은 "이 그래프에 없는 슬롯"으로 표시한 채 유지한다. 조용히 버리지 않는다.

## 6. UiService 연동

`com.juahn.uiservice`의 `UiPresenter`는 이미 전이를 기다리는 생명주기를 갖고 있다. `InternalCloseProcessAsync`가 `ITransitionFeature.CloseTransitionTask`를 `await`한 뒤에 비활성화/파괴한다. 별도의 `HideAsync` 계약을 새로 만들 필요가 없다.

| UiPresenter 생명주기 | 브릿지 동작 |
|---|---|
| `OnPresenterOpening()` | `Fire("Start")` |
| `OpenTransitionTask` | Start 스코프 완료를 나른다 → 프리젠터가 `OnOpenTransitionCompleted()`를 늦춘다 |
| `OnPresenterOpened()` | (Start 완료 시 `Loop`가 자동 발사된다) |
| `OnPresenterClosing()` | `Fire("End")` — `Loop`는 자동 취소 |
| `CloseTransitionTask` | End 스코프 완료를 나른다 → 프리젠터가 비활성/파괴를 그때까지 미룬다 |

UiService를 쓰지 않는 프로젝트는 `PlayOnEnable`로 코드 없이 돌리거나, 자체 UI 베이스에서 `Fire` / `WaitFor`를 직접 부른다.

## 7. 모듈(노드) 확장 계약

### 7.1 세 가지 요구사항

| 요구사항 | 어디에 | 없으면 |
|---|---|---|
| 인터페이스 구현 | `MotionEffectNode`(무언가를 움직인다) 또는 `MotionFlowNode`(실행 순서를 정한다) 상속 + `OnPlay` | 노드로 인식되지 않는다 |
| 설명 | `[MotionNode]`의 `Name` · `Category` · `Summary` | 팔레트 "미검증" 섹션으로 격리 |
| 작동 예시 | `Sample`이 가리키는 `.motiongraph` 에셋 | 팔레트 "미검증" 섹션으로 격리 |

설명을 별도 문서가 아니라 어트리뷰트에, 예시를 스크린샷이 아니라 **실행 가능한 그래프 에셋**에 두는 것이 핵심이다. 둘 다 코드 옆에 있어 썩지 않고, 툴이 기계적으로 검사할 수 있다. 팔레트에서 노드에 호버하면 그 예시 그래프가 그 자리에서 재생된다.

### 7.2 예시

```csharp
[MotionNode(
    Name     = "Scale Punch",
    Category = "Transform",
    Summary  = "대상을 잠깐 부풀렸다 되돌린다. 획득·강조 순간에 쓴다.",
    Sample   = "ScalePunch")]          // Samples~/Nodes/ScalePunch.motiongraph
[Serializable]
public sealed class ScalePunchNode : MotionEffectNode
{
    [MotionSlot(typeof(RectTransform))]
    public SlotRef Target = SlotRef.Self;

    public float    Amplitude = 0.2f;
    public float    Duration  = 0.2f;
    public EaseKind Ease      = EaseKind.OutBack;

    public override bool Reverts => true;

    protected override IMotionHandle OnPlay(in MotionContext ctx)
    {
        var target = ctx.Resolve<RectTransform>(Target);
        if (target == null) return MotionHandle.Skipped;

        ctx.Scope.Remember(target, target.localScale);
        return ctx.Tween.PunchScale(target, Amplitude, Duration, Ease);
    }
}
```

### 7.3 Node Doctor

검사 창이 등록된 모든 노드에 대해 설명 유무, 예시 에셋 유무, 예시가 실제로 재생되는지를 표로 보여준다.

미검증 노드도 **그래프에서 쓸 수는 있다** — 리팩터 도중의 실험용 노드를 막지 않기 위해서다. 대신 `-executeMethod`로 CI와 패키지 배포 파이프라인에서 검사를 돌려, 미검증이 하나라도 있으면 실패시킨다. 배포된 것은 100% 문서화됨이 보장된다.

## 8. 1차 노드 세트

IdlePaori의 기존 25개 모듈을 훑어 뽑았다.

**흐름 노드** — `Trigger`(진입) · `Sequence` · `Parallel` · `Delay` · `Repeat` · `SubGraph` · `StopTrigger`

**효과 노드** — `Move` · `Scale` · `Rotate` · `Fade` · `Color` · `PunchScale` · `Shake` · `Float` · `Bounce` · `FlyAcross` · `SetActive` · `PlayAnimator` · `SendSignal`

### 일부러 제외한 것

**사운드와 햅틱.** 프로젝트마다 오디오 시스템이 다르므로 코어에 넣으면 "참조 0"이 깨진다. `SendSignal("sfx:click")` 노드 하나만 두고, 신호를 받아 실제로 소리를 내거나 진동시키는 쪽은 프로젝트가 구현한다. IdlePaori의 `UISoundModule` / `UIButtonHapticModule`이 여기 해당한다.

같은 이유로 로컬라이즈와 게임 데이터 바인딩도 1차에서 제외한다.

## 9. 오류 처리

방치형 게임이 대상이므로 "조용한 실패"를 특히 경계한다. 몇 시간 뒤에 드러나는 문제는 재현이 어렵다.

| 상황 | 처리 |
|---|---|
| 미할당 슬롯 | 노드 스킵 + 인스턴스당 경고 1회 (매 프레임 스팸 금지) |
| 서브그래프 순환 참조 | 에디터 저장 시 거부 + 런타임 깊이 제한으로 이중 방어 |
| 없는 트리거 `Fire` | 경고 1회 후 무시. 에디터는 그래프가 선언한 트리거를 드롭다운으로 제공해 예방 |
| 재생 중 오브젝트 파괴/비활성 | 스코프 전부 취소 + Revert. 트윈 누수 0 |
| 그래프 교체로 생긴 고아 슬롯 | 인스펙터가 유지하고 표시. 조용히 버리지 않는다 |
| 트윈 백엔드 패키지 없음 | 내장 러너로 폴백. 코어만으로 항상 동작 |
| 노드 타입이 사라진 그래프 로드 | 해당 노드를 "결손 노드"로 유지하고 실행 시 스킵. 그래프의 나머지는 정상 동작 |

## 10. 검증

이 패키지가 **v2 라인에 테스트 규약을 처음 세운다.** 현재 v2에는 테스트가 하나도 없고 `_shared/ci.yml`은 `package.json` 유효성과 asmdef 존재만 검사한다. 여기서 만드는 규약을 다른 v2 패키지가 차츰 따라 쓴다.

### 10.1 순수 코어 — `dotnet test`, CI 게이트

`juahn.v2.UiMotion.Core`는 UnityEngine을 참조하지 않으므로 Unity 없이 컴파일하고 테스트할 수 있다. `Tests~/dotnet/`에 NUnit 프로젝트를 두고 코어 소스를 링크해 GitHub Actions에서 돌린다. Unity 라이선스가 필요 없으므로 **무료로, 모든 푸시마다** 돈다.

TDD로 짓는다. 커버 대상:

- 흐름 노드의 실행 순서 (`Sequence` · `Parallel` · `Delay` · `Repeat`)
- 스코프 생명주기 — 취소 시 원상 복구가 **등록의 역순으로** 전부 실행되는가
- 트리거 재발사 정책 (`Restart` · `Ignore` · `Queue`)
- 서브그래프 순환 검출
- 이징 함수의 경계값 (`t=0` → 0, `t=1` → 1)
- 경고 1회 정책이 실제로 한 번만 로그하는가

`_shared/ci.yml`에 이 테스트 스텝을 추가해 다른 패키지가 복사해 쓸 수 있게 한다.

### 10.2 Unity 계층 — 컴파일 + 스모크

`juahn.v2.UiMotion`(MonoBehaviour · ScriptableObject · 트윈 · 효과 노드)은 Unity 없이 돌릴 수 없다. 여기는 전역 규칙(`~/.claude/rules/testing.md`)대로 **컴파일 통과 + 에디터 스모크**로 검증한다.

각 노드의 `Sample` 그래프가 곧 스모크 케이스다. Node Doctor가 전부 재생해보는 것으로 회귀를 얕게 잡는다.

Unity Test Framework 기반 EditMode/PlayMode 테스트는 이번 범위 밖이다. 시간 기반이라 불안정해지기 쉽고, 로직의 핵심은 이미 10.1이 덮는다.

## 11. 열린 질문 (구현 계획에서 정할 것)

1. 그래프 직렬화 형식 — `[SerializeReference]` 다형 배열이 유력하나, Unity의 결손 타입 처리와 머지 충돌 특성을 확인해야 한다.
2. 내장 트윈 러너의 구동 방식 — `Update` 펌프 단일 인스턴스 vs 플레이어별 코루틴. 방치형이므로 수백 개 동시 재생을 가정해야 한다.
3. 에디터 프리뷰가 프리팹 스테이지와 씬 인스턴스 중 무엇을 대상으로 할지, 그리고 프리뷰가 만든 변경이 프리팹에 새어 나가지 않게 하는 방법.
