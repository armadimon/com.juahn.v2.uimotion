using System;

namespace Juahn.UiMotion
{
    /// <summary>그래프가 선언한 트리거 하나.</summary>
    [Serializable]
    public sealed class TriggerDeclaration
    {
        public string Name;
        public TriggerPolicy Policy;
        public NodeId Entry;

        public TriggerDeclaration()
        {
        }

        public TriggerDeclaration(string name, NodeId entry, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            Name = name;
            Entry = entry;
            Policy = policy;
        }
    }
}
