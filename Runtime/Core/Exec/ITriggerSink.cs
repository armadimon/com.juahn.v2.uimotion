namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 다른 트리거를 건드리는 통로. <see cref="MotionRuntime"/> 전체를 노출하지 않고
    /// 필요한 두 동작만 뚫는다.
    /// </summary>
    public interface ITriggerSink
    {
        void Fire(string trigger);

        void Stop(string trigger);
    }
}
