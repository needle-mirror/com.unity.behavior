using System;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeUI(typeof(SwitchNodeModel))]
    internal class SwitchNodeUI : BehaviorNodeUI
    {
        private readonly LinkField<Enum, RuntimeEnumField> m_EnumLinkField;
        private VariableModel m_LastAssignedEnumVariable;
        
        public SwitchNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/SwitchNodeStyles.uss"));
            AddToClassList("SwitchNodeUI");
            AddToClassList("TwoLineNode");

            Title = "Switch";
            
            m_EnumLinkField = new BehaviorLinkField<Enum, RuntimeEnumField>()
            {
                style =
                {
                    alignSelf = Align.Center,
                    minHeight = 22
                },
                FieldName = "EnumVariable",
                Model = nodeModel
            };
            
            m_EnumLinkField.RegisterCallback<LinkFieldTypeChangeEvent>(evt =>
            {
                Model.RemoveOutputPortModels();
                
                if (evt.FieldType is { IsEnum: true })
                {
                    foreach (var member in Enum.GetNames(evt.FieldType))
                    {
                        Model.AddPortModel(new PortModel(member, PortDataFlowType.Output) { IsFloating = true });
                    }
                    Model.Asset.CreateNodePortsForNode(Model);
                    this.schedule.Execute(AlignImmediateChildren);
                }
            });

            m_EnumLinkField.OnLinkChanged += (newLink =>
            {
                m_EnumLinkField.Dispatcher.DispatchImmediate(new SetNodeVariableLinkCommand(Model, m_EnumLinkField.FieldName, m_EnumLinkField.LinkVariableType, m_EnumLinkField.LinkedVariable, true));
                if (newLink == null)
                {
                    Model.RemoveOutputPortModels();
                    RefreshOutputPortUIs();
                }
            });
            m_EnumLinkField.Q<RuntimeEnumField>().label = "Choose an Enum";


            NodeValueContainer.Add(m_EnumLinkField);

            m_LastAssignedEnumVariable = m_EnumLinkField.LinkedVariable;
        }

        public override void Refresh(bool isDragging)
        {
            base.Refresh(isDragging);
            if (m_EnumLinkField.LinkedVariable != m_LastAssignedEnumVariable)
            {
                RefreshOutputPortUIs();
            }
            m_LastAssignedEnumVariable = m_EnumLinkField.LinkedVariable;

            if (Model is SwitchNodeModel model && model.UpdatedPorts)
            {
                AlignImmediateChildren();
                model.UpdatedPorts = false;
            }
        }

        private void AlignImmediateChildren()
        {
            this.schedule.Execute(_ =>
            {
                var nodePositions = GraphUILayoutUtility.ComputeChildNodePositions(this);
                GraphUILayoutUtility.ScheduleNodeMovement(this, Model.Asset, nodePositions);
            });
        }
        
        private void RefreshOutputPortUIs()
        {
            // Clear output port UIs.
            OutputPortsContainer.Clear();
            
            // Create new port UIs.
            foreach (PortModel portModel in Model.OutputPortModels)
            {
                var portUIContainer = CreatePortUI(portModel);
                if (portUIContainer == null)
                {
                    throw new Exception(
                        $"The port UI created for {portModel.Name} does not contain a element of type {nameof(Port)}, which is required.");
                }
                
                OutputPortsContainer.Add(portUIContainer);
            }
        }
    }
}