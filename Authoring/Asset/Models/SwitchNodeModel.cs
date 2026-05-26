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
        
        // Used for optimization to avoid full resolution path on port validation.
        private Dictionary<string, string> m_LastEnumNameToNumericValue = new(StringComparer.Ordinal);
        private string m_LastEnumTypeId;

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

            // Ensure stale connections are removed before new validations
            GraphAssetUtility.CleanupOrphanedFloatingPortNodesForParent(Asset, ID);
            
            UpdateNodeType();
        }

        protected internal override void EnsurePortsAreUpToDate()
        {
            foreach (FieldModel field in Fields)
            {
                if (field.FieldName == "EnumVariable" && field.LinkedVariable != null && field.LinkedVariable.Type.IsEnum)
                {
                    ValidatePortsFromEnumType(field.LinkedVariable.Type);
                    return;
                }
            }
        }

        private void ValidatePortsFromEnumType(Type enumType)
        {
            List<GraphAssetUtility.EnumValueEntry> enumEntries = GraphAssetUtility.GetEnumValueEntries(enumType).ToList();
            string[] enumNames = enumEntries.Select(entry => entry.Name).ToArray();
            Dictionary<string, string> enumNameToNumericValue = enumEntries.ToDictionary(entry => entry.Name, entry => entry.NumericValue, StringComparer.Ordinal);
            string enumTypeId = GraphAssetUtility.GetEnumTypeId(enumType);
            bool portsChanged = false;
            bool structuralPortChange = false;
            bool portRenamed = false;
            
            // 1. Attempt non-destructive rename only when enum entries changed.
            bool enumEntriesChanged = !string.Equals(m_LastEnumTypeId, enumTypeId, StringComparison.Ordinal)
                                      || !GraphAssetUtility.AreEnumDependencyDictionariesEqual(m_LastEnumNameToNumericValue, enumNameToNumericValue);
            if (enumEntriesChanged)
            {
                portRenamed = TryRenamePortsByNumericValue(enumEntries);
                portsChanged = true;
            }

            // 2. Remove ports that are no longer in the enum.
            List<PortModel> outputPortsToRemove = OutputPortModels.Where(port => !enumNames.Contains(port.Name)).ToList();
            foreach (PortModel outputPort in outputPortsToRemove)
            {
                RemovePort(outputPort);
                portsChanged = true;
                structuralPortChange = true;
            }

            // 3. Add ports that are new in the enum.
            foreach (string enumName in enumNames)
            {
                if (FindPortModelByName(enumName) == null)
                {
                    AddPortModel(new PortModel(enumName, PortDataFlowType.Output) { IsFloating = true }, false);
                    portsChanged = true;
                    structuralPortChange = true;
                }
            }

            // 4. Update floating port nodes with new numeric values.
            if (portsChanged)
            {
                if (structuralPortChange)
                {
                    SortOutputPortModelsBy(enumNames.ToList());
                    GraphAssetUtility.CleanupOrphanedFloatingPortNodesForParent(Asset, ID);
                    Asset.CreateNodePortsForNode(this);
                }

                bool floatingMetadataChanged = UpdateFloatingNodeNumericValues(enumNameToNumericValue);
                if (portRenamed || floatingMetadataChanged)
                {
                    Asset.SetAssetDirty(false);
                }
                
                UpdatedPorts = structuralPortChange;
            }
            else
            {
                bool floatingMetadataChanged = UpdateFloatingNodeNumericValues(enumNameToNumericValue);
                if (floatingMetadataChanged)
                {
                    Asset.SetAssetDirty(false);
                }
                UpdatedPorts = false;
            }

            if (enumEntriesChanged)
            {
                m_LastEnumTypeId = enumTypeId;
                m_LastEnumNameToNumericValue = enumNameToNumericValue;
            }
        }

        private bool TryRenamePortsByNumericValue(IReadOnlyList<GraphAssetUtility.EnumValueEntry> enumEntries)
        {
            if (Asset?.Nodes == null || enumEntries == null || enumEntries.Count == 0)
            {
                return false;
            }

            Dictionary<string, List<string>> enumNamesByNumericValue = enumEntries
                .GroupBy(entry => entry.NumericValue, StringComparer.Ordinal)
                .ToDictionary(
                    grouping => grouping.Key,
                    grouping => grouping.Select(entry => entry.Name).ToList(),
                    StringComparer.Ordinal);

            Dictionary<string, FloatingPortNodeModel> floatingNodesByPortName = Asset.Nodes
                .OfType<FloatingPortNodeModel>()
                .Where(node => node.ParentNodeID == ID && !string.IsNullOrEmpty(node.PortName))
                .GroupBy(node => node.PortName, StringComparer.Ordinal)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.First(), StringComparer.Ordinal);

            HashSet<string> enumNames = new HashSet<string>(enumEntries.Select(entry => entry.Name), StringComparer.Ordinal);
            HashSet<string> usedPortNames = new HashSet<string>(OutputPortModels.Select(port => port.Name), StringComparer.Ordinal);

            bool anyRenamed = false;
            List<PortModel> stalePorts = OutputPortModels.Where(port => !enumNames.Contains(port.Name)).ToList();
            foreach (PortModel stalePort in stalePorts)
            {
                if (!floatingNodesByPortName.TryGetValue(stalePort.Name, out FloatingPortNodeModel floatingNode)
                    || string.IsNullOrEmpty(floatingNode.Metadata))
                {
                    continue;
                }

                if (!enumNamesByNumericValue.TryGetValue(floatingNode.Metadata, out List<string> candidateNames))
                {
                    continue;
                }

                List<string> availableCandidates = candidateNames
                    .Where(candidate => !usedPortNames.Contains(candidate))
                    .ToList();
                if (availableCandidates.Count != 1)
                {
                    continue;
                }

                string renamedPortName = availableCandidates[0];
                usedPortNames.Remove(stalePort.Name);
                stalePort.Name = renamedPortName;
                floatingNode.PortName = renamedPortName;
                usedPortNames.Add(renamedPortName);
                anyRenamed = true;
            }

            return anyRenamed;
        }

        private bool UpdateFloatingNodeNumericValues(IReadOnlyDictionary<string, string> enumNameToNumericValue)
        {
            if (Asset?.Nodes == null || enumNameToNumericValue == null)
            {
                return false;
            }

            bool changed = false;
            foreach (FloatingPortNodeModel floatingNode in Asset.Nodes.OfType<FloatingPortNodeModel>())
            {
                if (floatingNode.ParentNodeID != ID)
                {
                    continue;
                }

                string numericValue = null;
                if (enumNameToNumericValue.TryGetValue(floatingNode.PortName, out string matchedValue))
                {
                    numericValue = matchedValue;
                }

                if (!string.Equals(floatingNode.Metadata, numericValue, StringComparison.Ordinal))
                {
                    floatingNode.Metadata = numericValue;
                    changed = true;
                }
            }

            return changed;
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
