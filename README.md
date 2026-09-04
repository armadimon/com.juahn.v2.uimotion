# UI Motion (com.juahn.v2.uimotion)

JuahnFrameworkV2 스택의 UI 연출 패키지. 노드 그래프로 UI 움직임을 조립하고,
그 결과를 에셋 하나로 저장해 어디에든 재사용한다.

**현재 상태:** 순수 C# 코어와 Unity 런타임 계층이 구현됨. 그래프 창 · 노드 팔레트 ·
슬롯 자동 바인딩 인스펙터 같은 에디터 툴과, UiService · DOTween 어댑터는 후속 작업이다.
지금은 그래프를 코드로 조립해 재생할 수 있다.

## 어셈블리 두 개

| 어셈블리 | 의존 | 내용 |
|---|---|---|
| `juahn.v2.UiMotion.Core` | 없음 (`noEngineReferences`) | 그래프 실행 엔진 · 스코프 · 흐름 노드 · 이징 · 순환 검출 |
| `juahn.v2.UiMotion` | Core | `MotionGraph` 에셋 · `MotionPlayer` · 틱 펌프 · 트윈 러너 · 효과 노드 13종 |

코어가 UnityEngine을 참조하지 않는 덕에 `dotnet test`로 Unity 없이 검증된다.
그 결과 취소 · 원상 복구 · 스코프 생명주기처럼 버그가 가장 많이 나는 부분이
Unity 라이선스 없이 모든 푸시마다 CI에서 검사된다.

## 위상

| 트리거 | 의미 |
|---|---|
| `Start` | 진입 시 1회. 자연 완료하면 `Loop`를 자동 발사한다 |
| `Loop` | 무한 반복 허용. `End`가 발사되면 자동 취소된다 |
| `End` | 완료될 때까지 호스트가 기다린다 |
| 임의 이름 | `Click` · `Denied` · `Reward` … 서로 독립 스코프로 동시에 돈다 |

같은 트리거를 재발사했을 때의 정책은 트리거별로 고른다 —
`Restart`(끊고 처음부터) · `Ignore`(도는 중이면 무시) · `Queue`(하나만 대기).

## 원상 복구

효과 노드는 시작할 때 원래 값을 `ctx.Scope.Remember(...)`로 등록한다.
스코프가 **취소될 때** 등록의 **역순**으로 되돌린다.

**자연 완료 시에는 되돌리지 않는다** — 페이드인이 끝나자마자 다시 투명해지면 안 되기
때문이다. 되돌림은 "중간에 끊겼다"의 처리이지 "끝났다"의 처리가 아니다.

## 설치

Unity Package Manager → Add package from git URL:

```
https://github.com/armadimon/com.juahn.v2.uimotion.git
```

## Unity 계층

### 그래프 하나, 플레이어 하나

1. Project 창에서 `Create > Juahn > UI Motion > Motion Graph`로 그래프 에셋을 만든다
2. 연출을 붙일 프리팹 루트에 `Add Component > Juahn > UI Motion > Motion Player`를 붙이고
   `Graph`에 그 에셋을 꽂는다
3. `Play On Enable`이 켜져 있으면 활성화되는 순간 `Start`가 자동으로 발사된다

`MotionGraph`는 여러 오브젝트가 공유하는 **불변 데이터**다. "누구를 움직이는가"(슬롯 바인딩)와
"지금 무엇이 도는가"(실행 상태)는 전부 `MotionPlayer`가 갖는다. 그래서 인벤토리 슬롯 수백 개가
같은 에셋 하나를 동시에 재생해도 서로를 밟지 않는다.

재생은 플레이어마다 도는 `Update`가 아니라 숨겨진 전역 `MotionPump` 하나가 돌린다.
`MonoBehaviour.Update`는 호출마다 네이티브-매니지드 경계를 넘으므로, 300개가 동시에 살아 있는
방치형에서 그 비용은 300배가 된다. 펌프는 1회다.

### 호스트가 부르는 네 가지

| 멤버 | 용도 |
|---|---|
| `Fire(trigger)` | 트리거 하나를 발사한다. 재발사 정책(`Restart` · `Ignore` · `Queue`)은 그래프가 트리거별로 정해 둔 것을 따른다 |
| `Stop(trigger)` | 도는 스코프를 **취소**한다. 취소이므로 원상 복구가 등록의 역순으로 실행된다 |
| `WaitFor(trigger, onCompleted)` | 트리거가 끝나면 콜백을 부른다. **자연 완료든 취소든 부른다** — 대기자를 영영 붙잡아 두면 팝업이 닫히지 않는다. 지금 재생 중이 아니면 즉시 부른다 |
| `ClaimTriggerOwnership()` | 트리거 발사를 호스트가 전담한다고 선언한다. `Play On Enable`이 무시된다 |

`ClaimTriggerOwnership`이 필요한 이유는 이중 발사다. 호스트(UiService 브릿지 같은 것)가
열림 시점에 `Fire("Start")`를 부르는데 `Play On Enable`도 켜져 있으면 `Start`가 두 번 나간다.
정책이 `Restart`면 우연히 무해하지만 `Ignore`나 `Queue`인 그래프에서는 실제 버그가 된다.
**첫 `OnEnable`보다 먼저 부르는 것이 안전하다.** 늦게 불러도 이미 시작된 것을 걷어내지만,
그 사이에 한 프레임이 재생된다.

### 슬롯 목록은 저작하지 않는다

슬롯은 노드가 선언한다 — `SlotRef` 필드에 `[MotionSlot(typeof(RectTransform))]`을 다는 것이
선언의 전부다. 그래서 **플레이어의 슬롯 목록은 그래프에서 매번 계산되는 파생값**이고, 사람이
채우는 것은 그 목록의 **값**(어떤 오브젝트를 꽂을지)뿐이다.

