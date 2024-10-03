using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using DropDown = Unity.AppUI.UI.Dropdown;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(SwitchNodeModel))]
    internal class SwitchNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        public SwitchNodeInspectorUI(NodeModel nodeModel)
            : base(nodeModel)
        {
        }
    }
}