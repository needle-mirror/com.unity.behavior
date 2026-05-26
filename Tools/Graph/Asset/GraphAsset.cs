using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Behavior.GraphFramework
{
    internal class GraphAsset : ScriptableObject
    {
        [SerializeField]
        public BlackboardAsset Blackboard;

        [SerializeField]
        private string m_Description;
        public string Description
        {
            get => m_Description;
            set => m_Description = value;
        }

        [SerializeReference]
        private List<NodeModel> m_Nodes = new();
        public List<NodeModel> Nodes
        {
            get => m_Nodes;
            set => m_Nodes = value;
        }

        /// <summary>
        /// Does the asset needs to rebuilt its data.
        /// </summary>
        internal bool HasOutstandingChanges { get; set; }

        [SerializeField]
        [HideInInspector]
        internal long m_VersionTimestamp;
        public long VersionTimestamp => m_VersionTimestamp;

        public void MarkUndo(string description, bool hasOutstandingChange = true)
        {
#if UNITY_EDITOR
            // For any change not registered through dispatcher, we need to manually provide additional information.
            if (hasOutstandingChange && description.Contains("(outstanding)") == false)
            {
                description += $" (outstanding)";
            }

            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (description.Contains(assetPath) == false)
            {
                description += $" ({assetPath})";
            }

            UnityEditor.Undo.RegisterCompleteObjectUndo(this, description);
#endif
            // There are still a few lingering non-command changes to asset data preceded by MarkUndo() calls.
            // In order to pick up these changes, set the asset dirty here too.
            SetAssetDirty(hasOutstandingChange);
        }

        public virtual void SaveAsset()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
        }

        public void SetAssetDirty(bool setHasOutStandingChange = true)
        {
            m_VersionTimestamp = DateTime.Now.Ticks;
            HasOutstandingChanges |= setHasOutStandingChange;
#if UNITY_EDITOR
            if (!UnityEditor.EditorUtility.IsDirty(this))
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        internal virtual void ValidateAsset()
        {
            // Clean up orphaned floating port nodes before validation
            GraphAssetUtility.CleanupOrphanedFloatingPortNodes(this);
            
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                NodeModel node = Nodes[i];
                // holdovers for supporting older graphs - remove for 1.0.0
                if (node.Asset == null)
                {
                    node.Asset = this;
                }
                if (!node.HasPortModels)
                {
                    node.OnDefineNode();
                }

                node.OnValidate();
            }

            Blackboard?.ValidateAsset();
        }

        protected virtual void OnEnable()
        {
            EnsureAssetHasBlackboard();
        }

        internal virtual void EnsureAssetHasBlackboard()
        {
            string blackboardName = name + " Blackboard";
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            BlackboardAsset blackboard = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)
                .FirstOrDefault(asset => asset is BlackboardAsset) as BlackboardAsset;
            if (blackboard != null)
            {
                if (Blackboard != null && blackboard == Blackboard)
                {
                    // Update the graph Blackboard name if needed.
                    if (blackboard.name != blackboardName)
                    {
                        blackboard.name = blackboardName;
                    }
                }
                return;
            }
#endif
            if (Blackboard == null)
            {
                Blackboard = CreateInstance<BlackboardAsset>();
                Blackboard.name = blackboardName;
            }

#if UNITY_EDITOR
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            UnityEditor.AssetDatabase.AddObjectToAsset(Blackboard, this);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        #region Model changing API
        public NodeModel CreateNode(Type nodeType, Vector2 position, PortModel connectedPort = null, object[] args = null)
        {
            var node = Activator.CreateInstance(nodeType, args) as NodeModel;
            if (node == null)
            {
                Debug.LogError($"Failed to create node of type {nodeType}");
                return null;
            }
            node.Asset = this;
            node.Position = position;
            node.OnDefineNode();
            Nodes.Add(node);

            // Mark asset dirty whenever nodes are created (including FloatingPortNodeModel during validation)
            SetAssetDirty(true);

            // Connect the node to the specified port.
            if (connectedPort != null)
            {
                if (connectedPort.IsInputPort && node.TryDefaultOutputPortModel(out PortModel portModelTarget))
                {
                    portModelTarget.ConnectTo(connectedPort);
                }
                else if (connectedPort.IsOutputPort && node.TryDefaultInputPortModel(out portModelTarget))
                {
                    connectedPort.ConnectTo(portModelTarget);
                }
            }

            CreateNodePortsForNode(node);

            return node;
        }

        internal void CreateNodePortsForNode(NodeModel parentNode)
        {
            if (parentNode == null)
            {
                return;
            }

            const float horizontalSpacing = 200.0f;
            List<PortModel> nonDefaultOutputPorts = parentNode.OutputPortModels
                .Where(portModel => !portModel.IsDefaultOutputPort)
                .ToList();
            int portCount = nonDefaultOutputPorts.Count;
            if (portCount == 0)
            {
                return;
            }

            float leftMostOffsetX = -0.5f * (portCount - 1) * horizontalSpacing;
            int index = 0;

            foreach (PortModel portModel in nonDefaultOutputPorts)
            {
                float offsetX = leftMostOffsetX + index * horizontalSpacing;
                Vector2 targetPosition = parentNode.Position + new Vector2(offsetX, 120.0f);

                // Check if a FloatingPortNodeModel already exists for this port
                FloatingPortNodeModel existingNode = FindFloatingPortNodeForPort(parentNode, portModel);

                if (existingNode != null)
                {
                    // Update position of existing floating port node
                    if (existingNode.Position != targetPosition)
                    {
                        existingNode.Position = targetPosition;
                        SetAssetDirty(setHasOutStandingChange: false);
                    }
                }
                else if (portModel.Connections.Count() == 0)
                {
                    // Create new floating port node only if no connections exist
                    CreateNode(typeof(FloatingPortNodeModel), targetPosition, portModel,
                        new object[] { parentNode.ID, portModel.Name });
                }

                index++;
            }
        }

        private FloatingPortNodeModel FindFloatingPortNodeForPort(NodeModel parentNode, PortModel portModel)
        {
            if (parentNode == null || portModel == null)
            {
                return null;
            }

            // Look through connections to find if a FloatingPortNodeModel is connected to this port
            foreach (PortModel connection in portModel.Connections)
            {
                if (connection?.NodeModel is FloatingPortNodeModel floatingNode &&
                    floatingNode.ParentNodeID == parentNode.ID &&
                    floatingNode.PortName == portModel.Name)
                {
                    return floatingNode;
                }
            }

            return null;
        }

        public void DeleteNode(NodeModel node)
        {
            if (!Nodes.Contains(node)) //node has already been deleted. Ex: it was a part of a sequence.
                return;

            DeleteNodePortsForNode(node);

            List<NodeModel> nodesToDelete = new List<NodeModel>();
            foreach (PortModel inputPort in node.InputPortModels)
            {
                foreach (PortModel connection in inputPort.Connections.ToList())
                {
                    DeleteEdge(connection, inputPort);
                }
            }

            foreach (PortModel outputPort in node.OutputPortModels)
            {
                foreach (PortModel connection in outputPort.Connections.ToList())
                {
                    DeleteEdge(outputPort, connection);
                }
            }

            // If the node is a sequence, delete the nested nodes too.
            if (node is SequenceNodeModel sequenceModel)
            {
                nodesToDelete.AddRange(sequenceModel.Nodes);
            }
            else
            {
                // Otherwise, the node may be nested within a sequence group, so remove it there as well.
                // Note: This only affects sequences of actions nested together, not nodes linked with edges in an
                // implicit sequence.
                foreach (NodeModel parent in node.Parents)
                {
                    if (parent is not SequenceNodeModel sequence)
                    {
                        continue;
                    }

                    // Remove link between the sequence and the node being deleted.
                    sequence.Nodes.Remove(node);
                    if (sequence.Nodes.Count >= 2)
                    {
                        continue;
                    }

                    // If the sequence only has one child, remove the sequence and connect the parent to the child.
                    if (sequence.Nodes.Count == 1)
                    {
                        NodeModel child = sequence.Nodes.First();
                        child.Position = sequence.Position;
                        child.Parents.Remove(sequence);
                        sequence.Nodes.Remove(child);

                        // Connect the parent's edges to the remaining child.
                        PortModel sequenceOutputConnection = sequence.OutgoingConnections.FirstOrDefault();
                        if (sequenceOutputConnection != null && child.TryDefaultOutputPortModel(out PortModel childOutputPortModel))
                        {
                            ConnectEdge(childOutputPortModel, sequenceOutputConnection);
                        }
                        PortModel sequenceInputConnection = sequence.IncomingConnections.FirstOrDefault();
                        if (sequenceInputConnection != null && child.TryDefaultInputPortModel(out PortModel childInputPortModel))
                        {
                            ConnectEdge(childInputPortModel, sequenceInputConnection);
                        }
                    }

                    // Since the sequence no longer contains nodes, delete it.
                    nodesToDelete.Add(sequence);
                }
            }
            Nodes.Remove(node);
            
            // Mark asset dirty whenever nodes are deleted
            SetAssetDirty(true);

            foreach (NodeModel nodeToDelete in nodesToDelete)
            {
                if (Nodes.Contains(nodeToDelete))
                {
                    DeleteNode(nodeToDelete);
                }
            }
        }

        internal void DeleteNodePortsForNode(NodeModel node)
        {
            foreach (PortModel portModel in node.OutputPortModels)
            {
                foreach (PortModel connectedPort in portModel.Connections.ToArray())
                {
                    if (connectedPort.NodeModel is FloatingPortNodeModel)
                    {
                        DeleteNode(connectedPort.NodeModel);
                    }
                }
            }
        }

        public void ConnectEdge(PortModel startPort, PortModel endPort)
        {
            Assert.IsTrue(Nodes.Contains(startPort.NodeModel), $"Asset {this} does not contain node {startPort.NodeModel}.");
            Assert.IsTrue(Nodes.Contains(endPort.NodeModel), $"Asset {this} does not contain node {endPort.NodeModel}.");

            startPort.ConnectTo(endPort);
        }

        public void DeleteEdge(PortModel startPort, PortModel endPort)
        {
            if (startPort == null || endPort == null)
            {
                return;
            }

            startPort.RemoveConnectionTo(endPort);
            endPort.RemoveConnectionTo(startPort);
        }

        public void RemoveNodeFromSequence(NodeModel node)
        {
            Assert.IsTrue(Nodes.Contains(node), $"Asset {this} does not contain node {node}.");
            foreach (NodeModel parent in node.Parents)
            {
                if (parent is SequenceNodeModel parentSequence)
                {
                    parentSequence.Nodes.Remove(node);
                }
            }
        }

        public void AddNodeToSequence(NodeModel node, SequenceNodeModel sequence, int index)
        {
            Assert.IsTrue(Nodes.Contains(node), $"Asset {this} does not contain node {node}.");
            Assert.IsTrue(Nodes.Contains(sequence), $"Asset {this} does not contain node {sequence}.");

            // Remove node from any existing parent sequences
            foreach (NodeModel parent in node.Parents)
            {
                if (parent is SequenceNodeModel parentSequence)
                {
                    parentSequence.Nodes.Remove(node);
                }
            }

            // Add node to the new sequence model
            if (index >= sequence.Nodes.Count)
            {
                sequence.Nodes.Add(node);
            }
            else
            {
                sequence.Nodes.Insert(index, node);
            }

            // disconnect ports
            foreach (PortModel portModel in node.AllPortModels)
            {
                foreach (PortModel connectedPort in portModel.Connections)
                {
                    connectedPort.RemoveConnectionTo(portModel);
                }
                portModel.ClearConnections();
            }
            node.Parents.Clear(); // disconnect from previous parents
            node.Parents.Add(sequence); // add parent link to sequence
        }
        #endregion
    }
}
