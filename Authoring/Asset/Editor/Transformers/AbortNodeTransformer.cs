using Unity.Behavior.GraphFramework;
using System;

namespace Unity.Behavior
{
    internal class AbortNodeTransformer : INodeTransformer
    {
        public Type NodeModelType => typeof(AbortNodeModel);

        public Node CreateNodeFromModel(GraphAssetProcessor graphAssetProcessor, NodeModel nodeModel)
        {
            AbortNodeModel abortNodeModel = nodeModel as AbortNodeModel;

            abortNodeModel.NodeType = abortNodeModel.ModelAbortType == AbortNodeModel.AbortType.Restart ? typeof(RestartModifier) : typeof(AbortModifier);

            // Ensure node model is up to date.
            abortNodeModel.OnValidate();

            // Create instance of the appropriate runtime type.
            Node node = Activator.CreateInstance(abortNodeModel.NodeType) as Node;

            return node;
        }

        public void ProcessNode(GraphAssetProcessor graphAssetProcessor, NodeModel nodeModel, Node node)
        {
            // Set the observer type on the runtime node
            if (node is IObserverAbort observerNode && nodeModel is IObserverAbortNodeModel observerNodeModel)
            {
                observerNode.AbortTarget = observerNodeModel.ObserverType;
            }

            if (node is IConditional conditionalNode)
            {
                DefaultNodeTransformer.ProcessNodeConditions(graphAssetProcessor, nodeModel, conditionalNode);
            }
        }
    }
}
