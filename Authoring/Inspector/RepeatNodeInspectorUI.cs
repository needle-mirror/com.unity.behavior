using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(RepeatNodeModel))]
    internal class RepeatNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private Dropdown m_RepeatModeDropdown;
        private Toggle m_ReturnSuccessOnConditionFailToggle;
        private VisualElement m_ConditionsContainer;
        private Toggle m_AllowMultipleRepeatsPerTickField;

        RepeatNodeModel RepeatNodeModel => InspectedNode as RepeatNodeModel;
        public RepeatNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        protected override void RefreshNodeProperties()
        {
            base.RefreshNodeProperties();
            
            CreateDropdownElement();
            m_AllowMultipleRepeatsPerTickField = CreateField<Toggle>("Allow Multiple Repeats Per Tick",
                "If enabled, repeated processing will be occur on the same graph update.\n" +
                "This can cause potential infinite loops if child nodes complete on the same frame. An error will be thrown if this happens.");
            m_AllowMultipleRepeatsPerTickField.RegisterValueChangedCallback(OnDelayRepeatValueChanged);
            m_AllowMultipleRepeatsPerTickField.SetValueWithoutNotify(RepeatNodeModel.AllowMultipleRepeatsPerTick);

            if (RepeatNodeModel.Mode == RepeatNodeModel.RepeatMode.Condition)
            {
                m_ReturnSuccessOnConditionFailToggle = CreateField<Toggle>(
                    RepeatWhileConditionModifier.kReturnFailureOnConditionFailName,
                    RepeatWhileConditionModifier.kReturnFailureOnConditionFailTooltip);
                m_ReturnSuccessOnConditionFailToggle.RegisterValueChangedCallback(evt =>
                {
                    InspectedNode.Asset.MarkUndo("Toggle Return Success On Condition Fail");
                    RepeatNodeModel.ReturnFailureOnConditionFail = evt.newValue;
                });
                m_ReturnSuccessOnConditionFailToggle.SetValueWithoutNotify(RepeatNodeModel.ReturnFailureOnConditionFail);
            }

            CreateConditionalFields();
        }

        private void OnDelayRepeatValueChanged(ChangeEvent<bool> evt)
        {
            RepeatNodeModel.Asset.MarkUndo("Toggle Repeat Node Delay Repeat To Next Tick");
            RepeatNodeModel.AllowMultipleRepeatsPerTick = evt.newValue;
        }

        private void CreateDropdownElement()
        {
            VisualElement dropdownContainer = new VisualElement();
            dropdownContainer.AddToClassList("DropdownPropertyElement");
            dropdownContainer.style.flexDirection = FlexDirection.Row;
            dropdownContainer.style.justifyContent = Justify.SpaceBetween;
            dropdownContainer.style.alignItems = Align.Center;
            NodeProperties.Add(dropdownContainer);

            Label repeatModeLabel = new Label("Repeat Mode");
            dropdownContainer.Add(repeatModeLabel);

            m_RepeatModeDropdown = new Dropdown();
            var repeatModes = Enum.GetNames(typeof(RepeatNodeModel.RepeatMode));
            for (int i = 0; i < repeatModes.Length; i++)
            {
                repeatModes[i] = Util.NicifyVariableName(repeatModes[i]);
            }
            m_RepeatModeDropdown.bindItem = (item, i) => item.label = repeatModes[i];
            m_RepeatModeDropdown.sourceItems = repeatModes;
            m_RepeatModeDropdown.selectedIndex = (int)RepeatNodeModel.Mode;
            m_RepeatModeDropdown.RegisterValueChangedCallback(OnRepeatValueChanged);
            dropdownContainer.Add(m_RepeatModeDropdown);
        }

        private void OnRepeatValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            RepeatNodeModel.Asset.MarkUndo("Change Repeat Mode");
            RepeatNodeModel.Mode = (RepeatNodeModel.RepeatMode)evt.newValue.First();
            RepeatNodeModel.OnValidate();
            Refresh();
        }

        private void CreateConditionalFields()
        {
            m_ConditionsContainer = new VisualElement() { name = "ConditionsContainer" };
            NodeProperties.Add(m_ConditionsContainer);

            m_ConditionsContainer.Clear();
            if (InspectedNode is not IConditionalNodeModel conditionalNode || RepeatNodeModel.Mode != RepeatNodeModel.RepeatMode.Condition)
            {
                m_ConditionsContainer.style.display = DisplayStyle.None;
                return;
            }

            m_ConditionsContainer.style.display = DisplayStyle.Flex;
            m_ConditionsContainer.Add(new Divider());
            m_ConditionsContainer.Add(new ConditionRequirementElement("Repeat if", conditionalNode));
            m_ConditionsContainer.Add(new ConditionInspectorElement(conditionalNode));
        }
    }
}
