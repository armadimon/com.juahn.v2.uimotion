# Changelog

## [0.1.0] - 미출시

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
