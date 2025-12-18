using System;

namespace Unity.Behavior
{
    [Serializable]
    internal class ModifierNodeModel : BehaviorGraphNodeModel
    {
        public ModifierNodeModel(NodeInfo nodeInfo) : base(nodeInfo) { }

        protected ModifierNodeModel(ModifierNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
        }
    }
}
