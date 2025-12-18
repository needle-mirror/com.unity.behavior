using System;
using System.Collections.Generic;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(SwitchNodeModel))]
    internal class SwitchNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private VisualElement m_InspectorField;
        private Dropdown m_ModeDropdown;

        private SwitchNodeModel SwitchNodeModel => InspectedNode as SwitchNodeModel;

        public SwitchNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        public override void Refresh()
        {
            base.Refresh();
            
            if (m_ModeDropdown == null)
            {
                CreateDropdownElement();
            }
            else
            {
                // Refresh will clear NodeProperties if the node model has any field.
                if (!NodeProperties.Contains(m_InspectorField))
                {
                    NodeProperties.Add(m_InspectorField);
                }

                SwitchNodeModel.EnumMode enumMode = (SwitchNodeModel.EnumMode)m_ModeDropdown.selectedIndex;
                if (SwitchNodeModel.Mode != enumMode)
                {
                    m_ModeDropdown.SetValueWithoutNotify(new[] { (int)SwitchNodeModel.Mode });
                }
            }
        }

        void CreateDropdownElement()
        {
            m_InspectorField = new VisualElement();
            m_InspectorField.AddToClassList("Inspector-FieldContainer");
            NodeProperties.Add(m_InspectorField);

            Label EnumModeLabel = new Label("Enum Mode");
            m_InspectorField.Add(EnumModeLabel);

            m_ModeDropdown = new Dropdown();
            var EnumModes = Enum.GetNames(typeof(SwitchNodeModel.EnumMode));
            for (int i = 0; i < EnumModes.Length; i++)
            {
                EnumModes[i] = Util.NicifyVariableName(EnumModes[i]);
            }
            m_ModeDropdown.bindItem = (item, i) => item.label = EnumModes[i];
            m_ModeDropdown.sourceItems = EnumModes;
            m_ModeDropdown.selectedIndex = (int)SwitchNodeModel.Mode;
            m_ModeDropdown.RegisterValueChangedCallback(OnModeValueChanged);
            m_InspectorField.Add(m_ModeDropdown);
        }

        private void OnModeValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            var enumerator = evt.newValue.GetEnumerator();
            if (enumerator.MoveNext())
            {
                SwitchNodeModel.EnumMode newValue = (SwitchNodeModel.EnumMode)enumerator.Current;
                SwitchNodeModel.Asset.MarkUndo("Change Switch Mode");
                SwitchNodeModel.Mode = newValue;
                SwitchNodeModel.OnValidate();
                SwitchNodeModel.Asset.SetAssetDirty();
                Refresh();
            }
        }
    }
}
