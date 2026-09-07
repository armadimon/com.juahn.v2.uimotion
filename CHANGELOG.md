# Changelog

## [0.1.0] - 미출시

### 변경 — 트리거가 그래프 안의 노드가 된다

- `TriggerNode`에 `Policy`를 실었다. 재발사 정책이 그래프의 트리거 목록과 노드 두 곳에
  나뉘어 있으면 어긋났을 때 무엇이 맞는지 알 수 없다
- `TriggerIntrospector` — 노드들에서 트리거 선언을 계산한다. `SlotIntrospector`가 슬롯에
  대해 하는 일과 같다. **트리거 목록은 저작값이 아니라 파생값이 된다.** 진입점은 트리거
  노드 자신이므로 노드를 지우면 트리거도 사라지고 죽은 id가 목록에 남지 않는다

### 추가

순수 C# 코어 실행 엔진 (`juahn.v2.UiMotion.Core`).

- `IMotionGraphView` — 실행기가 그래프를 보는 유일한 창구. 그래프는 실행 상태를 갖지 않는다
- `MotionScope` — 트리거 1회 발사의 실행 단위. 취소 시 등록의 역순으로 원상 복구
- `NodeRun` — 노드 하나와 자식 전파. 핸들이 끝나면 자식들을 동시에 시작한다
- `MotionRuntime` — 트리거 발사 창구와 `Start`/`Loop`/`End` 위상 규약
- `TriggerRunner` — `Restart`/`Ignore`/`Queue` 재발사 정책
- 흐름 노드 — `Trigger` · `Sequence` · `Parallel` · `Delay` · `Repeat` · `StopTrigger` · `SubGraph`
- `GraphCycleDetector` — 서브그래프 순환 검출. 다이아몬드는 순환이 아니다
- `EaseLibrary` — 13종 이징. 표준 정의 대조 회귀 고정 테스트 포함
- `OnceLogger` — 같은 키의 경고를 한 번만
- 오서링 어트리뷰트 — `[MotionNode]` · `[MotionSlot]` · `[MotionParam]`

`dotnet test` 기반 CI 게이트. Unity 라이선스 없이 모든 푸시마다 코어를 검증하고,
`Runtime/Core`에 UnityEngine 참조가 새어드는 것을 grep으로 막는다.

Unity 런타임 계층 (`juahn.v2.UiMotion`).

- `MotionGraph` — `[SerializeReference]` 노드 배열 + 평면 간선 목록으로 저장되는
  ScriptableObject. 조회 로직은 전부 순수 코어의 `MotionGraphIndex`에 있어 `dotnet test`로 검증된다
- `MotionGraphAuthoring` — 에디터가 쓰는 그래프 변경 API
- `MotionPlayer` — 프리팹에 붙는 유일한 컴포넌트. `Fire` · `Stop` · `WaitFor` ·
  `ClaimTriggerOwnership` · `SetGraph` · `SyncBindings`
- `MotionPump` — 전역 틱 펌프 하나가 모든 플레이어를 한 `Update`에서 돌린다.
  플레이어마다 `Update`를 두면 수백 개 동시 재생에서 네이티브-매니지드 경계 비용이 그만큼 곱해진다
- 슬롯 바인딩과 타입 해석 — `SlotBinding` · `SlotTable` · `MotionSlots`.
  요구 타입이 아니면 `GetComponent`로 한 번 더 찾고, 실패하면 노드를 건너뛰며 경고를 한 번만 남긴다
- 교체 가능한 트윈 백엔드 — `IMotionTweenRunner` · `BuiltinTweenRunner` · `ctx.Tween()`
- 효과 노드 13종 — Move · Scale · Rotate · Fade · Color · Punch Scale · Shake ·
  Float · Bounce · Fly Across · Set Active · Send Signal · Play Animator
- `UnityEffectNode` — 효과 노드의 공통 베이스. 슬롯 해석 · 원상 복구 등록 · 트윈 실행
- `SubGraphAssetNode` — 다른 `MotionGraph` 에셋을 가리키는 서브그래프
- `MotionSignals` — Send Signal 노드의 수신 지점
- `UnityMotionLog` — `IMotionLog`를 `Debug`로

순수 코어에 추가된 것.

- `MotionGraphIndex` · `SlotIntrospector` · `NodeLink`
- `IMotionContext.Host` — 효과 노드가 트윈 백엔드에 닿는 통로. `ISlotResolver`가 `object`를
  돌려주는 것과 같은 관용구로, 코어가 Unity 타입을 알지 않기 위해 타입을 잃는 지점을 하나로 모은다
