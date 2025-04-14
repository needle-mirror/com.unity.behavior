using Unity.Behavior.GraphFramework;
using Unity.AppUI.UI;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(RepeatNodeModel))]
    internal class RepeatNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private Dropdown m_RepeatModeDropdown;
        private VisualElement m_ConditionsContainer;
        private Toggle m_AllowMultipleRepeatsPerTickField;

        RepeatNodeModel RepeatNodeModel => InspectedNode as RepeatNodeModel;
        public RepeatNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        private void OnRepeatValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            var enumerator = evt.newValue.GetEnumerator();
            if (enumerator.MoveNext())
            {
                RepeatNodeModel.RepeatMode newValue = (RepeatNodeModel.RepeatMode)enumerator.Current;
                RepeatNodeModel.Asset.MarkUndo("Change Repeat Mode.");
                RepeatNodeModel.Mode = newValue;
                RepeatNodeModel.OnValidate();
                RepeatNodeModel.Asset.SetAssetDirty();
                Refresh();
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            if (m_RepeatModeDropdown == null)
            {
                CreateDropdownElement();
            }
            else
            {
                RepeatNodeModel.RepeatMode repeatMode = (RepeatNodeModel.RepeatMode)m_RepeatModeDropdown.selectedIndex;
                if (RepeatNodeModel.Mode != repeatMode)
                {
                    m_RepeatModeDropdown.selectedIndex = (int)RepeatNodeModel.Mode;
                }
            }
            if (m_AllowMultipleRepeatsPerTickField == null)
            {
                m_AllowMultipleRepeatsPerTickField = CreateField<Toggle>("Allow Multiple Repeats Per Tick",
                    "If enabled, repeated processing will be occur on the same graph update.\n" +
                    "This can cause potential infinite loops if child nodes complete on the same frame. An error will be thrown if this happens.");
                m_AllowMultipleRepeatsPerTickField.RegisterValueChangedCallback(OnDelayRepeatValueChanged);
            }

            if (m_ConditionsContainer == null)
            {
                m_ConditionsContainer = new VisualElement() { name = "ConditionsContainer" };
                NodeProperties.Add(m_ConditionsContainer);
            }
            RefreshConditionalFields();
            m_AllowMultipleRepeatsPerTickField.SetValueWithoutNotify(RepeatNodeModel.AllowMultipleRepeatsPerTick);
        }
        
        private void OnDelayRepeatValueChanged(ChangeEvent<bool> evt)
        {
            RepeatNodeModel.Asset.MarkUndo("Toggle Repeat Node Delay Repeat To Next Tick.");
            RepeatNodeModel.AllowMultipleRepeatsPerTick = evt.newValue;
        }

        void CreateDropdownElement()
        {
            VisualElement dropdownContainer = new VisualElement();
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

        private void RefreshConditionalFields()
        {
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

            // Hide the truncate NodeUI setting on Conditional Guard actions.
            if (InspectedNode is ConditionalGuardNodeModel)
            {
                VisualElement truncateOptionField = this.Q<VisualElement>("TruncateOptionField");
                truncateOptionField.Hide();
            }
        }
    }
}