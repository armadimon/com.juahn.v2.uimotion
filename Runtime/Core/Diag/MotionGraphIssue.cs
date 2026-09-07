namespace Juahn.UiMotion
{
    /// <summary>
    /// 검사에서 나온 문제 하나. 그래프 창이 이것을 노드 위에 배지로 그리고
    /// Node Doctor가 표로 나열한다.
    /// </summary>
    public struct MotionGraphIssue
    {
        public MotionIssueLevel Level;

        /// <summary>문제가 붙은 노드. 그래프 전체의 문제면 <see cref="NodeId.None"/>.</summary>
        public NodeId Node;

        public string Message;

        public MotionGraphIssue(MotionIssueLevel level, NodeId node, string message)
        {
            Level = level;
            Node = node;
            Message = message;
        }

        public override string ToString()
        {
            string where = Node.IsValid ? " " + Node : "";
            return Level + where + ": " + Message;
        }
    }
}
