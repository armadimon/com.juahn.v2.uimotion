using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// <see cref="SlotRef"/> 필드가 요구하는 타입을 선언한다. 인스펙터가 이 타입으로
    /// 드래그 대상을 걸러 준다.
    ///
    /// 코어는 이 값을 검사하지 않고 나르기만 한다 — Unity 타입을 알 수 없기 때문이다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class MotionSlotAttribute : Attribute
    {
        public MotionSlotAttribute(Type requiredType)
        {
            RequiredType = requiredType;
        }

        public Type RequiredType { get; }
    }
}
