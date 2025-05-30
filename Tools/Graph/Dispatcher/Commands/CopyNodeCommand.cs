using System.Collections.Generic;

namespace Unity.Behavior.GraphFramework
{
    internal class CopyNodeCommand : Command
    {
        public List<NodeModel> NodeModels { get; }

        public CopyNodeCommand(bool markUndo = true) : base(markUndo)
        {
            NodeModels = new List<NodeModel>();
        }
        
        public CopyNodeCommand(NodeModel nodeModel, bool markUndo = false) : this(markUndo)
        {
            NodeModels.Add(nodeModel);
        }
        
        public CopyNodeCommand(IEnumerable<NodeModel> nodeModels, bool markUndo = false) : this(markUndo)
        {
            NodeModels.AddRange(nodeModels);
            SetAssetDirty = false;
        }
    }
}