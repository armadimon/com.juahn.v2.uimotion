# 재현 하네스

`docs/ugui-cost.md`의 수치를 낸 것들이다. **이 패키지의 일부가 아니다** — 측정을 다시
하고 싶을 때 쓰는 재료이므로, 여기 두고 Unity가 임포트하지 않게 확장자 그대로 둔다.

## 다시 재는 법

1. 빈 Unity 프로젝트를 만든다. `manifest.json`을 그 프로젝트의 `Packages/`에 넣는다
   (`com.unity.ugui`와 `com.unity.test-framework`만 있으면 된다).
2. `CanvasCostTests.cs`와 `CanvasBench.Tests.asmdef`를 `Assets/Tests/`에 넣는다.
3. 돌린다.

```
unity test --mode PlayMode --output ./results.xml
```

4. 결과는 NUnit 리포트가 아니라 **에디터 로그**에 있다. `[[CANVAS-COST]]`로 찾는다.

```
grep -A 14 "CANVAS-COST" Logs/Editor.log
```

## 왜 빈 프로젝트인가

캔버스 비용은 프로젝트와 무관하고, 실제 게임 프로젝트에서 재면 다른 시스템의
캔버스가 섞여 들어온다. 빈 프로젝트가 더 정확하다.

## 마커 이름을 추측하지 않는다

`A0_DumpMarkers` 테스트가 등록된 UI 관련 마커를 전부 찍는다. 카테고리를 잘못 짚으면
`ProfilerRecorder`가 **조용히 0을 돌려주고**, 그러면 "비용이 없다"는 잘못된 결론이
나온다. 실제로 처음에는 `Canvas.SendWillRenderCanvases`를 찾다가 아무것도 못 잡았는데,
진짜 이름은 `UIEvents.WillRenderCanvases`였다.
