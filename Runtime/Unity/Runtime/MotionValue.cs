using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>그래프가 보간하거나 게임이 제공한 진행값. 상태 판정이나 보상은 소유하지 않는다.</summary>
    public sealed class MotionValue : MonoBehaviour
    {
        [SerializeField] private float _value;
        public event Action<float> Changed;
        public float Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; Changed?.Invoke(value); }
        }
    }
}
