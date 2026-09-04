namespace Juahn.UiMotion
{
    /// <summary>
    /// 재생 중인 무언가. 노드 하나의 효과일 수도 있고, 흐름 노드가 조율하는 자식 묶음일 수도 있다.
    ///
    /// 시간은 <see cref="Tick"/>으로 바깥에서 들어온다. 핸들이 스스로 시계를 읽지 않는다.
    /// </summary>
    public interface IMotionHandle
    {
        /// <summary>끝났는가. <b>취소된 핸들도 완료로 취급한다</b> — 실행기는 둘을 구분하지 않는다.</summary>
        bool IsDone { get; }

        /// <summary>시간을 진행시킨다. <see cref="IsDone"/>이면 아무 일도 하지 않아야 한다.</summary>
        void Tick(float deltaSeconds);

        /// <summary>즉시 끊는다. 이미 끝났으면 아무 일도 하지 않아야 한다.</summary>
        void Cancel();
    }
}
