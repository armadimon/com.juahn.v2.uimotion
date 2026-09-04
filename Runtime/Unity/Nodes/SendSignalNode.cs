using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    [MotionNode(
        Name = "Send Signal",
        Category = "Signal",
        Summary = "문자열 신호를 밖으로 던진다. 소리와 진동은 프로젝트가 이 신호를 받아 처리한다. 즉시 끝난다.",
        Sample = "SendSignal")]
    [Serializable]
    public sealed class SendSignalNode : UnityEffectNode
    {
        [MotionParam(Label = "신호", Tooltip = "예: sfx:click, haptic:light. 규약은 프로젝트가 정한다.")]
        public string Signal = "";

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (string.IsNullOrEmpty(Signal))
            {
                return MotionHandle.Skipped;
            }

            // 신호를 낸 오브젝트를 함께 넘긴다. 받는 쪽이 "어느 버튼이 눌렸나"를 알아야
            // 위치 기반 사운드나 화면 흔들림을 붙일 수 있다.
            var player = ctx == null ? null : ctx.Host as MotionPlayer;
            GameObject source = player == null ? null : player.gameObject;

            MotionSignals.Emit(Signal, source);
            return MotionHandle.Completed;
        }
    }
}
