using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 모든 노드의 베이스. 직접 상속하지 말고 <see cref="MotionEffectNode"/>(무언가를 움직인다)나
    /// <see cref="MotionFlowNode"/>(실행 순서를 정한다) 중 하나를 고른다.
    ///
    /// <b>UnityEngine을 참조하지 않는다.</b> Unity 직렬화에 필요한 <see cref="SerializableAttribute"/>와
    /// public 필드는 순수 C#이라 이 어셈블리에 둘 수 있다. 인스펙터 표시는
    /// <c>[MotionParam]</c>이 대신한다(후속 태스크).
    /// </summary>
    [Serializable]
    public abstract class MotionNodeBase
    {
        /// <summary>그래프 안에서의 식별자. 그래프가 채운다.</summary>
        public NodeId Id;

        /// <summary>
        /// 자식 실행을 이 노드가 직접 조율하는가.
        ///
        /// false(기본)면 실행기가 이 노드의 핸들이 끝난 뒤 자식들을 <b>동시에</b> 시작한다.
        /// true면 실행기가 손대지 않고 노드가 알아서 한다 — <c>Sequence</c>·<c>Parallel</c>·<c>Repeat</c>가 그렇다.
        /// </summary>
        public virtual bool OwnsChildren => false;

        /// <summary>
        /// 중단되면 대상을 원래 상태로 되돌리는가. <b>엔진은 이 값을 읽지 않는다</b> —
        /// 되돌릴지는 노드가 <c>ctx.Scope.Remember</c>를 부르는지로 정해진다.
        /// 이 속성은 에디터와 Node Doctor가 표시용으로 쓴다.
        /// </summary>
        public virtual bool Reverts => false;

        /// <summary>
        /// 이 노드를 시작한다. <b>절대 null을 돌려주지 않는다</b> —
        /// 파생이 null을 주면 <see cref="MotionHandle.Completed"/>로 바꾼다.
        ///
        /// 실행기가 넘기는 <paramref name="ctx"/>는 <b>항상 non-null</b>이다.
        /// null을 넘기는 것은 문맥을 쓰지 않는 노드를 테스트할 때뿐이다.
        /// </summary>
        public IMotionHandle Play(IMotionContext ctx)
        {
            IMotionHandle handle = OnPlay(ctx);
            return handle ?? MotionHandle.Completed;
        }

        /// <summary>파생이 채운다. 실제로 무엇을 시작할지.</summary>
        protected abstract IMotionHandle OnPlay(IMotionContext ctx);
    }
}
