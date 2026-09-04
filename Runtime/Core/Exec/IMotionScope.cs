using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 보는 스코프의 면. 실행 제어(Tick·Cancel)는 실행기의 몫이므로 여기 없다.
    /// </summary>
    public interface IMotionScope
    {
        /// <summary>
        /// 이 스코프가 <b>취소될 때</b> 실행할 복구 동작을 등록한다. 등록의 역순으로 실행된다.
        ///
        /// 자연 완료 시에는 실행되지 않는다 — 페이드인이 끝나자마자 다시 투명해지면 안 되기 때문이다.
        /// 되돌림은 "중간에 끊겼다"의 처리이지 "끝났다"의 처리가 아니다.
        /// </summary>
        void Remember(Action revert);
    }
}
