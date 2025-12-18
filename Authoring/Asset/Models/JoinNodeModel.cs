using System;

namespace Unity.Behavior
{
    [NodeModelInfo(typeof(WaitForAnyComposite))]
    [NodeModelInfo(typeof(WaitForAllComposite))]
    [Serializable]
    internal class JoinNodeModel : BehaviorGraphNodeModel
    {
        public override int MaxInputsAccepted => int.MaxValue;

        public JoinNodeModel(NodeInfo nodeInfo) : base(nodeInfo) { }

        protected JoinNodeModel(JoinNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
        }
    }
}
