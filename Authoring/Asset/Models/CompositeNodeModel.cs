using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    internal class CompositeNodeModel : BehaviorGraphNodeModel
    {
        [SerializeReference]
        private readonly List<string> m_NamedChildren;

        public override bool HasDefaultOutputPort => m_NamedChildren != null && !m_NamedChildren.Any();
        public override int MaxOutputsAccepted => int.MaxValue;

        internal bool UpdatedPorts = false;

        public CompositeNodeModel(NodeInfo nodeInfo) : base(nodeInfo)
        {
            if (nodeInfo == null)
            {
                return;
            }

            m_NamedChildren = nodeInfo.NamedChildren ?? new List<string>();
        }

        protected CompositeNodeModel(CompositeNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
            m_NamedChildren = nodeModelOriginal.m_NamedChildren ?? new List<string>();
        }

        protected internal override void EnsurePortsAreUpToDate()
        {
            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(NodeTypeID);
            if (nodeInfo == null)
            {
                return;
            }

            List<PortModel> outputPortsToRemove = OutputPortModels.Where(port =>
                port.Name != PortModel.k_OutputPortName && !nodeInfo.NamedChildren.Contains(port.Name)).ToList();

            foreach (PortModel port in outputPortsToRemove)
            {
                RemovePort(port);
            }

            bool portsChanged = false;
            foreach (string childName in nodeInfo.NamedChildren)
            {
                PortModel portModel = FindPortModelByName(childName);
                if (portModel == null)
                {
                    portModel = new PortModel(childName, PortDataFlowType.Output) { IsFloating = true };
                    AddPortModel(portModel);
                    portsChanged = true;
                }
                else if (Asset != null)
                {
                    // Legacy graph support:
                    // Prior to 1.0.14, composite that had their port count changed could generate the PortModel in the CompositeNodeModel.
                    // However, they were doing so without generating the relevant FloatingPortNodeModel in the Asset.Nodes list.
                    bool existInAssetNodes = Asset.Nodes.Find(n => n is FloatingPortNodeModel portNodeModel 
                                          && portNodeModel.ID == this.ID 
                                          && portNodeModel.PortName == portModel.Name) == null;
                    
                    // If it is the case, we refresh the FloatingPortNodeModels for the node here.
                    if (existInAssetNodes == false)
                    {
                        Asset.CreateNodePortsForNode(this);
                        portsChanged = true;
                    }
                }
            }

            if (portsChanged)
            {
                SortOutputPortModelsBy(nodeInfo.NamedChildren);
                UpdatedPorts = true;
            }
        }
    }
}
