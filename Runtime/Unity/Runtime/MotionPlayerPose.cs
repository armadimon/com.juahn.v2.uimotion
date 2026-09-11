using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Juahn.UiMotion
{
    public sealed partial class MotionPlayer
    {
        private readonly Dictionary<Transform, Pose> _basePoses = new Dictionary<Transform, Pose>();
        private readonly Dictionary<CanvasGroup, float> _baseAlphas = new Dictionary<CanvasGroup, float>();
        private readonly Dictionary<Graphic, Color> _baseColors = new Dictionary<Graphic, Color>();
        private readonly struct Pose
        {
            public readonly Vector3 Position, Scale;
            public readonly Quaternion Rotation;
            public readonly Vector2 AnchoredPosition;
            public Pose(Transform target) { Position = target.localPosition; Scale = target.localScale; Rotation = target.localRotation; AnchoredPosition = target is RectTransform rect ? rect.anchoredPosition : Vector2.zero; }
            public void Restore(Transform target) { target.localPosition = Position; target.localScale = Scale; target.localRotation = Rotation; if (target is RectTransform rect) rect.anchoredPosition = AnchoredPosition; }
        }
        /// <summary>레이아웃 확정 후, 모션 시작 전에 호출한다. 복귀할 제자리 상태를 새로 저장한다.</summary>
        public void CaptureBasePose()
        {
            StopAll();
            _basePoses.Clear(); _baseAlphas.Clear(); _baseColors.Clear();
            _baseScales?.Clear();
            CaptureTarget(transform);
            foreach (var binding in _bindings)
            {
                if (binding.Target is Component component) CaptureTarget(component.transform);
                else if (binding.Target is GameObject go) CaptureTarget(go.transform);
            }
        }
        public Vector2 BaseAnchoredPositionOf(RectTransform target)
        {
            CaptureTarget(target);
            return target == null ? Vector2.zero : _basePoses[target].AnchoredPosition;
        }
        private void CaptureTarget(Transform target)
        {
            if (target == null || _basePoses.ContainsKey(target)) return;
            _basePoses.Add(target, new Pose(target));
            BaseScaleOf(target);
            if (target.TryGetComponent<CanvasGroup>(out var group)) _baseAlphas[group] = group.alpha;
            if (target.TryGetComponent<Graphic>(out var graphic)) _baseColors[graphic] = graphic.color;
        }
        /// <summary>StopAll과 별도로 자연 완료 뒤의 값까지 복구한다. 비활성화·풀 반환에서 호출된다.</summary>
        public void ResetToBasePose()
        {
            StopAll();
            foreach (var pair in _basePoses) if (pair.Key != null) pair.Value.Restore(pair.Key);
            foreach (var pair in _baseAlphas) if (pair.Key != null) pair.Key.alpha = pair.Value;
            foreach (var pair in _baseColors) if (pair.Key != null) pair.Key.color = pair.Value;
        }
    }
}
