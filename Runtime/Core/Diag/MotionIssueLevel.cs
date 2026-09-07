namespace Juahn.UiMotion
{
    /// <summary>검사 결과의 심각도. 값이 클수록 심각하다 — 정렬에 쓴다.</summary>
    public enum MotionIssueLevel
    {
        /// <summary>알아 두면 좋은 것. 동작에는 문제가 없다.</summary>
        Info = 0,

        /// <summary>의도와 다르게 동작할 가능성이 높다. 실행은 된다.</summary>
        Warning = 1,

        /// <summary>확실히 잘못됐다. 배포 전에 고쳐야 한다.</summary>
        Error = 2,
    }
}
