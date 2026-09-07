namespace Juahn.UiMotion
{
    /// <summary>
    /// 효과 노드가 트윈 러너에 닿는 통로.
    ///
    /// 코어의 <see cref="IMotionContext"/>는 트윈을 모른다. 대신 <c>Host</c>를 나르는데,
    /// Unity 계층은 거기에 <see cref="MotionPlayer"/>를 넣는다. 여기서 그것을 꺼낸다.
    ///
    /// 호스트가 없거나 러너가 꽂히지 않았으면 <see cref="BuiltinTweenRunner"/>로 폴백한다 —
    /// 노드가 null을 만나 죽는 경로를 아예 없앤다.
    /// </summary>
    public static class MotionContextExtensions
    {
        /// <summary>
        /// 대상의 <b>제자리 크기</b>. 호스트가 기억한다.
        ///
        /// 호스트가 없으면 지금 크기를 그대로 돌려준다. 그 경우는 기억할 곳이 없으므로
        /// 반복 재생에서 크기가 흘러내릴 수 있는데, 실행기 밖에서 만든 문맥에서만
        /// 일어난다 — 런타임 경로는 언제나 <see cref="MotionPlayer"/>를 호스트로 넣는다.
        /// </summary>
        public static UnityEngine.Vector3 BaseScale(this IMotionContext ctx, UnityEngine.Transform target)
        {
            if (target == null)
            {
                return UnityEngine.Vector3.one;
            }

            var player = ctx == null ? null : ctx.Host as MotionPlayer;
            return player == null ? target.localScale : player.BaseScaleOf(target);
        }

        public static IMotionTweenRunner Tween(this IMotionContext ctx)
        {
            if (ctx == null)
            {
                return BuiltinTweenRunner.Shared;
            }

            var player = ctx.Host as MotionPlayer;
            if (player == null || player.TweenRunner == null)
            {
                return BuiltinTweenRunner.Shared;
            }

            return player.TweenRunner;
        }
    }
}
