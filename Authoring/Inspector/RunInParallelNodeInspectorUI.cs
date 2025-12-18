using Unity.Behavior.GraphFramework;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements;
using Unity.AppUI.UI;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(RunInParallelNodeModel))]
    internal class RunInParallelNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private VisualElement m_InspectorField;
        private Dropdown m_ModeDropdown;
        private RunInParallelNodeModel ParallelNodeModel => InspectedNode as RunInParallelNodeModel;

        public RunInParallelNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        private void OnModeValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            var enumerator = evt.newValue.GetEnumerator();
            if (enumerator.MoveNext())
            {
                RunInParallelNodeModel.ParallelMode newValue = (RunInParallelNodeModel.ParallelMode)enumerator.Current;
                ParallelNodeModel.Asset.MarkUndo("Change Parallel Mode");
                ParallelNodeModel.Mode = newValue;
                ParallelNodeModel.OnValidate();
                ParallelNodeModel.Asset.SetAssetDirty();
                Refresh();
            }
        }

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

                RunInParallelNodeModel.ParallelMode parallelMode = (RunInParallelNodeModel.ParallelMode)m_ModeDropdown.selectedIndex;
                if (ParallelNodeModel.Mode != parallelMode)
                {
                    m_ModeDropdown.SetValueWithoutNotify(new[] { (int)ParallelNodeModel.Mode });
                }
            }
        }

        void CreateDropdownElement()
        {
            m_InspectorField = new VisualElement();
            m_InspectorField.AddToClassList("Inspector-FieldContainer");
            NodeProperties.Add(m_InspectorField);

            Label parallelModeLabel = new Label("Parallel Mode");
            m_InspectorField.Add(parallelModeLabel);

            m_ModeDropdown = new Dropdown();
            var parallelModes = Enum.GetNames(typeof(RunInParallelNodeModel.ParallelMode));
            for (int i = 0; i < parallelModes.Length; i++)
            {
                parallelModes[i] = Util.NicifyVariableName(parallelModes[i]);
            }
            m_ModeDropdown.bindItem = (item, i) => item.label = parallelModes[i];
            m_ModeDropdown.sourceItems = parallelModes;
            m_ModeDropdown.selectedIndex = (int)ParallelNodeModel.Mode;
            m_ModeDropdown.RegisterValueChangedCallback(OnModeValueChanged);
            m_InspectorField.Add(m_ModeDropdown);
        }
    }
}
