using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    [NodeUI(typeof(PlaceholderNodeModel))]
    internal class PlaceholderNodeUI : BehaviorNodeUI
    {
        public PlaceholderNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            AddToClassList("Placeholder");

            if (nodeModel is PlaceholderNodeModel placeholderNodeModel)
            {
                InitPlaceholderNodeType(placeholderNodeModel.PlaceholderType, placeholderNodeModel.Story);

                Title = placeholderNodeModel.Name;
                tooltip = placeholderNodeModel.Name;

                NodeValueContainer.Add(CreatePlaceholderNodeContent(this, placeholderNodeModel.Story, placeholderNodeModel.Variables));
            }

            else if (nodeModel is BehaviorGraphNodeModel behaviorGraphNodeModel)
            {
                BehaviorAuthoringGraph asset = nodeModel.Asset as BehaviorAuthoringGraph;
                if (asset.RuntimeNodeTypeIDToNodeModelInfo.TryGetValue(behaviorGraphNodeModel.NodeTypeID, out BehaviorAuthoringGraph.NodeModelInfo modelInfo) == false)
                {
                    modelInfo = new BehaviorAuthoringGraph.NodeModelInfo();
                    modelInfo.Name = "Missing Node";
                    modelInfo.Story = "Missing Node";
                }
                modelInfo.RuntimeTypeID = behaviorGraphNodeModel.NodeTypeID;
                PlaceholderNodeModel.PlaceholderNodeType placeholderNodeType;
                if (typeof(CompositeNodeModel).IsAssignableFrom(nodeModel.GetType()))
                {
                    placeholderNodeType = PlaceholderNodeModel.PlaceholderNodeType.Composite;
                }
                else if (typeof(ModifierNodeModel).IsAssignableFrom(nodeModel.GetType()))
                {
                    placeholderNodeType = PlaceholderNodeModel.PlaceholderNodeType.Modifier;
                }
                else
                {
                    placeholderNodeType = PlaceholderNodeModel.PlaceholderNodeType.Action;
                }
                InitPlaceholderNodeType(placeholderNodeType, modelInfo.Story);

                Title = modelInfo.Name;
                tooltip = modelInfo.Name;

                NodeValueContainer.Add(CreatePlaceholderNodeContent(this, modelInfo.Story, modelInfo.Variables));
            }
        }

        void InitPlaceholderNodeType(PlaceholderNodeModel.PlaceholderNodeType placeholderNodeType, string story)
        {
            switch (placeholderNodeType)
            {
                case PlaceholderNodeModel.PlaceholderNodeType.Action:
                    AddToClassList("Action");
                    break;
                case PlaceholderNodeModel.PlaceholderNodeType.Modifier:
                    AddToClassList("Modifier");
                    break;
                case PlaceholderNodeModel.PlaceholderNodeType.Composite:
                    AddToClassList("Composite");
                    break;
            }

            if (placeholderNodeType != PlaceholderNodeModel.PlaceholderNodeType.Action && !String.IsNullOrEmpty(story))
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
                if (nodeUI.Model is PlaceholderNodeModel placeholderNodeModel)
                {
                    if (placeholderNodeModel.PlaceholderType == PlaceholderNodeModel.PlaceholderNodeType.Action)
                    {
                        graphView?.ShowActionNodeWizard(nodeUI.Position, placeholderNodeModel);
                    }
                    else if (placeholderNodeModel.PlaceholderType == PlaceholderNodeModel.PlaceholderNodeType.Modifier)
                    {
                        graphView?.ShowModifierNodeWizard(nodeUI.Position, placeholderNodeModel);
                    }
                    else if (placeholderNodeModel.PlaceholderType == PlaceholderNodeModel.PlaceholderNodeType.Composite)
                    {
                        graphView?.ShowSequencingNodeWizard(nodeUI.Position, placeholderNodeModel);
                    }
                }
            };
#endif
            return container;
        }
    }
}