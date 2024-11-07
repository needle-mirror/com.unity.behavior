using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
#if ENABLE_UXML_UI_SERIALIZATION
    [UxmlElement]
#endif
    internal partial class BehaviorInspectorView : InspectorView
    {
#if !ENABLE_UXML_UI_SERIALIZATION
        internal new class UxmlFactory : UxmlFactory<BehaviorInspectorView, UxmlTraits> {}
#endif
        internal new BehaviorGraphNodeModel InspectedNode => base.InspectedNode as BehaviorGraphNodeModel;

        public BehaviorInspectorView() { }

        protected override NodeInspectorUI CreateDefaultInspectorImpl()
        {
            if (GraphEditor.Asset == null)
            {
                return null;
            }

            BehaviorGraphInspectorUI graphInspector = new BehaviorGraphInspectorUI(GraphEditor.Asset as BehaviorAuthoringGraph);
            if (GraphEditor is BehaviorGraphEditor editor)
            {
                graphInspector.EditSubgraphStoryButton.clicked += editor.OnSubgraphRepresentationButtonClicked;   
            }
            return graphInspector;
        }

        public override void Refresh()
        {
            if (InspectedNodeUI is BehaviorNodeUI behaviorNodeUI)
            {
                behaviorNodeUI.UpdateLinkFields();
            }
            base.Refresh();
        }
    }
}