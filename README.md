# UI Motion (com.juahn.v2.uimotion)

JuahnFrameworkV2 스택의 UI 연출 패키지. 노드 그래프로 UI 움직임을 조립하고,
그 결과를 에셋 하나로 저장해 어디에든 재사용한다.

**현재 상태:** 순수 C# 코어 실행 엔진만 구현됨. Unity 계층(ScriptableObject ·
MonoBehaviour · 트윈 러너 · 효과 노드)과 에디터 툴은 후속 작업이다.

## 어셈블리 두 개

| 어셈블리 | 의존 | 내용 |
|---|---|---|
| `juahn.v2.UiMotion.Core` | 없음 (`noEngineReferences`) | 그래프 실행 엔진 · 스코프 · 흐름 노드 · 이징 · 순환 검출 |
| `juahn.v2.UiMotion` | Core | ScriptableObject · MonoBehaviour · 트윈 러너 · 효과 노드 (후속) |

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

## 코어 테스트 실행

```bash
dotnet test "Tests~/dotnet/UiMotion.Core.Tests.csproj"
```

Unity 없이 돈다. `Tests~`의 `~`는 Unity가 이 폴더를 무시하게 만드는 UPM 관례다.

## 노드 추가하기

세 가지를 갖추면 어떤 노드든 추가할 수 있다.

1. `MotionEffectNode`(무언가를 움직인다) 또는 `MotionFlowNode`(실행 순서를 정한다)를
   상속하고 `OnPlay`를 채운다
2. `[MotionNode]`로 이름 · 분류 · 설명을 단다
3. `Sample`이 가리키는 작동 예시 그래프를 둔다

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

`[MotionSlot]`과 `[MotionParam]`은 UnityEngine의 `[SerializeField]` · `[Tooltip]` ·
`[Range]`를 대신한다 — 코어에서 그것들을 쓸 수 없기 때문이다.

## 알려진 한계

- `RepeatNode`는 사이클 완료 후 남은 시간을 다음 사이클로 넘기지 않는다. 넘기려면
  `NodeRun`이 소비한 시간을 보고해야 하는데 실행 모델 전체를 건드리는 변경이다.
  UI 장식 루프에서 사이클당 최대 한 프레임 손실은 눈에 보이지 않는다
- `SubGraphNode`의 런타임 중첩 깊이는 8로 제한된다. 에디터의 순환 검사와 별개로
  손으로 만든 순환 에셋에 대한 방어다

## 설계 문서

`docs/superpowers/specs/2026-09-04-ui-motion-graph-design.md`
