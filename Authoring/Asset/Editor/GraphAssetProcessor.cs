using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor.Callbacks;
#endif
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Behavior
{
    internal class GraphAssetProcessor
    {
        internal const BindingFlags k_bindingFlags = BindingFlags.Instance | BindingFlags.Public;

        private readonly Dictionary<SerializableGUID, BlackboardVariable> m_VariableModelToVariable = new();
        private readonly Dictionary<NodeModel, Node> m_NodeModelToNode = new();

        private readonly BehaviorAuthoringGraph m_Asset;
        internal BehaviorAuthoringGraph Asset => m_Asset;

        private BehaviorGraphModule m_GraphModule;
        internal BehaviorGraphModule GraphModule => m_GraphModule;

        internal BlackboardReference BlackboardReference => m_GraphModule.BlackboardReference;
        internal List<BlackboardReference> BlackboardReferences => m_GraphModule.BlackboardGroupReferences;
        private readonly BehaviorGraph m_Graph;
        internal BehaviorGraph Graph => m_Graph;

        private static Dictionary<Type, INodeTransformer> s_NodeTypeToNodeTransformerDictionary;
        private static List<IBlackboardVariableConverter> s_BlackboardVariableConverters;
        private static List<GraphAssetProcessor> s_ProcessorsPendingRebuild = new List<GraphAssetProcessor>();

#if UNITY_EDITOR
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            CreateVariableConverters();
            s_NodeTypeToNodeTransformerDictionary = new Dictionary<Type, INodeTransformer>();

            var cachedNodeTransformers = UnityEditor.TypeCache.GetTypesDerivedFrom<INodeTransformer>();
            foreach (Type type in cachedNodeTransformers)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                var nodeTransformer = Activator.CreateInstance(type) as INodeTransformer;
                if (typeof(NodeModel).IsAssignableFrom(nodeTransformer.NodeModelType))
                {
                    s_NodeTypeToNodeTransformerDictionary.Add(nodeTransformer.NodeModelType, nodeTransformer);
                }
            }

            if (s_ProcessorsPendingRebuild.Count == 0)
            {
                return;
            }

            foreach (var processor in s_ProcessorsPendingRebuild)
            {
                processor.ProcessGraph();
            }

            s_ProcessorsPendingRebuild.Clear();
        }
#endif

        private static void CreateVariableConverters()
        {
#if UNITY_EDITOR
            s_BlackboardVariableConverters = new List<IBlackboardVariableConverter>();
            var blackboardVariableConverters = UnityEditor.TypeCache.GetTypesDerivedFrom<IBlackboardVariableConverter>();
            foreach (Type type in blackboardVariableConverters)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                var converter = Activator.CreateInstance(type) as IBlackboardVariableConverter;
                s_BlackboardVariableConverters.Add(converter);
            }
