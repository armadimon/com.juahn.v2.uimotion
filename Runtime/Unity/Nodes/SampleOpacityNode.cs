using System;
using UnityEngine;
using UnityEngine.UI;
namespace Juahn.UiMotion
{
    [Serializable,MotionNode(Name="Sample Opacity",Category="UI",Summary="게임 진행값을 알파로 샘플링한다. 시간·단계는 호출자가 소유한다.")]
    public sealed class SampleOpacityNode : UnityEffectNode
    {
        [MotionSlot(typeof(Component))] public SlotRef Target=SlotRef.Self;
        [MotionSlot(typeof(MotionValue))] public SlotRef Progress=new SlotRef("Progress");
        public override bool Reverts=>true;
        public override bool BlocksChildren=>true;
        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            var target=Resolve<Component>(ctx,Target);var progress=Resolve<MotionValue>(ctx,Progress);
            if(target==null || progress==null)return MotionHandle.Skipped;
            var group=target as CanvasGroup;if(group==null)group=target.GetComponent<CanvasGroup>();
            var graphic=target as Graphic;if(graphic==null)graphic=target.GetComponent<Graphic>();
            if(group==null && graphic==null)return MotionHandle.Skipped;
            var original=group!=null?group.alpha:graphic.color.a;
            void Apply(float value)
            {
                if(group!=null)group.alpha=value;
                else if(graphic!=null){var color=graphic.color;color.a=value;graphic.color=color;}
            }
            Remember(ctx,()=>Apply(original));Apply(Mathf.Clamp01(progress.Value));
            return MotionHandle.Forever(_=>{if(progress!=null)Apply(Mathf.Clamp01(progress.Value));});
        }
    }
}
