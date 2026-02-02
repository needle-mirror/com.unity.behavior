using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(BehaviorGraphNodeModel))]
    [NodeInspectorUI(typeof(ActionNodeModel))]
    [NodeInspectorUI(typeof(CompositeNodeModel))]
    [NodeInspectorUI(typeof(ModifierNodeModel))]
    [NodeInspectorUI(typeof(JoinNodeModel))]
    internal class BehaviorGraphNodeInspectorUI : NodeInspectorUI
    {
        public const string k_ObserverAbortDisabledTooltip =
            "Observer functionality is only available when the node is a direct child of TryInOrder or Sequence node.";
        protected const string k_ObserverAbortTypeNoneTooltip =
            "<b>None</b>: Condition is evaluated only when entering the node.";
        protected const string k_ObserverAbortTypeSelfTooltip =
            "<b>Self</b>: Condition is monitored while this branch is running. Aborts if condition is no longer satisfied.";
        protected const string k_ObserverAbortTypeLowerPriorityTooltip =
            "<b>Lower Priority</b>: Condition is monitored while lower-priority siblings are running. Aborts them if condition becomes satisfied.";
        protected const string k_ObserverAbortTypeBothTooltip =
            "<b>Both</b>: Combines Self and Lower Priority monitoring.";
        protected const string k_ObserverAbortConditionDetailsTooltip = "\n<i>Aborting forces the parent composite to re-evaluate from the beginning. " +
            "Conditions are evaluated once per graph tick while the parent composite is active.</i>";

        protected const string k_ObserverAbortTypeAllTooltip =
            k_ObserverAbortTypeNoneTooltip + "\n" + 
            k_ObserverAbortTypeSelfTooltip + "\n" + 
            k_ObserverAbortTypeLowerPriorityTooltip + "\n" + 
            k_ObserverAbortTypeBothTooltip + "\n" +
            k_ObserverAbortConditionDetailsTooltip;

        protected const string k_ObserverAbortLowerPriorityOnlyTooltip = 
            k_ObserverAbortTypeNoneTooltip + "\n" + k_ObserverAbortTypeLowerPriorityTooltip;

        private readonly VisualElement m_NodeProperties;
        internal VisualElement NodeProperties => m_NodeProperties;

        private readonly VisualElement m_NodeInfo;
        protected VisualElement NodeInfo => m_NodeInfo;

        private readonly Label m_NodeTitle;
        private readonly Label m_NodeDescription;
        private readonly VisualElement m_NodeIcon;
        private readonly Label m_CategoryField;
        private readonly ActionButton m_EditDefinition;
        private readonly ActionButton m_EditScript;

        public BehaviorGraphNodeModel GraphNodeModel => InspectedNode as BehaviorGraphNodeModel;

        protected string Title
        { get => m_NodeTitle.text; set { m_NodeTitle.text = value; } }

        protected string Description
        {
            get => m_NodeDescription.text;
            set
            {
                m_NodeDescription.text = value;
                m_NodeDescription.EnableInClassList("Hidden", value == null || value.Length == 0);
            }
        }

        protected Texture2D Icon
        {
            get => m_NodeIcon.style.backgroundImage.value.texture;
            set
            {
                m_NodeIcon.style.backgroundImage = value;
                m_NodeIcon.EnableInClassList("Hidden", value == null);
            }
        }

        public BehaviorGraphNodeInspectorUI(NodeModel nodeModel) : base(nodeModel)
        {
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/Inspector/Assets/BehaviorInspectorStyleSheet.uss"));
            ResourceLoadAPI.Load<VisualTreeAsset>("Packages/com.unity.behavior/Authoring/Inspector/Assets/BehaviorNodeInspectorLayout.uxml").CloneTree(this);

            m_NodeProperties = this.Q<VisualElement>("InspectedProperties");
            m_NodeTitle = this.Q<Label>("Info-Name");
            m_NodeIcon = this.Q("Node-Icon");
            m_NodeDescription = this.Q<Label>("Info-Description");
            m_CategoryField = this.Q<Label>("Subtitle");
            m_NodeInfo = this.Q<VisualElement>("Inspector-Info");
            m_EditDefinition = this.Q<ActionButton>("EditDefinition");
            m_EditScript = this.Q<ActionButton>("EditScript");

            // Handling placeholder node.
            if (GraphNodeModel.NodeType == null)
            {
                VisualElement container = new VisualElement();
                container.name = "PlaceholderWarningContainer";
                AppUI.UI.Icon icon = new AppUI.UI.Icon();
                icon.name = "PlaceholderWarningIcon";
                icon.size = AppUI.UI.IconSize.L;
                icon.iconName = "warning";
                container.Add(icon);
                container.Add(new AppUI.UI.Text("Placeholder nodes will be removed or replaced with a Sequence node during execution."));
                NodeInfo.Add(container);

                var editButtonsContainer = this.Q("NodeInfo-EditButtons");
                editButtonsContainer.AddToClassList("Hidden");
                Refresh();
                return;
            }

            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(GraphNodeModel.NodeTypeID);
            RefreshNodeInformation(nodeInfo);

#if UNITY_EDITOR
            // Checks if the node is a built-in type.
            if (nodeInfo == null || Util.IsNodeInPackageRuntimeAssembly(nodeInfo))
            {
                m_EditDefinition.SetEnabled(false);
                m_EditDefinition.tooltip = "The definition of built-in nodes can not be edited.";
                m_EditScript.label = "Inspect Script";
            }

            m_EditDefinition.clicked += OnEditNode;
            m_EditScript.clicked += OnEditScript;
#else
            VisualElement buttonsContainer = this.Q("NodeInfo-EditButtons");
            buttonsContainer.AddToClassList("Hidden");
#endif
            Refresh();
        }

        public override void Refresh()
        {
            base.Refresh();
            
            // Placeholder node handling.
            if (GraphNodeModel.NodeType == null)
            {
                if (GraphNodeModel.Asset is BehaviorAuthoringGraph authoringGraph
                    && authoringGraph.RuntimeNodeTypeIDToNodeModelInfo.TryGetValue(GraphNodeModel.NodeTypeID, out var nodeModelInfo))
                {
                    Title = $"{nodeModelInfo.Name} (Placeholder)";
                    Description = nodeModelInfo.Story;
                    m_NodeTitle.EnableInClassList("Full-Width", true);
                    m_CategoryField.AddToClassList("Hidden");
                }

                return;
            }
            
            RefreshNodeProperties();
            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(GraphNodeModel.NodeTypeID);
            RefreshNodeInformation(nodeInfo);

            foreach (BaseLinkField field in this.Query<BaseLinkField>().ToList())
            {
                // Keep the linked label prefix updated on Blackboard asset group variables.
                Util.UpdateLinkFieldBlackboardPrefixes(field);
            }
        }

        /// <summary>
        /// Method that generate the inspector fields and populate NodeProperties.
        /// </summary>
        protected virtual void RefreshNodeProperties()
        {
            NodeProperties.Clear();
            CreateObserverDropdownField();
            if (GraphNodeModel.m_FieldValues.Count != 0)
            {
                CreateFields();
            }
        }

        /// <summary>
        /// Default implementation for ObserverAbort.
        /// </summary>
        protected virtual void CreateObserverDropdownField()
        {
            // Skip Node model or Node type don't implement the proper interface.
            if (InspectedNode is not IObserverAbortNodeModel ||
                !typeof(IObserverAbort).IsAssignableFrom(GraphNodeModel.NodeType.Type))
            {
                return;
            }
            
            IObserverAbortNodeModel observer = GraphNodeModel as IObserverAbortNodeModel;

            bool isValidObserverAbort = GraphNodeModel.CanUseObserverAbort();
            var observerTypeDropdown = CreateDropdownField("Abort Target",
                tooltips: isValidObserverAbort ? k_ObserverAbortTypeAllTooltip : k_ObserverAbortDisabledTooltip,
                items: Enum.GetNames(typeof(ObserverAbortTarget)),
                selectedIndex: (int)observer.ObserverType,
                valueChangedCallback: evt =>
                {
                    GraphNodeModel.Asset.MarkUndo("Change Observer Type");
                    observer.ObserverType = (ObserverAbortTarget)evt.newValue.First();
                    GraphNodeModel.OnValidate();
                    Refresh(); // Refresh to update condition requirement label
                });
            observerTypeDropdown.SetEnabled(isValidObserverAbort);
        }

        protected void RefreshNodeInformation(NodeInfo nodeInfo)
        {
            if (nodeInfo == null)
            {
                m_CategoryField.EnableInClassList("Hidden", true);
                m_NodeTitle.EnableInClassList("Full-Width", true);
                Icon = null;
                return;
            }
            Title = nodeInfo.Name;
            if (string.IsNullOrEmpty(nodeInfo.Description))
            {
                Description = "(No description added)";
                m_NodeDescription.AddToClassList("PlaceholderText");
            }
            else
            {
                Description = nodeInfo.Description;
            }
            Icon = nodeInfo.Icon;
            m_CategoryField.SetEnabled(false);
            m_CategoryField.text = nodeInfo.Category.ToUpper();
            m_CategoryField.EnableInClassList("Hidden", nodeInfo.Category == null || nodeInfo.Category.Length == 0);
        }

        private void CreateFields()
        {
            BehaviorGraphNodeModel behaviorGraphNodeModel = InspectedNode as BehaviorGraphNodeModel;
            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(behaviorGraphNodeModel.NodeTypeID);

            if (nodeInfo?.Variables == null)
            {
                return;
            }
            foreach (var variable in nodeInfo.Variables)
            {
                FindAndCreateField(variable.Name, nodeInfo);
            }
        }

        protected void FindAndCreateField(string fieldName, NodeInfo nodeInfo)
        {
            if (nodeInfo.Variables != null)
            {
                for (int i = 0; i < nodeInfo.Variables.Count; ++i)
                {
                    var field = nodeInfo.Variables[i];
                    string nicifiedName = Util.NicifyVariableName(field.Name).Replace(" ", "");
                    string nicifiedFieldName = Util.NicifyVariableName(fieldName).Replace(" ", "");
                    if (nicifiedFieldName.Equals(nicifiedName, StringComparison.OrdinalIgnoreCase))
                    {
                        var fieldElement = CreateField(field.Name, field.Type, field.Tooltip);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Create a static field (non BaseLinkField) with a standardized container.
        /// </summary>
        /// <typeparam name="TValue">Unity.AppUI.UI.BaseVisualElement type</typeparam>
        /// <returns></returns>
        protected TValue CreateField<TValue>(string fieldName, string tooltip = null)
            where TValue : Unity.AppUI.UI.BaseVisualElement, new()
        {
            VisualElement fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("Inspector-FieldContainer");
            fieldContainer.Add(new Label(fieldName));

            TValue field = new TValue();
            fieldContainer.Add(field);
            NodeProperties.Add(fieldContainer);

            if (tooltip != null)
            {
                fieldContainer.tooltip = tooltip;
                field.tooltip = tooltip;
            }
            return field;
        }

        protected BaseLinkField CreateField(string fieldName, Type fieldType, string tooltip = null)
        {
            string nicifiedFieldName = Util.NicifyVariableName(fieldName);
            BaseLinkField field = LinkFieldUtility.CreateNodeLinkField(fieldName, fieldType);

            if (field == null)
            {
                return null;
            }

            VisualElement fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("Inspector-FieldContainer");
            fieldContainer.Add(new Label(nicifiedFieldName));
            fieldContainer.Add(field);
            NodeProperties.Add(fieldContainer);
            field.FieldName = fieldName;
            field.Model = InspectedNode;
            field.AddToClassList("LinkField-Light");
            field.Label = Util.NicifyVariableName(nicifiedFieldName);

            if (tooltip != null)
            {
                fieldContainer.tooltip = tooltip;
                field.tooltip = tooltip;
            }

            return field;
        }

        protected Dropdown CreateDropdownField(string fieldName, string tooltips, string[] items, int selectedIndex, 
            EventCallback<ChangeEvent<IEnumerable<int>>> valueChangedCallback)
        {
            VisualElement typeDropdownContainer = new VisualElement();
            typeDropdownContainer.AddToClassList("DropdownPropertyElement");
            Label label = new Label(fieldName);
            label.tooltip = tooltips;
            typeDropdownContainer.Add(label);

            var dropdown = new Dropdown();
            dropdown.bindItem = (item, i) => item.label = Util.NicifyVariableName(items[i]);
            dropdown.sourceItems = items;
            dropdown.selectedIndex = selectedIndex;
            dropdown.RegisterValueChangedCallback(valueChangedCallback);
            typeDropdownContainer.Add(dropdown);
            NodeProperties.Add(typeDropdownContainer);
            return dropdown;
        }

#if UNITY_EDITOR

        private void OnEditScript()
        {
            if (InspectedNode is BehaviorGraphNodeModel aiModel)
            {
                string path = NodeRegistry.GetInfo(aiModel.NodeType).FilePath.Replace("\\", "/");
                CodeEditor.CodeEditor.Editor.CurrentCodeEditor.OpenProject(path);
            }
        }

        private void OnEditNode()
        {
            Dictionary<string, Type> variableSuggestions = Util.GetVariableSuggestions(InspectedNode.Asset);

            if (InspectedNode is ActionNodeModel actionNodeModel)
            {
                NodeInfo info = NodeRegistry.GetInfoFromTypeID(actionNodeModel.NodeTypeID);
                ActionNodeWizardWindow.GetAndShowEditWindow(parent, info, variableSuggestions);
            }
            else if (InspectedNode is ModifierNodeModel modifierNodeModel)
            {
                NodeInfo info = NodeRegistry.GetInfoFromTypeID(modifierNodeModel.NodeTypeID);
                ModifierNodeWizardWindow.GetAndShowEditWindow(parent, info, variableSuggestions);
            }
            else if (InspectedNode is CompositeNodeModel sequencingNodeModel)
            {
                NodeInfo info = NodeRegistry.GetInfoFromTypeID(sequencingNodeModel.NodeTypeID);
                SequencingNodeWizardWindow.GetAndShowEditWindow(parent, info, InspectedNode, variableSuggestions);
            }
        }

#endif
    }
}
