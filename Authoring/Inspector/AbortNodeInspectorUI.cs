using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using Unity.AppUI.UI;
using UnityEngine.UIElements;
using System.Linq;
using Button = Unity.AppUI.UI.Button;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(AbortNodeModel))]
    internal class AbortNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private AbortNodeModel m_AbortNodeModel => InspectedNode as AbortNodeModel;
        private Dropdown m_TypeDropdown;
        private Dropdown m_ConditionRequirementDropdown;

        public AbortNodeInspectorUI(NodeModel nodeModel) : base(nodeModel)
        {
        }

        protected override void RefreshNodeProperties()
        { 
            NodeProperties.Clear();

            NodeProperties.Add(CreateTypeSelectionElement());
            CreateAbortObserverDropdownField();
            NodeProperties.Add(CreateConditionRequirementElement());
            NodeProperties.Add(new ConditionInspectorElement(m_AbortNodeModel));
        }

        private void CreateAbortObserverDropdownField()
        {
            // Skip if node doesn't implement the proper interface.
            if (InspectedNode is not IObserverAbortNodeModel ||
                !typeof(IObserverAbort).IsAssignableFrom(GraphNodeModel.NodeType.Type))
            {
                return;
            }
            
            IObserverAbortNodeModel observer = GraphNodeModel as IObserverAbortNodeModel;
            bool isValidObserverAbort = GraphNodeModel.CanUseObserverAbort();
            
            // Abort/Restart only support None and LowerPriority (Self/Both don't make semantic sense)
            string[] observerOptions = new string[] { "None", "LowerPriority" };
            int selectedIndex = observer.ObserverType == ObserverAbortTarget.LowerPriority ? 1 : 0;
            
            string tooltip = isValidObserverAbort 
                ? "<b>None</b>: Standard behavior - monitors conditions during execution." +
                  "\n" + k_ObserverAbortTypeLowerPriorityTooltip +
                  "\n" + k_ObserverAbortConditionDetailsTooltip +
                  "\n\n<i>Note: Self/Both are not available because these nodes already monitor and react to their own conditions.</i>"
                : k_ObserverAbortDisabledTooltip;

            var observerTypeDropdown = CreateDropdownField("Abort Target",
                tooltips: tooltip,
                items: observerOptions,
                selectedIndex: selectedIndex,
                valueChangedCallback: evt =>
                {
                    GraphNodeModel.Asset.MarkUndo("Change Observer Type");
                    observer.ObserverType = evt.newValue.First() == 0 ? ObserverAbortTarget.None : ObserverAbortTarget.LowerPriority;
                    GraphNodeModel.OnValidate();
                    Refresh(); // Refresh to update condition requirement label
                });
            observerTypeDropdown.SetEnabled(isValidObserverAbort);
        }

        private VisualElement CreateTypeSelectionElement()
        {
            VisualElement typeDropdownContainer = new VisualElement();
            typeDropdownContainer.AddToClassList("DropdownPropertyElement");
            Label label = new Label("Type");
            label.tooltip = "Select the type of abort node. \"Abort\" ends the execution of all the children nodes and returns failure to the parent node. \"Restart\" ends all the children nodes and then restarts them.";
            typeDropdownContainer.Add(label);
            m_TypeDropdown = new Dropdown();
            List<string> types = Enum.GetNames(typeof(AbortNodeModel.AbortType)).ToList();
            m_TypeDropdown.bindItem = (item, i) => item.label = types[i];
            m_TypeDropdown.sourceItems = types;
            m_TypeDropdown.selectedIndex = (int)m_AbortNodeModel.ModelAbortType;
            typeDropdownContainer.Add(m_TypeDropdown);

            m_TypeDropdown.RegisterValueChangedCallback(evt =>
            {
                m_AbortNodeModel.Asset.MarkUndo("Change Abort Mode");
                m_AbortNodeModel.ModelAbortType = (AbortNodeModel.AbortType)evt.newValue.First();
                m_AbortNodeModel.OnValidate();
                m_AbortNodeModel.Asset.SetAssetDirty();
                Refresh();
            });

            return typeDropdownContainer;
        }

        private VisualElement CreateConditionRequirementElement()
        {
            VisualElement conditionRequirementDropdown = new VisualElement();
            conditionRequirementDropdown.AddToClassList("DropdownPropertyElement");
            Label label = new Label();
            
            string labelText = m_AbortNodeModel.ModelAbortType == AbortNodeModel.AbortType.Fail ? "Aborts if" : "Restarts if";
            if (m_AbortNodeModel.ObserverType != ObserverAbortTarget.None)
            {
                labelText = m_AbortNodeModel.ModelAbortType == AbortNodeModel.AbortType.Fail ? "Aborts and observes if" : "Restarts and observes if";
            }
            
            label.text = labelText;
            label.tooltip = "Select if the node should trigger when any condition is met or if all conditions must return true.";
            conditionRequirementDropdown.Add(label);
            m_ConditionRequirementDropdown = new Dropdown();
            List<string> types = new List<string>
            {
                "Any Are True",
                "All Are True"
            };
            m_ConditionRequirementDropdown.bindItem = (item, i) => item.label = types[i];
            m_ConditionRequirementDropdown.sourceItems = types;
            m_ConditionRequirementDropdown.selectedIndex = m_AbortNodeModel.RequiresAllConditionsTrue ? 1 : 0;
            conditionRequirementDropdown.Add(m_ConditionRequirementDropdown);

            m_ConditionRequirementDropdown.RegisterValueChangedCallback(evt =>
            {
                m_AbortNodeModel.RequiresAllConditionsTrue = evt.newValue.First() == 1;
                m_AbortNodeModel.OnValidate();
                m_AbortNodeModel.Asset.SetAssetDirty();
                Refresh();
            });

            return conditionRequirementDropdown;
        }
    }
}
