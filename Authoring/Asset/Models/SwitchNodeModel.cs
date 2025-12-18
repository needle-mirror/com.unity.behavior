using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(SwitchComposite))]
    [NodeModelInfo(typeof(SwitchFlagComposite))]
    internal class SwitchNodeModel : BehaviorGraphNodeModel
    {
        [Serializable]
        public enum EnumMode
        {
            Enum,
            FlagEnum,
        }

        [SerializeField]
        private EnumMode m_Mode;
        public EnumMode Mode { get => m_Mode; set => m_Mode = value; }
        public override bool HasDefaultOutputPort => false;
        internal bool UpdatedPorts = false;

        public SwitchNodeModel(NodeInfo nodeInfo) : base(nodeInfo) 
        {
            if (nodeInfo == null || nodeInfo.Type == typeof(SwitchComposite)) Mode = EnumMode.Enum;
            else if (nodeInfo.Type == typeof(SwitchFlagComposite)) Mode = EnumMode.FlagEnum;
        }

        protected SwitchNodeModel(SwitchNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
            this.Mode = nodeModelOriginal.Mode;

            foreach (var outputPortModel in nodeModelOriginal.OutputPortModels)
            {
                AddPortModel(new PortModel(outputPortModel.Name, PortDataFlowType.Output) { IsFloating = true });
            }
        }

        public override void OnValidate()
        {
            base.OnValidate();
            UpdateNodeType();
        }

        protected internal override void EnsurePortsAreUpToDate()
        {
            foreach (FieldModel field in Fields)
            {
                if (field.FieldName == "EnumVariable" && field.LinkedVariable != null && field.LinkedVariable.Type.IsSubclassOf(typeof(Enum)))
                {
                    ValidatePortsFromEnumType(field.LinkedVariable.Type);
                    return;
                }
            }
        }

        private void ValidatePortsFromEnumType(Type enumType)
        {
            string[] enumNames = Enum.GetNames(enumType);
            bool portsChanged = false;
            List<PortModel> outputPortsToRemove = OutputPortModels.Where(port => !enumNames.Contains(port.Name)).ToList();
            foreach (PortModel outputPort in outputPortsToRemove)
            {
                RemovePort(outputPort);
                portsChanged = true;
            }

            foreach (string enumName in enumNames)
            {
                if (FindPortModelByName(enumName) == null)
                {
                    AddPortModel(new PortModel(enumName, PortDataFlowType.Output) { IsFloating = true });
                    portsChanged = true;
                }
            }

            if (portsChanged)
            {
                // Sort the order of the ports.
                SortOutputPortModelsBy(enumNames.ToList());
                Asset.CreateNodePortsForNode(this);
                UpdatedPorts = true;
            }
        }

        private void UpdateNodeType()
        {
            Type expectedType = null;
            switch (Mode)
            {
                case EnumMode.Enum:
                    expectedType = typeof(SwitchComposite);
                    break;

                case EnumMode.FlagEnum:
                    expectedType = typeof(SwitchFlagComposite);
                    break;
            }

            if (NodeType != null && expectedType == NodeType.Type)
            {
                return;
            }

            NodeType = expectedType;
            NodeDescriptionAttribute attribute = expectedType.GetCustomAttribute<NodeDescriptionAttribute>();
            if (attribute != null)
            {
                NodeTypeID = attribute.GUID;
            }

            Asset.SetAssetDirty(true);
        }
    }
}
