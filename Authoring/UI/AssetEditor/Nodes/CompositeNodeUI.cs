using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeUI(typeof(CompositeNodeModel))]
    internal class CompositeNodeUI : BehaviorNodeUI
    {
        private CompositeNodeModel CompositeNodeModel { get; set; }

        public CompositeNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            AddToClassList("Composite");
            BehaviorGraphNodeModel behaviorGraphNodeModel = nodeModel as BehaviorGraphNodeModel;
            if (behaviorGraphNodeModel == null)
            {
                return;
            }
            
            CompositeNodeModel = nodeModel as CompositeNodeModel;
            
            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(behaviorGraphNodeModel.NodeTypeID);
            InitFromNodeInfo(nodeInfo);
        }

        public override void Refresh(bool isDragging)
        {
            base.Refresh(isDragging);

            if (CompositeNodeModel.UpdatedPorts)
            {
                AlignImmediateChildren();
                CompositeNodeModel.UpdatedPorts = false;
            }
        }

        private void AlignImmediateChildren()
        {
            this.schedule.Execute(_ =>
            {
                var nodePositions = GraphUILayoutUtility.ComputeChildNodePositions(this);
                GraphUILayoutUtility.ScheduleNodeMovement(this, Model.Asset, nodePositions);
            });
        }
    }
}
