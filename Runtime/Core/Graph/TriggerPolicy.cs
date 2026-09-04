namespace Juahn.UiMotion
{
    /// <summary>
    /// 이미 재생 중인 트리거를 다시 발사했을 때의 처리. 값은 직렬화되므로 바꾸지 않는다.
    /// </summary>
    public enum TriggerPolicy
    {
        /// <summary>돌던 것을 끊고 처음부터 다시. 기본값.</summary>
        Restart = 0,

        /// <summary>돌고 있으면 새 발사를 버린다.</summary>
        Ignore = 1,

        /// <summary>현재 것이 끝난 뒤에 실행한다.</summary>
        Queue = 2,
    }
}
