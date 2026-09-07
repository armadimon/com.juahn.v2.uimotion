using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CanvasBench
{
    /// <summary>
    /// uGUI 캔버스가 무엇에 얼마를 쓰는지 실제로 잰다.
    ///
    /// 통과/실패를 가리는 테스트가 아니라 측정이다. 연출 시스템 자체의 비용은 순수 C#
    /// 벤치마크로 쟀지만, 실제로 프레임을 잡아먹는 것은 그 연출이 건드린 결과로 캔버스가
    /// 다시 만들어지는 비용이다. 그것은 Unity 안에서만 잴 수 있다.
    ///
    /// 마커 이름과 카테고리를 추측하지 않는다. 실제로 등록된 마커를 열거해 찾는다 -
    /// 카테고리를 잘못 짚으면 ProfilerRecorder가 조용히 0을 돌려주고, 그러면
    /// "비용이 없다"는 잘못된 결론이 나온다.
    /// </summary>
    public sealed class CanvasCostTests
    {
        private const int Frames = 180;
        private const int WarmupFrames = 60;

        private Canvas _canvas;
        private RectTransform[] _items;
        private Image[] _images;
        private CanvasGroup _group;

        private static readonly StringBuilder Report = new StringBuilder();
        private static bool _markersDumped;

        private void Build(int items)
        {
            var canvasGo = new GameObject("MeasureCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _group = canvasGo.AddComponent<CanvasGroup>();

            _items = new RectTransform[items];
            _images = new Image[items];

            for (int i = 0; i < items; i++)
            {
                var go = new GameObject("Item" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(canvasGo.transform, false);

                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(40f, 40f);
                rect.anchoredPosition = new Vector2((i % 30) * 45f - 660f, (i / 30) * 45f - 400f);

                _items[i] = rect;
                _images[i] = go.GetComponent<Image>();
                _images[i].color = new Color(0.8f, 0.6f, 0.3f, 1f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(_canvas.gameObject);
            }
        }

        [OneTimeTearDown]
        public void PrintReport()
        {
            if (Report.Length > 0)
            {
                Debug.Log("[[CANVAS-COST]]\n" + Report);
            }
        }

        // 등록된 마커 중 UI/Canvas 관련을 전부 찍는다. 이름과 카테고리를 눈으로 확인하기 위해서다.
        [UnityTest]
        public IEnumerator A0_DumpMarkers()
        {
            Build(120);
            for (int f = 0; f < 30; f++)
            {
                _items[0].localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(f * 0.3f));
                Color c = _images[0].color; c.a = 0.5f + 0.4f * Mathf.Sin(f * 0.3f); _images[0].color = c;
                yield return null;
            }

            if (!_markersDumped)
            {
                _markersDumped = true;
                var found = new List<string>();
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);

                for (int i = 0; i < handles.Count; i++)
                {
                    ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handles[i]);
                    string n = d.Name;
                    if (n != null && (n.Contains("Canvas") || n.Contains("UI.") || n.StartsWith("UGUI")))
                    {
                        found.Add(n + "  [" + d.Category.Name + "]");
                    }
                }

                found.Sort();
                Debug.Log("[[MARKERS]] " + found.Count + "개\n" + string.Join("\n", found));
            }

            Assert.Pass();
        }

        [UnityTest] public IEnumerator B_Baseline_120() { Build(120); yield return Measure("기준 120칸 (변화 없음)", null); }

        [UnityTest]
        public IEnumerator C_Scale1_of120()
        {
            Build(120);
            yield return Measure("캔버스 120칸 / 1개 스케일", Scale(1));
        }

        [UnityTest]
        public IEnumerator D_Scale30_of120()
        {
            Build(120);
            yield return Measure("캔버스 120칸 / 30개 스케일", Scale(30));
        }

        [UnityTest]
        public IEnumerator E_Scale1_of30()
        {
            Build(30);
            yield return Measure("캔버스 30칸 / 1개 스케일", Scale(1));
        }

        [UnityTest]
        public IEnumerator F_Scale1_of600()
        {
            Build(600);
            yield return Measure("캔버스 600칸 / 1개 스케일", Scale(1));
        }

        [UnityTest]
        public IEnumerator G_Scale30_of600()
        {
            Build(600);
            yield return Measure("캔버스 600칸 / 30개 스케일", Scale(30));
        }

        [UnityTest]
        public IEnumerator H_GroupAlpha_120()
        {
            Build(120);
            yield return Measure("캔버스 120칸 / CanvasGroup.alpha", delegate(float t)
            {
                _group.alpha = 0.5f + 0.5f * Mathf.Sin(t * 6f);
            });
        }

        [UnityTest]
        public IEnumerator I_Color1_of120()
        {
            Build(120);
            yield return Measure("캔버스 120칸 / 1개 color", Color1());
        }

        [UnityTest]
        public IEnumerator J_Color30_of120()
        {
            Build(120);
            yield return Measure("캔버스 120칸 / 30개 color", Color30());
        }

        // 움직이는 30칸만 자식 Canvas로 떼어낸다. 표준 대응책이 실제로 듣는지 본다.
        [UnityTest]
        public IEnumerator K_SubCanvas_30of600()
        {
            Build(600);

            var sub = new GameObject("SubCanvas", typeof(RectTransform), typeof(Canvas));
            sub.transform.SetParent(_canvas.transform, false);

            for (int i = 0; i < 30; i++)
            {
                _items[i].SetParent(sub.transform, true);
            }

            yield return Measure("캔버스 600칸 / 30개를 자식Canvas로", Scale(30));
        }

        private Action<float> Scale(int count)
        {
            return delegate(float t)
            {
                float s = 1f + 0.2f * Mathf.Sin(t * 6f);
                var v = new Vector3(s, s, 1f);
                for (int i = 0; i < count && i < _items.Length; i++) { _items[i].localScale = v; }
            };
        }

        private Action<float> Color1()
        {
            return delegate(float t)
            {
                Color c = _images[0].color; c.a = 0.5f + 0.5f * Mathf.Sin(t * 6f); _images[0].color = c;
            };
        }

        private Action<float> Color30()
        {
            return delegate(float t)
            {
                float a = 0.5f + 0.5f * Mathf.Sin(t * 6f);
                for (int i = 0; i < 30 && i < _images.Length; i++)
                {
                    Color c = _images[i].color; c.a = a; _images[i].color = c;
                }
            };
        }

        private static ProfilerRecorder Find(string markerName, int capacity)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);

            for (int i = 0; i < handles.Count; i++)
            {
                if (ProfilerRecorderHandle.GetDescription(handles[i]).Name == markerName)
                {
                    var recorder = new ProfilerRecorder(handles[i], capacity);
                    recorder.Start();
                    return recorder;
                }
            }

            return default;
        }

        private IEnumerator Measure(string name, Action<float> mutate)
        {
            for (int f = 0; f < WarmupFrames; f++)
            {
                if (mutate != null) { mutate(f * 0.016f); }
                yield return null;
            }

            string[] names =
            {
                "UIEvents.WillRenderCanvases",
                "PostLateUpdate.PlayerUpdateCanvases",
                "Canvas.BuildBatch",
                "UGUI.Rendering.UpdateBatches",
            };

            var recorders = new ProfilerRecorder[names.Length];
            var totals = new long[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                recorders[i] = Find(names[i], Frames);
            }

            for (int f = 0; f < Frames; f++)
            {
                if (mutate != null) { mutate((WarmupFrames + f) * 0.016f); }
                yield return null;

                for (int i = 0; i < names.Length; i++)
                {
                    if (recorders[i].Valid && recorders[i].LastValue > 0)
                    {
                        totals[i] += recorders[i].LastValue;
                    }
                }
            }

            var line = new StringBuilder();
            line.Append(name.PadRight(36));

            for (int i = 0; i < names.Length; i++)
            {
                line.Append(" | " + Us(totals[i], Frames, recorders[i].Valid).PadLeft(9));
                if (recorders[i].Valid) { recorders[i].Dispose(); }
            }

            if (Report.Length == 0)
            {
                Report.AppendLine("".PadRight(36) + " | 리빌드     | 캔버스갱신 | BuildBatch | 배치갱신");
            }

            Report.AppendLine(line.ToString());

            Assert.Pass();
        }

        private static string Us(long totalNs, int frames, bool valid)
        {
            return valid ? (totalNs / 1000.0 / frames).ToString("F2") + " us" : "마커없음";
        }
    }
}
