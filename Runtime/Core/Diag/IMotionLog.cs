namespace Juahn.UiMotion
{
    /// <summary>
    /// 코어가 진단을 내보내는 곳. 코어는 UnityEngine을 참조하지 않으므로
    /// <c>Debug.Log</c>를 직접 부를 수 없다 — Unity 계층이 구현을 꽂는다.
    /// </summary>
    public interface IMotionLog
    {
        void Warn(string message);

        void Error(string message);
    }
}
