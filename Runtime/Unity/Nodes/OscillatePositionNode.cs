using System;
using UnityEngine;
namespace Juahn.UiMotion
{
    [Serializable, MotionNode(Name = "Oscillate Position", Category = "Transform", Summary = "시각 자식의 로컬 위치만 왕복시킨다. 게임 위치와 독립적인 Loop 연출이다.")]
    public sealed class OscillatePositionNode : UnityEffectNode
    {
        [MotionSlot(typeof(Transform))] public SlotRef Target=SlotRef.Self;
        public Vector3 Amplitude=new Vector3(.025f,0,0);
        public float Period=.13f;
        public override bool Reverts=>true;
        public override bool BlocksChildren=>true;
        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            var target=Resolve<Transform>(ctx,Target);if(target==null)return MotionHandle.Skipped;
            var origin=target.localPosition;Remember(ctx,()=>{if(target!=null)target.localPosition=origin;});
            return MotionHandle.Forever(elapsed=>{if(target!=null)target.localPosition=origin+Amplitude*Mathf.Sin(elapsed*2f*Mathf.PI/Mathf.Max(.01f,Period));});
        }
    }
}
