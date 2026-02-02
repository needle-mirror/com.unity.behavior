using System;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(ObserverAbortNodeModel))]
    internal class ObserverAbortNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private ObserverAbortNodeModel m_ObserverAbortNodeModel => InspectedNode as ObserverAbortNodeModel;

        public ObserverAbortNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        protected override void RefreshNodeProperties()
        {
            base.RefreshNodeProperties();
            CreateConditionalFields();
        }

        protected override void CreateObserverDropdownField()
        {
            // Skip if node doesn't implement the proper interface.
            if (InspectedNode is not IObserverAbortNodeModel ||
                !typeof(IObserverAbort).IsAssignableFrom(GraphNodeModel.NodeType.Type))
            {
                return;
            }
            
            IObserverAbortNodeModel observer = GraphNodeModel as IObserverAbortNodeModel;
            bool isValidObserverAbort = GraphNodeModel.CanUseObserverAbort();
            
            // Priority Abort node can only use LowerPriority, Self, or Both when connected to Sequence/TryInOrder
            // When NOT connected, it can also use None (becomes inactive)
            string[] observerOptions;
            int selectedIndex;
            
            if (isValidObserverAbort)
            {
                // When connected to valid parent: only show LowerPriority, Self, Both
                observerOptions = new string[] { "Self", "LowerPriority", "Both" };
                selectedIndex = observer.ObserverType switch
                {
                    ObserverAbortTarget.Self => 0,
                    ObserverAbortTarget.LowerPriority => 1,
                    ObserverAbortTarget.Both => 2,
                    _ => 1 // Default to LowerPriority
                };
            }
            else
            {
                // When NOT connected: show all options including None
                observerOptions = new string[] { "None" };
                selectedIndex = 0;
            }
            
            string tooltip = isValidObserverAbort 
                ? "\n" + k_ObserverAbortTypeSelfTooltip +
                  "\n" + k_ObserverAbortTypeLowerPriorityTooltip +
                  "\n" + k_ObserverAbortTypeBothTooltip +
                  "\n\n<i>Note: None is not available as this node is designed for priority-based interruption only.</i>"
                : k_ObserverAbortDisabledTooltip + 
                  "\n\n<i>While inactive, the node will be replaced by an implicit sequence at runtime.</i>";

            var observerTypeDropdown = CreateDropdownField("Abort Target",
                tooltips: tooltip,
                items: observerOptions,
                selectedIndex: selectedIndex,
                valueChangedCallback: evt =>
                {
                    GraphNodeModel.Asset.MarkUndo("Change Observer Type");
                    
                    if (isValidObserverAbort)
                    {
                        // Map dropdown index to enum value (skipping None)
                        observer.ObserverType = evt.newValue.First() switch
                        {
                            0 => ObserverAbortTarget.Self,
                            1 => ObserverAbortTarget.LowerPriority,
                            2 => ObserverAbortTarget.Both,
                            _ => ObserverAbortTarget.LowerPriority
                        };
                    }
                    else
                    {
                        // Direct enum mapping when inactive
                        observer.ObserverType = (ObserverAbortTarget)evt.newValue.First();
                    }
                    
                    GraphNodeModel.OnValidate();
                    Refresh(); // Refresh to update UI
                });
            observerTypeDropdown.SetEnabled(isValidObserverAbort);
        }

        private void CreateConditionalFields()
        {
            VisualElement conditionsContainer = new VisualElement() { name = "ConditionsContainer" };
            NodeProperties.Add(conditionsContainer);

            conditionsContainer.Clear();
            if (InspectedNode is not IConditionalNodeModel conditionalNode)
            {
                conditionsContainer.style.display = DisplayStyle.None;
                return;
            }

            conditionsContainer.style.display = DisplayStyle.Flex;
            conditionsContainer.Add(new ConditionRequirementElement("Observe and abort if", conditionalNode));
            conditionsContainer.Add(new ConditionInspectorElement(conditionalNode));
        }
    }
}

