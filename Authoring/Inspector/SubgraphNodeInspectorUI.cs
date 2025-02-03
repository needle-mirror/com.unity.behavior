using System;
using System.Collections;
using System.Collections.Generic;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Behavior
{
    [NodeInspectorUI(typeof(SubgraphNodeModel))]
    internal class SubgraphNodeInspectorUI : BehaviorGraphNodeInspectorUI
    {
        private SubgraphNodeModel m_NodeModel => InspectedNode as SubgraphNodeModel;
        private BaseLinkField m_SubgraphField;
        private BaseLinkField m_BlackboardAssetField;
        private const string k_StaticNodeTitle = "Run Subgraph";
        private const string k_DynamicNodeTitle = "Run Subgraph Dynamically";
        private const string k_DefaultDescription = "Running subgraphs allows you to keep your graphs clean and to switch out functionality at runtime using dynamic subgraphs.";
        private const string k_DynamicNodeDescription = "You're going to run a subgraph dynamically. Make sure you have your subgraph implement a Blackboard asset that you can refer to in this inspector.";

        private readonly HashSet<SerializableGUID> m_AssignedFieldModels = new HashSet<SerializableGUID>();
        
        public SubgraphNodeInspectorUI(NodeModel nodeModel) : base(nodeModel) { }

        public override void Refresh()
        {
            UpdateInspectorTitleAndDescription();

            NodeProperties.Clear();
            CreateSubgraphField();
            if (m_NodeModel.IsDynamic)
            {
                CreateBlackboardAssetField();
            }
            else
            {
                if (m_SubgraphField.LinkedVariable != null)
                {
                    CreateSubgraphRepresentationToggle();
                    CreateBlackboardFields();     
                }
            }
        }

        private void CreateSubgraphRepresentationToggle()
        {
            VisualElement representationToggleElement = new VisualElement();
            representationToggleElement.name = "ToggleOptionField";
            representationToggleElement.AddToClassList("ToggleOptionField");
            representationToggleElement.Add(new Label("Subgraph Representation"));
            representationToggleElement.tooltip = "Show the subgraph representation on the node UI.";
            Toggle toggle = new Toggle();
            representationToggleElement.Add(toggle);
            
            toggle.value = m_NodeModel.ShowStaticSubgraphRepresentation;
            toggle.RegisterValueChangedCallback(evt =>
            {
                m_NodeModel.ShowStaticSubgraphRepresentation = evt.newValue;
                m_NodeModel?.Asset.SetAssetDirty();
            });
            
            NodeProperties.Add(representationToggleElement);
        }

        private void UpdateInspectorTitleAndDescription()
        {
            Title = m_NodeModel.IsDynamic ? k_DynamicNodeTitle : k_StaticNodeTitle;
            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(m_NodeModel.NodeTypeID);
            if (m_NodeModel.SubgraphField.LinkedVariable == null)
            {
                // Show a default description text before a subgraph has been assigned.
                Description = k_DefaultDescription;
            }
            else
            {
                // Set the description depending on if the graph is being run dynamically or not.
                Description = m_NodeModel.IsDynamic ? k_DynamicNodeDescription : nodeInfo.Description;   
            }
        }

        private void CreateSubgraphField()
        {
            if (m_SubgraphField == null)
            {
                // Typically, we'd use CreateField() here, but we need to use a different field type for the subgraph field,
                // which isn't supported by LinkFieldUtility.CreateForType(name, type).
                m_SubgraphField = new BaseLinkField { 
                    FieldName = SubgraphNodeModel.k_SubgraphFieldName, 
                    LinkVariableType = typeof(BehaviorGraph),
                    AllowAssetEmbeds = true,
                    Model = InspectedNode
                };
                m_SubgraphField.OnLinkChanged += _ =>
                {
                    m_SubgraphField.Dispatcher.DispatchImmediate(new SetNodeVariableLinkCommand(m_NodeModel, m_SubgraphField.FieldName, m_SubgraphField.LinkVariableType, m_SubgraphField.LinkedVariable, true));

                    m_NodeModel.OnValidate();
                    m_NodeModel.CacheRuntimeGraphId();

                    // Reset the blackboard field if there is no subgraph variable linked.
                    if (m_SubgraphField.LinkedVariable == null)
                    {
                        if (m_BlackboardAssetField != null)
                        {
                            m_BlackboardAssetField.LinkedVariable = null;      
                        }
                    }
                };
                
                m_SubgraphField.AddToClassList("LinkField-Light");
                m_SubgraphField.RegisterCallback<LinkFieldTypeChangeEvent>(_ =>
                {
                    Refresh();
                });
            }
            
            VisualElement fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("Inspector-FieldContainer");
            fieldContainer.Add(new Label(SubgraphNodeModel.k_SubgraphFieldName));
            fieldContainer.Add(m_SubgraphField);
            
            NodeProperties.Add(fieldContainer);

            // Update the inspector LinkField value from the NodeModel LinkedVariable value.
            m_SubgraphField.LinkedVariable = m_NodeModel.SubgraphField.LinkedVariable == null ? null : m_NodeModel.SubgraphField.LinkedVariable;
        }
        
        private void CreateBlackboardAssetField()
        {
            if (m_BlackboardAssetField == null)
            {
                // Typically, we'd use CreateField() here, but we need to use a different field type for the blackboard asset field,
                // which isn't supported by LinkFieldUtility.CreateForType(name, type).
                m_BlackboardAssetField = new BaseLinkField { 
                    FieldName = SubgraphNodeModel.k_BlackboardFieldName, 
                    LinkVariableType = typeof(BehaviorBlackboardAuthoringAsset),
                    AllowAssetEmbeds = true,
                    Model = InspectedNode
                };
                m_BlackboardAssetField.OnLinkChanged += _ =>
                {
                    m_BlackboardAssetField.Dispatcher.DispatchImmediate(new SetNodeVariableLinkCommand(m_NodeModel, m_BlackboardAssetField.FieldName, m_BlackboardAssetField.LinkVariableType, m_BlackboardAssetField.LinkedVariable, true));
                    m_NodeModel.OnValidate();
                };
                m_BlackboardAssetField.AddToClassList("LinkField-Light");
            }
            
            VisualElement fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("Inspector-FieldContainer");
            fieldContainer.Add(new Label("Blackboard"));
            fieldContainer.Add(m_BlackboardAssetField);
            
            NodeProperties.Add(fieldContainer);

            if (m_NodeModel.BlackboardAssetField?.LinkedVariable != null)
            {
                m_BlackboardAssetField.LinkedVariable = m_NodeModel.BlackboardAssetField.LinkedVariable;
            }

            CreateRequiredBlackboardAssetFields();
        }

        private void CreateBlackboardFields()
        {
            m_AssignedFieldModels.Clear();
            if (m_NodeModel.RuntimeSubgraph == null)
            {
                return;
            }

            if (m_NodeModel.SubgraphAuthoringAsset == null)
            {
                return;
            }
            
            Divider divider = new Divider();
            divider.size = Size.S;
            divider.AddToClassList("FieldDivider");
            NodeProperties.Add(divider);
            var subgraphNodeMode  = InspectedNode as SubgraphNodeModel;
            foreach (VariableModel variable in m_NodeModel.SubgraphAuthoringAsset.Blackboard.Variables)
            {
                CreateAssignVariableFieldElement(variable, subgraphNodeMode.Fields, m_NodeModel.RuntimeSubgraph.BlackboardReference.Blackboard.Variables);
            }
        }
        
        private void CreateRequiredBlackboardAssetFields()
        {
            m_AssignedFieldModels.Clear();
            if (m_NodeModel.BlackboardAssetField?.LinkedVariable == null)
            {
                return;
            }
            
            Divider divider = new Divider();
            divider.size = Size.S;
            divider.AddToClassList("FieldDivider");
            NodeProperties.Add(divider);

            if (m_NodeModel.RequiredBlackboard.Variables.Count == 0)
            {
                NodeProperties.Add(new Label("Blackboard is empty"));
            }
            
            var subgraphNodeMode  = InspectedNode as SubgraphNodeModel;
            foreach (VariableModel variable in m_NodeModel.RequiredBlackboard.Variables)
            {
                CreateAssignVariableFieldElement(variable, subgraphNodeMode.Fields, m_NodeModel.RequiredBlackboard.RuntimeBlackboardAsset.Blackboard.Variables);
            }
        }

        private void CreateAssignVariableFieldElement(VariableModel variable, IEnumerable<BehaviorGraphNodeModel.FieldModel> fields, List<BlackboardVariable> blackboardVariables)
        {
            if (variable.IsShared)
            {
                CreateSharedVariableElement(variable.Name, variable.Type);
            }
            else if (variable.IsExposed)
            {
                BaseLinkField field = CreateField(variable.Name, variable.Type);
                
                AddRightClickRevertContextManipulator(variable, field, fields, blackboardVariables);
                
                if (field.LinkedVariable != null)
                {
                    SetOverrideText(field);
                    return;
                }
                
                field.RegisterCallback<LinkFieldValueChangeEvent>(_ =>
                {
                    m_NodeModel.SetVariableOverride(variable.ID, true);
                });
                
                field.OnLinkChanged += _ =>
                {
                    m_NodeModel.SetVariableOverride(variable.ID, true);
                };

                AssignSubgraphVariable(blackboardVariables, fields, field);

                if (typeof(IList).IsAssignableFrom(variable.Type))
                {
                    var inputField = field.Q<VisualElement>("InputField");
                    if (inputField != null)
                    {
                        inputField.Q<Label>().text =
                            $"{m_SubgraphField.LinkedVariable.Name} {BlackboardUtils.GetArrowUnicode()} {variable.Name}";
                    }
                }
                else if (m_NodeModel.IsVariableOverridden(variable.ID))
                {
                    SetOverrideText(field);
                }
            }
        }

        private void AddRightClickRevertContextManipulator(VariableModel variable, BaseLinkField field, IEnumerable<BehaviorGraphNodeModel.FieldModel>  fieldModels, List<BlackboardVariable> blackboardVariables)
        {
            var visualElement = field.GetFirstAncestorOfType<VisualElement>();
            var label = visualElement.Q<Label>();
            
            label.AddManipulator(new ContextMenuManipulator(() =>
            {
                if (!m_NodeModel.IsVariableOverridden(variable.ID))
                {
                    return;
                }
#if UNITY_EDITOR
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Revert Variable"), false, () =>
                {
                    ResetVariableOverride(variable.ID, field, fieldModels, blackboardVariables);
                });
                menu.ShowAsContext();             
#endif

            }));
        }

        private void ResetVariableOverride(SerializableGUID guid, BaseLinkField field, IEnumerable<BehaviorGraphNodeModel.FieldModel> fieldModels, List<BlackboardVariable> blackboardVariables)
        {
            m_NodeModel.SetVariableOverride(guid, false);
            if (field.LinkedVariable != null)
            {
                field.LinkedVariable = null;
            }

            AssignSubgraphVariable(blackboardVariables, fieldModels, field, true);
            m_NodeModel.Asset.SetAssetDirty();
            Refresh();
        }

        private bool AssignSubgraphVariable(List<BlackboardVariable> variables, IEnumerable<BehaviorGraphNodeModel.FieldModel> fieldModels, BaseLinkField field, bool revertOverride = false)
        {
            foreach (var subgraphVariable in variables)
            {
                if (m_AssignedFieldModels.Contains(subgraphVariable.GUID))
                {
                    continue;
                }
                
                if (field.LinkVariableType != subgraphVariable.Type || !field.FieldName.Equals(subgraphVariable.Name,
                        StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }
                
                foreach (var fieldModel in fieldModels)
                {
                    if (fieldModel.Type.Type == subgraphVariable.Type && fieldModel.FieldName.Equals(subgraphVariable.Name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (!m_NodeModel.IsVariableOverridden(subgraphVariable.GUID) || revertOverride)
                        {
                            field.SetValueWithoutNotify(subgraphVariable.ObjectValue);
                            fieldModel.LocalValue.ObjectValue = subgraphVariable.ObjectValue;
                            m_AssignedFieldModels.Add(subgraphVariable.GUID);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void SetOverrideText(BaseLinkField field)
        {
            var visualElement = field.GetFirstAncestorOfType<VisualElement>();
            if (visualElement != null)
            {
                string nicifiedFieldName = Util.NicifyVariableName(field.FieldName);
                var label = visualElement.Q<Label>();
                label.text = string.Format("{0} (Override)", nicifiedFieldName); 
            }
        }

        private void CreateSharedVariableElement(string fieldName, Type fieldType)
        {
            BaseLinkField field = LinkFieldUtility.CreateNodeLinkField(fieldName, fieldType);
            VisualElement fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("Inspector-FieldContainer");
            Label label = new Label(fieldName);
            label.AddToClassList("SharedVariableLabel");
            VisualElement labelContainer = new VisualElement();
            labelContainer.name = "SharedVariableLabelContainer";
            labelContainer.Add(label);
            fieldContainer.Add(labelContainer);
            fieldContainer.Add(field);
            NodeProperties.Add(fieldContainer);
            field.AddToClassList("LinkField-Light");
            field.SetEnabled(false);
            field.tooltip = "Variables marked as \"Shared\" can not be assigned through a subgraph node. Set the shared variable value from the blackboard that it belongs to.";
        }
    }
}