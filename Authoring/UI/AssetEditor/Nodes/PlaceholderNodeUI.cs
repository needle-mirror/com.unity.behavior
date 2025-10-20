using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    internal class PlaceholderNodeUI : BehaviorNodeUI
    {
        private PlaceholderNodeType m_PlaceholderNodeType;

        internal enum PlaceholderNodeType
        {
            Action,
            Modifier,
            Composite
        }

        public PlaceholderNodeUI(NodeModel nodeModel, BehaviorAuthoringGraph asset) : base(nodeModel)
        {
            AddToClassList("Placeholder");

            if (nodeModel is BehaviorGraphNodeModel behaviorGraphNodeModel)
            {
                if (asset == null || !asset.RuntimeNodeTypeIDToNodeModelInfo.TryGetValue(behaviorGraphNodeModel.NodeTypeID, out BehaviorAuthoringGraph.NodeModelInfo modelInfo))
                {
                    modelInfo = new BehaviorAuthoringGraph.NodeModelInfo();
                    modelInfo.Name = "Missing Node";
                    modelInfo.Story = "Missing Node";
                }
                modelInfo.RuntimeTypeID = behaviorGraphNodeModel.NodeTypeID;
                if (typeof(CompositeNodeModel).IsAssignableFrom(nodeModel.GetType()))
                {
                    m_PlaceholderNodeType = PlaceholderNodeType.Composite;
                }
                else if (typeof(ModifierNodeModel).IsAssignableFrom(nodeModel.GetType()))
                {
                    m_PlaceholderNodeType = PlaceholderNodeType.Modifier;
                }
                else
                {
                    m_PlaceholderNodeType = PlaceholderNodeType.Action;
                }
                InitPlaceholderNodeType(modelInfo.Story);

                Title = modelInfo.Name;
                tooltip = modelInfo.Name;

                NodeValueContainer.Add(CreatePlaceholderNodeContent(this, modelInfo.Story, modelInfo.Variables));
            }
        }

        void InitPlaceholderNodeType(string story)
        {
            switch (m_PlaceholderNodeType)
            {
                case PlaceholderNodeType.Action:
                    AddToClassList("Action");
                    break;
                case PlaceholderNodeType.Modifier:
                    AddToClassList("Modifier");
                    break;
                case PlaceholderNodeType.Composite:
                    AddToClassList("Composite");
                    break;
            }

            if (m_PlaceholderNodeType != PlaceholderNodeType.Action && !String.IsNullOrEmpty(story))
            {
                AddToClassList("TwoLineNode");
            }
        }

        private VisualElement CreatePlaceholderNodeContent(BehaviorNodeUI nodeUI, string story, List<VariableInfo> variables)
        {
            var container = new VisualElement();
            container.styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Elements/Assets/LinkFieldStyles.uss"));
            container.AddToClassList("PlaceholderNodeContent");

            var storyContainer = new VisualElement();
            storyContainer.AddToClassList("PlaceholderStoryContainer");
            string[] words = story.Split(' ');
            if (words?.Length > 0)
            {
                container.Add(storyContainer);
            }
            for (int i = 0; i < words.Length; ++i)
            {
                string word = words[i];
                word = word.TrimStart('[');
                word = word.TrimEnd(']');
                var foundMatch = variables != null && variables.Any(variable =>
                    string.Equals(word, variable.Name, StringComparison.OrdinalIgnoreCase));
                if (foundMatch)
                {
                    var label = new Label(word);
                    label.AddToClassList("Linked");
                    label.AddToClassList("LinkedLabel");
                    storyContainer.Add(label);
                }
                else
                {
                    Label label = new Label(word);
                    storyContainer.Add(label);
                }
            }

#if UNITY_EDITOR
            // Add an appui button to create the action
            var button = new Unity.AppUI.UI.Button();
            button.title = "Create";
            container.Add(button);
            button.clickable.clicked += () =>
            {
                var graphView = container.GetFirstAncestorOfType<BehaviorGraphView>();
                if (nodeUI.Model is BehaviorGraphNodeModel nodeModel)
                {
                    if (m_PlaceholderNodeType == PlaceholderNodeType.Action)
                    {
                        graphView?.ShowActionNodeWizard(nodeUI.Position, nodeModel);
                    }
                    else if (m_PlaceholderNodeType == PlaceholderNodeType.Modifier)
                    {
                        graphView?.ShowModifierNodeWizard(nodeUI.Position, nodeModel);
                    }
                    else if (m_PlaceholderNodeType == PlaceholderNodeType.Composite)
                    {
                        graphView?.ShowSequencingNodeWizard(nodeUI.Position, nodeModel);
                    }
                }
            };
#endif
            return container;
        }
    }
}
