using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;
using TextField = Unity.AppUI.UI.TextField;

namespace Unity.Behavior
{
    internal class BehaviorGraphInspectorUI : NodeInspectorUI
    {
        internal readonly ActionButton EditSubgraphStoryButton;

        private const string k_GraphSubtitle = "Behavior Graph";
        private readonly BehaviorAuthoringGraph m_InspectedGraph;
        private TextField m_GraphDescription;

        public BehaviorGraphInspectorUI(BehaviorAuthoringGraph graph) : base(null)
        {
            m_InspectedGraph = graph;
            AddToClassList("NodeInspectorUI");
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/Inspector/Assets/BehaviorInspectorStyleSheet.uss"));
            ResourceLoadAPI.Load<VisualTreeAsset>("Packages/com.unity.behavior/Authoring/Inspector/Assets/BehaviorGraphInspectorLayout.uxml").CloneTree(this);
            
            Label titleLabel = this.Q<Label>("Info-Name");
            Label infoDescriptionLabel = this.Q<Label>("Info-Description");
            Label subtitleLabel = this.Q<Label>("Subtitle");
            EditSubgraphStoryButton = this.Q<ActionButton>("EditSubgraphStoryButton");
            m_GraphDescription = this.Q<TextField>("GraphDescription-Field");

            titleLabel.text = m_InspectedGraph.name;
            subtitleLabel.text = k_GraphSubtitle.ToUpper();
            infoDescriptionLabel.text = "This graph can be used in other graphs. Edit how it represents itself in other graphs below.";
            m_GraphDescription.RegisterValueChangingCallback(OnDescriptionChanged);
            if (!string.IsNullOrEmpty(m_InspectedGraph.Description))
            {
                m_GraphDescription.value = m_InspectedGraph.Description;
            }
        }

        private void OnDescriptionChanged(ChangingEvent<string> evt)
        {
            m_InspectedGraph.MarkUndo("Edit Subgraph Description", hasOutstandingChange: false);
            m_InspectedGraph.Description = evt.newValue;
        }

        public override void Refresh()
        {
            base.Refresh();
            
            if (m_GraphDescription != null && m_InspectedGraph.Description != m_GraphDescription.value)
            {
                m_GraphDescription.SetValueWithoutNotify(m_InspectedGraph.Description);
            }
        }
    }
}