- `MotionHandle.Forever` — Float · Bounce 같은 유지 연출용 무한 핸들

Unity 계층 컴파일 게이트 (`Tools~/compile-check`). 설치된 Unity의 매니지드 DLL을 참조해
`Runtime` 전체를 `dotnet build`로 컴파일한다. Unity DLL은 재배포할 수 없어 CI가 아니라 로컬이다.

### 수정

- `MotionGraphIndex`의 조회 결과와 `MotionGraph.Nodes` · `Links`를 읽기 전용으로 굳혀
  외부에서 그래프를 변형하는 경로를 없앴다. 그래프는 여러 오브젝트가 공유하는 불변 데이터다
- `MotionPump`가 순회 중 등록되는 플레이어와 경합하던 문제. 도메인 리로드를 끈 채 플레이를
  재시작하면 펌프가 되살아나지 않아 아무것도 재생되지 않던 문제
- 재생 중 바인딩을 바꾸면 런타임만 버려져 스코프의 원상 복구가 실행되지 못하고
  트윈이 어중간한 값에서 굳던 문제. `SyncBindings`와 `Bind`가 먼저 돌던 것을 걷어낸다
- `Play Animator`가 이전 Animator 상태를 보고 첫 틱에 완료 처리하던 문제

### 문서

- README에 저작 툴 패키지(`com.juahn.v2.uimotion.editor`) 안내를 넣었다. 이 패키지만으로는
  그래프를 코드로만 만들 수 있다
- 스펙의 열린 질문 3번(에디터 프리뷰의 대상과 유출 방지)을 닫았다. 프리뷰는 선택된
  `MotionPlayer` 그 자체를 대상으로 하고 임시 오브젝트를 만들지 않는다. 원상 복구만으로는
  프리뷰 중의 Ctrl+S를 막지 못하므로 멈춤 훅을 다섯 곳에 걸고 메뉴 항목을 하나 둔다
- 스펙 7.1의 "팔레트에서 노드에 호버하면 그 예시 그래프가 그 자리에서 재생된다"를 실제
  구현에 맞게 고쳤다. 재생에는 대상 오브젝트와 슬롯 바인딩이 필요하므로 팔레트는
  예시 그래프를 **여는 것**까지 한다
- README에 **패키지 넷의 지도**를 넣었다. 무엇이 필수이고 무엇이 선택인지, 각각이 무엇을
  요구하는지를 한 표로 모으고, UiService 브릿지와 DOTween 백엔드의 설치 절을 더했다.
  DOTween 절은 **설치가 두 단계**라는 것과 `UIMOTION_DOTWEEN` 심볼을 빼먹으면 오류 없이
  조용히 아무 일도 일어나지 않는다는 것을 함께 적었다
- 스펙 3.4를 실제 구현으로 고쳤다. `versionDefines`로 DOTween을 조건부로 만든다고 적혀
  있었는데 **그것은 동작하지 않는다** — DOTween은 에셋스토어 플러그인이라 UPM 패키지가
  아니고 `versionDefines`가 걸 이름이 없다. 실제로 쓴 `defineConstraints` +
  `overrideReferences` + `precompiledReferences`와 그 대가(조용한 실패)로 바꿨다.
  **DOTween 백엔드를 에디터 프리뷰에서 쓰지 않는 이유**도 같은 절에 적었다 — DOTween의
  업데이트 루프가 런타임 MonoBehaviour라 에디트 모드에서 돌지 않고, 그러면 핸들의
  `IsDone`이 영원히 false가 되어 프리뷰가 걸린다
- 스펙 6절에 **닫힘 완료원을 `OnPresenterClosed`에서 풀면 안 되는 이유**를 적었다.
  `InternalCloseProcessAsync`가 `NotifyFeaturesClosing()` 바로 다음 줄에서
  `NotifyFeaturesClosed()`를 부르고 `await`는 그보다 뒤에 오므로, 거기서 풀면 `End`
  연출이 한 프레임도 보이지 않는다. 브릿지에 그 오버라이드가 없는 것이 의도다.
  `OnDisable`·`OnDestroy`의 안전망과 `_waitForStart`/`_waitForEnd`도 함께 적었다
- 스펙 3.2·3.3의 어셈블리 참조 목록을 실제 asmdef에 맞췄다
