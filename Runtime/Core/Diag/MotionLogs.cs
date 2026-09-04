namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 <see cref="IMotionLog"/> 하나만 들고도 억제된 경고를 낼 수 있게 하는 헬퍼.
    ///
    /// <see cref="IMotionContext.Log"/>의 타입은 <see cref="IMotionLog"/>라 억제 통로가 없다.
    /// 그런데 <see cref="MotionRuntime"/>이 넘기는 실제 인스턴스는 <see cref="OnceLogger"/>다.
    /// 인터페이스를 넓히는 대신 여기서 한 번 내려찍는다.
    /// </summary>
    public static class MotionLogs
    {
        /// <summary>
        /// <paramref name="key"/>가 처음일 때만 경고한다. 억제를 모르는 로그면 그냥 경고한다 —
        /// 조용한 실패보다 시끄러운 편이 낫다.
        /// </summary>
        public static void WarnOnce(IMotionLog log, string key, string message)
        {
            if (log == null)
            {
                return;
            }

            var once = log as OnceLogger;
            if (once != null)
            {
                once.WarnOnce(key, message);
                return;
            }

            log.Warn(message);
        }
    }
}
