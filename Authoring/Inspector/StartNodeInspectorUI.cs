using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(StartNodeModel))]
    internal class StartNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private Toggle m_RepeatField;
        private Toggle m_AllowMultipleRepeatsPerTickField;
        public StartNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        public override void Refresh()
        {
            base.Refresh();
            if (m_RepeatField == null)
            {
                m_RepeatField = CreateField<Toggle>("Repeat");
                m_RepeatField.RegisterValueChangedCallback(OnRepeatValueChanged);
            }
            if (m_AllowMultipleRepeatsPerTickField == null)
            {
                m_AllowMultipleRepeatsPerTickField = CreateField<Toggle>("Allow Multiple Repeats Per Tick",
                    "If enabled, repeated processing will be occur on the same graph update.\n" +
                    "This can cause potential infinite loops if child nodes complete on the same frame. An error will be thrown if this happens.");
                m_AllowMultipleRepeatsPerTickField.RegisterValueChangedCallback(OnDelayRepeatValueChanged);
            }
            StartNodeModel startModel = InspectedNode as StartNodeModel;
            m_RepeatField.SetValueWithoutNotify(startModel.Repeat);
            m_AllowMultipleRepeatsPerTickField.SetValueWithoutNotify(startModel.AllowMultipleRepeatsPerTick);
            m_AllowMultipleRepeatsPerTickField.SetEnabled(startModel.Repeat);
        }

        private void OnRepeatValueChanged(ChangeEvent<bool> evt)
        {
            StartNodeModel startModel = InspectedNode as StartNodeModel;
            startModel.Asset.MarkUndo("Toggle Start Node Repeat");
            startModel.Repeat = evt.newValue;
        }
        
        private void OnDelayRepeatValueChanged(ChangeEvent<bool> evt)
        {
            StartNodeModel startModel = InspectedNode as StartNodeModel;
            startModel.Asset.MarkUndo("Toggle Start Node Delay Repeat To Next Tick");
            startModel.AllowMultipleRepeatsPerTick = evt.newValue;
        }
    }
}