`SyncBindings()`가 그 목록을 현재 그래프에 맞춘다. 인스펙터가 부르고, 코드로 그래프를 갈아
끼울 때는 `SetGraph(graph)`가 대신 불러 준다. 새 그래프에 없는 슬롯의 바인딩은 조용히 버리지
않고 뒤에 남는다 — 그래프를 잘못 바꿨다가 되돌렸을 때 손으로 채운 참조가 사라져 있으면 안 된다.

`SlotRef.Self`는 예약된 이름이라 목록에 뜨지 않는다 — 언제나 플레이어가 붙은 오브젝트 자신을
가리키고, 대부분의 노드가 `Target`의 기본값으로 쓴다. 그래서 한 오브젝트만 움직이는 흔한 그래프는
바인딩을 하나도 채우지 않아도 돈다.

꽂힌 오브젝트가 요구 타입이 아니면 `GetComponent`로 한 번 더 찾아본다. 인스펙터에 무엇을 끌어다
놓든 의도대로 동작하게 하기 위해서다. 그래도 없으면 그 노드만 건너뛰며 경고를 **한 번** 남긴다 —
방치형에서 매 프레임 경고가 나오면 진짜 문제를 찾을 수 없다.

## 검증

```bash
dotnet test "Tests~/dotnet/UiMotion.Core.Tests.csproj"   # 코어 — Unity 없이 돈다
./Tools~/compile-check/run.sh                            # Unity 계층 컴파일 게이트
```

첫 줄은 CI가 모든 푸시마다 돌린다. 둘째 줄은 설치된 Unity의 매니지드 DLL을 참조해 `Runtime`
전체를 `dotnet build`로 컴파일한다 — 1초 안에 끝나므로 에디터를 열기 전에 오타와 타입 오류가
전부 잡힌다. Unity의 DLL은 재배포할 수 없어 이 게이트는 CI가 아니라 로컬이다.
**Unity 파일을 건드린 커밋은 이것을 통과해야 한다.**

`Tests~`와 `Tools~`의 `~`는 Unity가 이 폴더를 무시하게 만드는 UPM 관례다.

## 노드 추가하기

세 가지를 갖추면 어떤 노드든 추가할 수 있다.

1. 베이스를 상속하고 `OnPlay`를 채운다 — Unity 대상을 만지면 `UnityEffectNode`,
   실행 순서를 정하는 흐름 노드면 코어의 `MotionFlowNode`다
2. `[MotionNode]`로 이름 · 분류 · 설명을 단다
3. `Sample`이 가리키는 작동 예시 그래프를 둔다

설명이나 예시가 없으면 팔레트의 "미검증" 섹션으로 격리된다. 쓰는 것 자체는 막지 않지만
CI 게이트에서 실패한다 — 배포된 것은 100% 문서화됨을 보장하기 위해서다.

```csharp
[MotionNode(
    Name     = "Tilt",
    Category = "Transform",
    Summary  = "대상을 잠깐 기울인다. 버튼을 눌렀을 때의 작은 반응에 쓴다.",
    Sample   = "Tilt")]
[Serializable]
public sealed class TiltNode : UnityEffectNode
{
    [MotionSlot(typeof(RectTransform))]
    public SlotRef Target = SlotRef.Self;

    [MotionParam(Label = "각도", Min = -45f, Max = 45f)]
    public float Angle = 8f;

    [MotionParam(Label = "시간", Min = 0f, Max = 5f)]
    public float Duration = 0.15f;

    public EaseKind Ease = EaseKind.OutCubic;

    public override bool Reverts => true;

    protected override IMotionHandle OnPlay(IMotionContext ctx)
    {
        RectTransform target = Resolve<RectTransform>(ctx, Target);
        if (target == null)
        {
            return MotionHandle.Skipped;
        }

        Vector3 from = target.localEulerAngles;
        Remember(ctx, () => target.localEulerAngles = from);

        return Run(ctx, Duration, Ease, t =>
            target.localEulerAngles = from + new Vector3(0f, 0f, Angle * t));
    }
}
```

`UnityEffectNode`가 주는 것이 그 셋이다 — `Resolve<T>`(슬롯을 원하는 타입으로, 실패하면
`null`), `Remember`(취소될 때 되돌릴 것), `Run`(호스트에 꽂힌 트윈 백엔드로 이징된 진행률을
흘린다. 백엔드가 없으면 내장 러너로 폴백하므로 `null`을 만날 경로가 없다).

`Remember`로 등록한 것은 **자연 완료 시에는 실행되지 않는다.** 페이드인이 끝나자마자 다시
투명해지면 안 되기 때문이다. 되돌림은 "중간에 끊겼다"의 처리다.

`[MotionSlot]`과 `[MotionParam]`은 UnityEngine의 `[SerializeField]` · `[Tooltip]` ·
`[Range]`를 대신한다 — 같은 노드 규약을 코어에서도 쓸 수 있어야 하기 때문이다.

## 알려진 한계

- `RepeatNode`는 사이클 완료 후 남은 시간을 다음 사이클로 넘기지 않는다. 넘기려면
  `NodeRun`이 소비한 시간을 보고해야 하는데 실행 모델 전체를 건드리는 변경이다.
  UI 장식 루프에서 사이클당 최대 한 프레임 손실은 눈에 보이지 않는다
- `SubGraphNode`의 런타임 중첩 깊이는 8로 제한된다. 에디터의 순환 검사와 별개로
  손으로 만든 순환 에셋에 대한 방어다

## 설계 문서

`docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`
