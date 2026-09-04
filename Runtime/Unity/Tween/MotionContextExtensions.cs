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
