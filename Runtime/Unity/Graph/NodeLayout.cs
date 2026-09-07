using System;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프 창에서 노드가 놓인 자리. <b>순전히 에디터용 데이터다</b> —
    /// <see cref="MotionGraphIndex"/>는 이것을 읽지 않고, 실행에 아무 영향이 없다.
    ///
    /// 별도 에셋으로 빼지 않는 이유는 두 파일이 어긋날 수 있기 때문이다.
    /// 노드를 지웠는데 위치 파일만 남거나 그 반대가 되면 창이 이상해진다.
    /// </summary>
    [Serializable]
    public struct NodeLayout
    {
        public NodeId Node;
        public Vector2 Position;

        public NodeLayout(NodeId node, Vector2 position)
        {
            Node = node;
            Position = position;
        }
    }
}