#endif
        }

        private static INodeTransformer GetNodeTransformer(Type nodeModelType)
        {
            if (s_NodeTypeToNodeTransformerDictionary == null)
            {
                return null;
            }

            if (s_NodeTypeToNodeTransformerDictionary.TryGetValue(nodeModelType, out INodeTransformer nodeTransformer))
            {
                return nodeTransformer;
            }

            // Attempt finding a transformer for the base type of the node model.
            Type nodeModelBaseType = nodeModelType.BaseType;
            if (nodeModelBaseType != null)
            {
                return GetNodeTransformer(nodeModelBaseType);
            }
            return null;
        }

        internal GraphAssetProcessor(BehaviorAuthoringGraph graphAsset, BehaviorGraph graph)
        {
            m_Asset = graphAsset;
            m_Graph = graph;

            // m_GraphModule is going to replace the Graph.RootGraph and be set as the first element of Graph.Graphs.
            m_GraphModule = new BehaviorGraphModule { AuthoringAssetID = graphAsset.AssetID };
            m_Graph?.Graphs.Add(m_GraphModule);
        }

        internal void ProcessGraph()
        {
            // If the node transformer dictionary is not yet initialized, we need to wait for the next script reload.
            // This can happen if the processor is created before the script reload event is called
            // - i.e. an InitializeOnLoad class calling AssetDatabase.SaveAssets.
            if (s_NodeTypeToNodeTransformerDictionary == null)
            {
                s_ProcessorsPendingRebuild.Add(this);
                return;
            }

            Cleanup();
            InitializeBlackboard();

            m_Graph.RootGraph = BuildGraph();
#if UNITY_EDITOR
            m_Graph.m_DebugInfo = m_Asset.GetOrCreateGraphDebugInfo(m_Asset);
#endif
        }

        internal BehaviorGraphModule BuildGraph()
        {
            // Set the graph version timestamp
            m_GraphModule.VersionTimestamp = m_Asset.m_VersionTimestamp;

            if (m_Asset.Roots.Count == 0)
            {
                return m_GraphModule;
            }

            if (m_Asset.Roots.Count == 1)
            {
                var firstRootNode = m_Asset.Roots[0];
                m_GraphModule.Root = BuildBranch(firstRootNode);
            }
            else
            {
                var parallelRoot = new ParallelAllComposite { Graph = m_GraphModule };

                // Iterating backwards to maintain StartOnEvent model's order.
                for (var index = 0; index < m_Asset.Roots.Count; index++)
                {
                    var root = m_Asset.Roots[index];
                    var child = BuildBranch(root);
                    if (child != null)
                    {
                        if (root is StartOnEventModel)
                        {
                            parallelRoot.Insert(0, child);
                        }
                        else
                        {
                            parallelRoot.Add(child);
                        }
                    }
                }

                m_GraphModule.Root = parallelRoot;
            }

            return m_GraphModule;
        }

        internal void Cleanup()
        {
            m_GraphModule.Root = null;
            m_VariableModelToVariable.Clear();
            m_NodeModelToNode.Clear();
        }

        internal void InitializeBlackboard(Dictionary<SerializableGUID, BlackboardVariable> variableOverrides = null)
        {
            // 1. Regenerate the embedded RuntimeBlackboardAsset if needed
            BehaviorBlackboardAuthoringAsset asset = m_Asset.Blackboard as BehaviorBlackboardAuthoringAsset;
            if (asset != null && (BlackboardReference.SourceBlackboardAsset == null
                || BlackboardReference.SourceBlackboardAsset.AssetID != asset.AssetID
                || BlackboardReference.SourceBlackboardAsset.VersionTimestamp != asset.VersionTimestamp))
            {
                BlackboardReference.SourceBlackboardAsset = asset.BuildRuntimeBlackboard();
            }

            // 2. Apply the BBVs override
            ReplaceBlackboardWithOverrides(BlackboardReference, variableOverrides);

            // 3. Generate GUID to BBV lookup table
            m_VariableModelToVariable.Clear();
            foreach (var blackboardVariable in BlackboardReference.Blackboard.Variables)
            {
                m_VariableModelToVariable.Add(blackboardVariable.GUID, blackboardVariable);
            }
            if (variableOverrides != null)
            {
                foreach (var variableOverride in variableOverrides)
                {
                    m_VariableModelToVariable[variableOverride.Key] = variableOverride.Value;
                }
            }

            // 4. Update the blackboard asset reference
            BlackboardReferences.Clear();
            foreach (BehaviorBlackboardAuthoringAsset blackboard in m_Asset.m_Blackboards)
            {
                BlackboardReference reference = new BlackboardReference();
                reference.SourceBlackboardAsset = blackboard.BuildRuntimeBlackboard();
                ReplaceBlackboardWithOverrides(reference, variableOverrides);
                foreach (var blackboardVariable in reference.Blackboard.Variables)
                {
                    m_VariableModelToVariable[blackboardVariable.GUID] = blackboardVariable;
                }
                BlackboardReferences.Add(reference);
            }
        }

        private void ReplaceBlackboardWithOverrides(BlackboardReference blackboardReference, Dictionary<SerializableGUID, BlackboardVariable> variableOverrides = null)
        {
            if (variableOverrides == null)
            {
                return;
            }

            foreach (var variableOverride in variableOverrides)
            {
                if (blackboardReference.SourceBlackboardAsset.IsSharedVariable(variableOverride.Key))
                {
                    continue;
                }

                blackboardReference.Blackboard.ReplaceBlackboardVariable(variableOverride.Key, variableOverride.Value);
            }
        }

        // Recursively builds the graph branch from a given node downward.
        private Node BuildBranch(NodeModel branchRootNode)
        {
            if (branchRootNode is FloatingPortNodeModel)
            {
                int numOutgoingConnections = branchRootNode.OutgoingConnections.Count();
                Assert.IsTrue(numOutgoingConnections < 2, "PortNodeModel with more than one outgoing connection is not supported");
                if (numOutgoingConnections == 1)
                {
                    return BuildBranch(branchRootNode.OutgoingConnections.First().NodeModel);
                }
                return null;
            }

            // Otherwise, do we have an implicit sequence? (a single action node with links)?
            if (IsImplicitSequence(branchRootNode))
            {
                // todo: this temporary SequenceNodeModel will be added to the cache "m_NodeModelToNode"
                // I would assume it does not work to be used as key, and keep adding new entries for the same action, potentially
                var sequenceNodeModelTemp = new SequenceNodeModel { ID = branchRootNode.ID }; // why reuse the ID?
                sequenceNodeModelTemp.Nodes.Add(branchRootNode);
                sequenceNodeModelTemp.SetPortModels(branchRootNode.AllPortModels);
                return BuildBranch(sequenceNodeModelTemp);
            }

            if (branchRootNode is SequenceNodeModel or BehaviorGraphNodeModel)
            {
                return GetOrCreateNode(branchRootNode);
            }

            Debug.LogWarning($"Invalid node model, {branchRootNode}, cannot be processed to a runtime node.");
            return null;
        }

        private void CreateAndAddConnections(IParent parent, NodeModel nodeModel)
        {
            foreach (PortModel outputPortModel in nodeModel.OutputPortModels)
            {
                List<NodeModel> connections = GetSortedConnections(outputPortModel);
                // Dirty hack for switches to work
                if (connections.Count == 0 && parent is SwitchComposite)
                {
                    parent.Add(null);
                }

                foreach (NodeModel connection in connections)
                {
                    if (!m_NodeModelToNode.TryGetValue(connection, out Node child))
                    {
                        child = BuildBranch(connection);
                    }

                    // Check for the switch hack
                    if (child == null && parent is not SwitchComposite)
                    {
                        continue;
                    }

                    parent.Add(child);

                    if (!outputPortModel.IsDefaultOutputPort && TryFindingField(parent.GetType(), outputPortModel.Name, out FieldInfo fieldInfo))
                    {
                        fieldInfo.SetValue(parent, child);
                    }
                }
            }
        }

        internal Node GetOrCreateNode(NodeModel nodeModel)
        {
            if (nodeModel is PlaceholderNodeModel placeholderNodeModel)
            {
                Debug.LogWarning($"Placeholder Node ({placeholderNodeModel.Name}) will be skipped in the runtime asset conversion. Please check your graph ({Graph.name}) and replace placeholder nodes.");
                return null;
            }
            if (m_NodeModelToNode.TryGetValue(nodeModel, out Node existingNode))
            {
                return existingNode;
            }

            INodeTransformer nodeTransformer = GetNodeTransformer(nodeModel.GetType());
            if (nodeTransformer == null)
            {
                throw new Exception($"No node transformer found for \"{nodeModel.Asset}\"({nodeModel.GetType()}) in graph {Graph.name}.");
            }

            Node node = nodeTransformer.CreateNodeFromModel(this, nodeModel);
            if (node == null)
            {
                return null;
            }
            node.ID = nodeModel.ID;
            node.Graph = m_GraphModule;
            m_NodeModelToNode.Add(nodeModel, node);
            nodeTransformer.ProcessNode(this, nodeModel, node);
            if (node is IParent nodeAsParent)
            {
                CreateAndAddConnections(nodeAsParent, nodeModel);
            }
            return node;
        }

        public BlackboardVariable GetVariableFromFieldModel(BehaviorGraphNodeModel.FieldModel fieldModel)
        {
            if (fieldModel.LinkedVariable != null && m_VariableModelToVariable.TryGetValue(fieldModel.LinkedVariable.ID, out BlackboardVariable value))
            {
                BlackboardVariable valueVariable = value;
                IBlackboardVariableConverter converter = GetBlackboardVariableConverter(fieldModel.LinkedVariable.Type, fieldModel.LocalValue.Type);
                if (converter != null)
                {
                    valueVariable = converter.Convert(fieldModel.LinkedVariable.Type, fieldModel.LocalValue.Type, valueVariable);
                }

                return valueVariable;
            }
            return fieldModel.LocalValue;
        }

        public static IBlackboardVariableConverter GetBlackboardVariableConverter(Type from, Type to)
        {
            if (from == null || to == null)
            {
                return null;
            }

            if (s_BlackboardVariableConverters == null)
            {
                CreateVariableConverters();
            }

            foreach (var converter in s_BlackboardVariableConverters)
            {
                if (converter.CanConvert(from, to))
                {
                    return converter;
                }
            }
            return null;
        }

        private static List<NodeModel> GetSortedConnections(PortModel portModel)
        {
            List<NodeModel> connectedNodeModels = new List<NodeModel>();
            for (int i = 0; i < portModel.Connections.Count; i++)
            {
                connectedNodeModels.Add(portModel.Connections[i].NodeModel);
            }

            connectedNodeModels.Sort((node1, node2) =>
            {
                float x1 = node1.Position.x;
                float x2 = node2.Position.x;
                return Comparer<float>.Default.Compare(x1, x2);
            });

            return connectedNodeModels;
        }

        private bool IsImplicitSequence(NodeModel nodeModel)
        {
            if (!nodeModel.HasOutgoingConnections || nodeModel is not BehaviorGraphNodeModel behaviorNodeModel)
            {
                return false;
            }

            if (nodeModel is PlaceholderNodeModel placeholderNodeModel)
            {
                Debug.LogWarning($"Placeholder Node ({placeholderNodeModel.Name}) will be turned into a Sequence node for the runtime asset. Please check your graph ({Graph.name}) and replace placeholder nodes.");
            }

            NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(behaviorNodeModel.NodeTypeID);
            // If the node is an action or a placeholder node and it has outgoing connections then we treat it as an implicit sequence.
            return nodeInfo == null || typeof(Action).IsAssignableFrom(nodeInfo.SerializableType);
        }

        private static bool TryFindingField(Type nodeType, string name, out FieldInfo fieldInfo)
        {
            fieldInfo = nodeType.GetFields(k_bindingFlags).FirstOrDefault(fi => fi.Name == name);
            return fieldInfo != default;
        }
    }
}