using Unity.Behavior.GraphFramework;
using System;

namespace Unity.Behavior
{
    internal class RepeatNodeTransformer : INodeTransformer
    {
        public Type NodeModelType => typeof(RepeatNodeModel);

        public Node CreateNodeFromModel(GraphAssetProcessor graphAssetProcessor, NodeModel nodeModel)
        {
            RepeatNodeModel repeatNodeModel = nodeModel as RepeatNodeModel;
            Node node = Activator.CreateInstance(repeatNodeModel.NodeType) as Node;

            return node;
        }

        public void ProcessNode(GraphAssetProcessor graphAssetProcessor, NodeModel nodeModel, Node node)
        {
            RepeatNodeModel repeatNodeModel = nodeModel as RepeatNodeModel;
            DefaultNodeTransformer.ProcessNodeFields(graphAssetProcessor, nodeModel, node);
            if (node is IRepeater repeater)
            {
                repeater.AllowMultipleRepeatsPerTick = repeatNodeModel.AllowMultipleRepeatsPerTick;
            }
            
        }
    }
}