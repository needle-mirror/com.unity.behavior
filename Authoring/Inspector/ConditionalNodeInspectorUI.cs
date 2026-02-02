using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
using Unity.AppUI.UI;
using UnityEngine.UIElements;
using UnityEngine;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(ConditionalGuardNodeModel))]
    [NodeInspectorUI(typeof(BranchingConditionNodeModel))]
    internal class ConditionalNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private IObserverAbortNodeModel m_ObserverAbortNodeModel => InspectedNode as IObserverAbortNodeModel;

        private string m_ConditionRequirementLabel;

        public ConditionalNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        public override void Refresh()
        {
            NodeProperties.Clear();

            if (InspectedNode is not IConditionalNodeModel conditionalNode)
            {
                return;
            }

            CreateObserverDropdownField();

            m_ConditionRequirementLabel = InspectedNode switch
            {
                ConditionalGuardNodeModel guardNode => GetGuardLabel(guardNode),
                BranchingConditionNodeModel => "Check if",
                _ => m_ConditionRequirementLabel
            };

            NodeProperties.Add(new ConditionRequirementElement(m_ConditionRequirementLabel, conditionalNode));
            NodeProperties.Add(new ConditionInspectorElement(conditionalNode));


            if (InspectedNode is ConditionalGuardNodeModel conditionalGuardNodeModel)
            {
                // Update title and description.
                NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(conditionalGuardNodeModel.NodeTypeID);
                RefreshNodeInformation(nodeInfo);
            }
        }

        private string GetGuardLabel(ConditionalGuardNodeModel guardModel)
        {
            return guardModel.ObserverType != ObserverAbortTarget.None
                ? "Guards and aborts if" 
                : "Guards if";
        }
    }
}
