using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine;

using UnityEditor;
using UnityEditor.Callbacks;

namespace Unity.Behavior
{
    /// <summary>
    /// The primary asset type used by Unity Behavior. The asset contains the authoring representation of the behavior
    /// graph.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Behavior Graph", menuName = "Behavior/Behavior Graph")]
    internal class BehaviorAuthoringGraph : GraphAsset, ISerializationCallbackReceiver
        , ISerializationValidator
    {
        /* Serialization version control for asset compatibility.
         *
         * Version history:
         * 0: Initial/legacy format
         * 1: Schema updates:
         *    - Added IsPlaceholder and RuntimeTypeString properties to NodeModelInfo
         *    - Converted SubgraphGraphInfo to use direct asset reference instead of GUID
         *    - Removed redundant RootGraph property
         */
        private const int kLatestSerializationVersion = 1;
        // Consumed on asset reimport to clean runtime graph from unavailable node type.
        // Do not handle missing type wrapped by BlackboardVariable inside of a node.
        private static HashSet<string> s_GraphPathToValidate = new();
        private static bool s_IsValidatingPlaceholderGraphAsset = false;

        [SerializeField] private int m_SerializedVersion = 0;

        [SerializeField]
        private BehaviorGraphDebugInfo m_DebugInfo;

        public BehaviorGraphDebugInfo DebugInfo
        {
            get
            {
                if (m_DebugInfo == null)
                {
                    m_DebugInfo = GetOrCreateGraphDebugInfo(this);
                }
                return m_DebugInfo;
            }
        }

        [SerializeField] private BehaviorGraph m_RuntimeGraph;
        public bool HasRuntimeGraph => m_RuntimeGraph != null && m_RuntimeGraph.RootGraph != null;
        internal BehaviorGraph RuntimeGraph => m_RuntimeGraph;
        internal IReadOnlyList<BehaviorBlackboardAuthoringAsset> BlackboardReferences => m_Blackboards;
        internal IReadOnlyList<SubgraphGraphInfo> SubgraphsInfo => m_SubgraphsInfo;

        [SerializeField]
        public SerializableGUID AssetID = SerializableGUID.Generate();

        [SerializeField]
        public StoryInfo Story = new StoryInfo();

        [NonSerialized]
        private List<NodeModel> m_RootNodes = new List<NodeModel>();

        public List<NodeModel> Roots
        {
            get
            {
                // Early out if no need to regenerate.
                if (m_RootNodes.Count > 0 && m_LastRootGraphGenerationTimestamp == m_VersionTimestamp)
                {
                    return m_RootNodes;
                }

                m_RootNodes.Clear();
                for (var i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i] is BehaviorGraphNodeModel { IsRoot: true })
                    {
                        m_RootNodes.Add(Nodes[i]);
                    }
                }

                // Sort StartOnEvent by their position (leftmost is first).
                m_RootNodes.Sort((node1, node2) =>
                {
                    float x1 = node1.Position.x;
                    float x2 = node2.Position.x;
                    return Comparer<float>.Default.Compare(x1, x2);
                });

                m_LastRootGraphGenerationTimestamp = m_VersionTimestamp;
                return m_RootNodes;
            }
        }

        [SerializeField]
        private List<NodeModelInfo> m_NodeModelsInfo = new();

        public IReadOnlyList<NodeModelInfo> NodeModelsInfo => m_NodeModelsInfo;

        private Dictionary<SerializableGUID, NodeModelInfo> m_RuntimeNodeTypeIDToNodeModelInfo;
        public IReadOnlyDictionary<SerializableGUID, NodeModelInfo> RuntimeNodeTypeIDToNodeModelInfo => m_RuntimeNodeTypeIDToNodeModelInfo;

        [SerializeField]
        internal List<BehaviorBlackboardAuthoringAsset> m_Blackboards = new List<BehaviorBlackboardAuthoringAsset>();

        [SerializeField] private BehaviorBlackboardAuthoringAsset m_MainBlackboardAuthoringAsset;

        [Serializable]
        public class NodeModelInfo : IEquatable<NodeModelInfo>
        {
            public string Name;
            public string Story;
            public string RuntimeTypeString;
            public SerializableGUID RuntimeTypeID;
            public List<VariableInfo> Variables;
            public List<string> NamedChildren;
            public bool IsPlaceholder;

            public NodeModelInfo() { }

            // Shallow copy constructor.
            public NodeModelInfo(NodeModelInfo other)
            {
                Name = other.Name;
                Story = other.Story;
                RuntimeTypeID = other.RuntimeTypeID;
                Variables = other.Variables;
                NamedChildren = other.NamedChildren;
                IsPlaceholder = other.IsPlaceholder;
            }

            public bool Equals(NodeModelInfo other)
            {
                return string.Equals(Name, other.Name) &&
                       string.Equals(Story, other.Story) &&
                       RuntimeTypeID == other.RuntimeTypeID &&
                       RuntimeTypeString == other.RuntimeTypeString &&
                       IsPlaceholder == other.IsPlaceholder &&
                       ListsEqual(Variables, other.Variables) &&
                       ListsEqual(NamedChildren, other.NamedChildren);
            }

            private static bool ListsEqual<T>(List<T> list1, List<T> list2)
            {
                if (ReferenceEquals(list1, list2)) return true;
                if (list1 == null || list2 == null) return false;
                if (list1.Count != list2.Count) return false;

                foreach (var item in list2)
                {
                    if (!list1.Contains(item))
                        return false;
                }
                return true;
            }
        }

        [SerializeField]
        private SerializableCommandBuffer m_CommandBuffer = new SerializableCommandBuffer();

        public SerializableCommandBuffer CommandBuffer => m_CommandBuffer;

        [System.Serializable]
        public class SubgraphGraphInfo
        {
            public BehaviorAuthoringGraph Asset;
            public long Timestamp;
        }

        [SerializeField]
        private List<SubgraphGraphInfo> m_SubgraphsInfo = new();

        [SerializeField]
        private bool m_BlackboardMissingManagedRef = false;
        [SerializeField]
        private bool m_GraphMissingManagedRef = false;
        [SerializeField]
        private bool m_HasMissingTypeInManagedRef = false;
        private long m_LastSerializedTimestamp;
        private long m_LastRootGraphGenerationTimestamp = 0;

        protected override void OnEnable()
        {
            // When object is first loaded in memory, set the last version timestamp (transient).
            m_LastSerializedTimestamp = VersionTimestamp;

            if (!EditorApplication.isCompiling)
            {
                if (ContainsInvalidSerializedReferences())
                {
                    SetAssetDirty(false);
                    m_HasMissingTypeInManagedRef = true;
                    return;
                }
                else if (EditorUtility.IsPersistent(this) && m_HasMissingTypeInManagedRef)
                {
                    s_GraphPathToValidate.Add(AssetDatabase.GetAssetPath(this));
                    m_HasMissingTypeInManagedRef = false;
                }
            }

            ValidateAssetNames();

            // Force update to catch up serialization version.
            if (m_SerializedVersion != kLatestSerializationVersion)
            {
                EditorApplication.delayCall += DelayedForceReimport;
                // Note that each function added to EditorApplication.delayCall is only executed once after it is added.
            }
        }

        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !EditorUtility.IsPersistent(this))
            {
                return;
            }

            ValidateSerializeReferenceTypeAndPlaceholder();
        }

        private void DelayedForceReimport()
        {
            if (!EditorUtility.IsPersistent(this) || m_SerializedVersion == kLatestSerializationVersion)
            {
                return;
            }

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this), ImportAssetOptions.ForceUpdate);
        }

        internal override void ValidateAsset()
        {
            // We can't validate if asset isn't written on the disk or have missing type from serialized data.
            if (!EditorUtility.IsPersistent(this))
            {
                return;
            }

            if (m_SerializedVersion != kLatestSerializationVersion)
            {
                m_SerializedVersion = kLatestSerializationVersion;
                HasOutstandingChanges = true;
            }

            ValidateSerializeReferenceTypeAndPlaceholder();

            if (ContainsInvalidSerializedReferences())
            {
                return;
            }

            EnsureAssetHasBlackboard();
            EnsureAuthoringDataIsUpToDate();
            BehaviorGraphAssetRegistry.Add(this);
            EnsureCorrectModelTypes();
            EnsureAtLeastOneRoot();
            EnsureStoryVariablesExist();
            EnsureAssetReferenceOnConditions();
            EnsureBlackboardsAreUpToDate();
            EnsureSubgraphsDependencyAreUpToDate();

            base.ValidateAsset();
        }

        public void AddOrUpdateDependency(BehaviorAuthoringGraph graph)
        {
            var subgraphInfo = m_SubgraphsInfo.Find((info) => info.Asset == graph);
            if (subgraphInfo == null)
            {
                m_SubgraphsInfo.Add(new() { Asset = graph, Timestamp = graph.m_VersionTimestamp });
            }
            else if (subgraphInfo.Timestamp != graph.m_VersionTimestamp)
            {
                subgraphInfo.Timestamp = graph.m_VersionTimestamp;
            }
            else
            {
                return;
            }

            SetAssetDirty(false);
        }

        public void RemoveDependency(BehaviorAuthoringGraph graph)
        {
            int index = m_SubgraphsInfo.FindIndex((info) => info.Asset == graph);
            if (index != -1)
            {
                m_SubgraphsInfo.RemoveAt(index);
                SetAssetDirty(true);
            }
        }

        public bool IsDependencyUpToDate(BehaviorAuthoringGraph graph)
        {
            var subgraphInfo = m_SubgraphsInfo.Find((info) => info.Asset == graph);
            return subgraphInfo != null && subgraphInfo.Timestamp == graph.m_VersionTimestamp;
        }

        public bool HasSubgraphDependency(BehaviorAuthoringGraph graph)
        {
            return m_SubgraphsInfo.Any((info) => info.Asset == graph);
        }

        private void EnsureSubgraphsDependencyAreUpToDate()
        {
            bool hasChanged = false;
            for (int i = m_SubgraphsInfo.Count - 1; i >= 0; i--)
            {
                SubgraphGraphInfo subgraphInfo = m_SubgraphsInfo[i];
                if (!this.ContainsStaticSubgraphReferenceTo(subgraphInfo.Asset))
                {
#if BEHAVIOR_DEBUG_ASSET_IMPORT
                    Debug.Log("Removing an invalid subgraph dependency", this);
#endif
                    // If the asset was deleted or this graph no longer contains reference the subgraph, then it is no longer dependent.
                    m_SubgraphsInfo.RemoveAt(i);
                    hasChanged = true;
                    continue;
                }

                // If the asset dependency is no longer up to date, we also need to rebuild the graph.
                hasChanged |= !IsDependencyUpToDate(subgraphInfo.Asset);
            }

            if (hasChanged)
            {
                // Lost of static subgraph dependency means the graph module needs to be rebuilt.
                SetAssetDirty(setHasOutStandingChange: true);
            }
        }

        private void RefreshSubgraphDependencies()
        {
            foreach (var subgraphInfo in m_SubgraphsInfo)
            {
                AddOrUpdateDependency(subgraphInfo.Asset);
            }
        }

        private void ValidateAssetNames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorUtility.IsPersistent(this))
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string assetPathName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (name != assetPathName)
            {
                name = assetPathName;
            }

            if (m_DebugInfo != null && m_DebugInfo.name != $"{name} Debug Info")
            {
                m_DebugInfo.name = $"{name} Debug Info";
            }
            if (m_RuntimeGraph != null && m_RuntimeGraph.name != name)
            {
                m_RuntimeGraph.name = name;
            }

            if (m_MainBlackboardAuthoringAsset != null && m_MainBlackboardAuthoringAsset.name != $"{name} Blackboard")
            {
                m_MainBlackboardAuthoringAsset.name = $"{name} Blackboard";
                if (m_MainBlackboardAuthoringAsset.RuntimeBlackboardAsset != null)
                {
                    m_MainBlackboardAuthoringAsset.RuntimeBlackboardAsset.name = $"{name} Blackboard";
                }
            }

            if (!AssetDatabase.IsMainAsset(this))
            {
                // Ensures that the authoring asset is always the main asset.
                AssetDatabase.SetMainObject(this, assetPath);
            }
        }

        private void EnsureBlackboardsAreUpToDate()
        {
            for (int i = m_Blackboards.Count - 1; i >= 0; i--)
            {
                BehaviorBlackboardAuthoringAsset blackboard = m_Blackboards[i];
                if (blackboard == null)
                {
                    m_Blackboards.RemoveAt(i);
                }
                else
                {
                    ValidateBlackboardAssetName(blackboard);
                    blackboard.ValidateAsset();
                }
            }
        }

        private void ValidateBlackboardAssetName(BehaviorBlackboardAuthoringAsset blackboard)
        {
            if (AssetDatabase.GetAssetPath(blackboard) != AssetDatabase.GetAssetPath(this))
            {
                return;
            }

            if (blackboard.name != $"{name} + Blackboard")
            {
                blackboard.name = $"{name} + Blackboard";
            }
        }

        private void EnsureAtLeastOneRoot()
        {
            // Add a start node if no root exists.
            if (Roots.Count == 0)
            {
                var newStart = new StartNodeModel(NodeRegistry.GetInfo(typeof(Start)));
                newStart.OnDefineNode();
                Nodes.Add(newStart);
            }
        }

        internal void EnsureCorrectModelTypes()
        {
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                NodeModel nodeModel = Nodes[i];
                if (nodeModel is not BehaviorGraphNodeModel node)
                {
                    continue;
                }

                NodeInfo info = NodeRegistry.GetInfoFromTypeID(node.NodeTypeID);
                if (info == null)
                {
                    // The node info is missing and it will be replaced with a placeholder node UI when opening the graph.
                    // We're keeping the serialization the same to avoid issues in case the node is recovered.
                }
                else if (node.NodeType.text != info.Type.AssemblyQualifiedName)
                {
                    node.NodeType = info.Type;
                }
            }
        }

        private void EnsureStoryVariablesExist()
        {
            if (Blackboard == null)
            {
                return;
            }

            Story ??= new StoryInfo();

            List<VariableInfo> storyVariables = Story.Variables;
            List<VariableModel> assetVariables = Blackboard.Variables;
            foreach (VariableInfo storyVariable in storyVariables)
            {
                if (!assetVariables.Any(assetVar =>
                        string.Equals(assetVar.Name, storyVariable.Name, StringComparison.CurrentCultureIgnoreCase)
                        && assetVar.Type == (Type)storyVariable.Type))
                {
                    string varName = char.ToUpper(storyVariable.Name.First()) + storyVariable.Name.Substring(1);
                    Type varType = BlackboardUtils.GetVariableModelTypeForType(storyVariable.Type);
                    VariableModel variable = Activator.CreateInstance(varType) as VariableModel;
                    variable.Name = varName;
                    Blackboard.Variables.Add(variable);
                }
            }
        }

        private void EnsureAssetReferenceOnConditions()
        {
            foreach (NodeModel node in Nodes)
            {
                if (node is not IConditionalNodeModel conditionalNode)
                {
                    continue;
                }
                foreach (ConditionModel condition in conditionalNode.ConditionModels)
                {
                    condition.Asset = this;
                }
            }
        }

        [OnOpenAsset(1)]
        public static bool OpenAsset(int instanceID, int line)
        {
            BehaviorAuthoringGraph asset = EditorUtility.InstanceIDToObject(instanceID) as BehaviorAuthoringGraph;
            if (asset == null)
            {
                BehaviorGraph runtimeGraph = EditorUtility.InstanceIDToObject(instanceID) as BehaviorGraph;
                if (runtimeGraph == null)
                {
                    return false;
                }
                if (!AssetDatabase.IsMainAsset(runtimeGraph) && AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(runtimeGraph)) is BehaviorAuthoringGraph parentAsset)
                {
                    asset = parentAsset;
                }
                else
                {
                    return false;
                }
            }
            BehaviorWindowDelegate.Open(asset);
            return true; // we did not handle the open
        }


        public static BehaviorGraph GetOrCreateGraph(BehaviorAuthoringGraph assetObject)
        {
            string assetPath = AssetDatabase.GetAssetPath(assetObject);
            if (!EditorUtility.IsPersistent(assetObject) || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (!assetObject.HasRuntimeGraph)
            {
                assetObject.EnsureAuthoringDataIsUpToDate();
            }

            return assetObject.m_RuntimeGraph;
        }

        public static BehaviorGraphDebugInfo GetOrCreateGraphDebugInfo(BehaviorAuthoringGraph assetObject)
        {
            string assetPath = AssetDatabase.GetAssetPath(assetObject);
            if (!EditorUtility.IsPersistent(assetObject) || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            BehaviorGraph graph = GetOrCreateGraph(assetObject);
            string graphPath = AssetDatabase.GetAssetPath(graph);
            BehaviorGraphDebugInfo debugInfo = AssetDatabase.LoadAssetAtPath<BehaviorGraphDebugInfo>(graphPath);
            if (debugInfo == null)
            {
                debugInfo = CreateInstance<BehaviorGraphDebugInfo>();
                debugInfo.name = assetObject.name + " Debug Info";
                AssetDatabase.AddObjectToAsset(debugInfo, graph);
                assetObject.SetAssetDirty(false);
            }
            else if (assetObject.DebugInfo != debugInfo)
            {
                assetObject.m_DebugInfo = debugInfo;
                assetObject.SetAssetDirty(false);
            }

            return debugInfo;
        }

        /// <inheritdoc cref="OnBeforeSerialize"/>
        public void OnBeforeSerialize()
        {
            if (VersionTimestamp == m_LastSerializedTimestamp)
            {
                return;
            }

            CreateNodeModelsInfoCache();
            m_LastSerializedTimestamp = VersionTimestamp;
        }

        /// <inheritdoc cref="OnAfterDeserialize"/>
        public void OnAfterDeserialize()
        {
            CreateNodeModelInfosDictionaryFromList();
        }

        private void CreateNodeModelsInfoCache()
        {
            AssetLogger.Reset();
            var oldNodeModelsInfos = new List<NodeModelInfo>(m_NodeModelsInfo);
            m_NodeModelsInfo.Clear();
            m_RuntimeNodeTypeIDToNodeModelInfo ??= new Dictionary<SerializableGUID, NodeModelInfo>();
            m_RuntimeNodeTypeIDToNodeModelInfo.Clear();

            foreach (NodeModel node in Nodes)
            {
                if (node is not BehaviorGraphNodeModel behaviorNode)
                {
                    continue;
                }

                NodeInfo nodeInfo = NodeRegistry.GetInfoFromTypeID(behaviorNode.NodeTypeID);
                if (nodeInfo == null || m_RuntimeNodeTypeIDToNodeModelInfo.ContainsKey(behaviorNode.NodeTypeID))
                {
                    // Add previously saved node model infos for placeholder if they're missing in the current collection.
                    CreatePlaceholderNodeIfNeeded(behaviorNode);
                    continue;
                }

                if (CheckManagedReferenceForPlaceholderNode(behaviorNode, nodeInfo))
                {
                    continue;
                }

                CreateNode(nodeInfo);
            }

            // If changed found, override serialized data.
            if (m_NodeModelsInfo.Count != oldNodeModelsInfos.Count)
            {
                DirtyAndLogResult();
            }
            else
            {
                foreach (NodeModelInfo nodeModelInfo in m_NodeModelsInfo)
                {
                    // If any entry is different, then update the last serialized timestamp.
                    if (!oldNodeModelsInfos.Contains(nodeModelInfo))
                    {
                        DirtyAndLogResult();
                        break;
                    }
                }
            }

            void DirtyAndLogResult()
            {
                SetAssetDirty(false);
                AssetLogger.LogResults(this, "automatically updated nodes with missing type references");
            }

            void CreateNode(NodeInfo nodeInfo)
            {
                var nodeModelInfo = new NodeModelInfo()
                {
                    IsPlaceholder = false,
                    Name = nodeInfo.Name,
                    Story = nodeInfo.Story,
                    RuntimeTypeString = nodeInfo.SerializableType.ToString(),
                    RuntimeTypeID = nodeInfo.TypeID,
                    Variables = nodeInfo.Variables.Select(variable => new VariableInfo
                    {
                        Name = variable.Name,
                        Type = (typeof(BlackboardVariable).IsAssignableFrom(variable.Type.Type) && variable.Type.Type.IsGenericType)
                            ? variable.Type.Type.GenericTypeArguments[0] : variable.Type
                    }).ToList(),
                    NamedChildren = nodeInfo.NamedChildren
                };
                m_RuntimeNodeTypeIDToNodeModelInfo[nodeInfo.TypeID] = nodeModelInfo;
                m_NodeModelsInfo.Add(nodeModelInfo);

                var oldNodeModelInfo = oldNodeModelsInfos.Find(node => node.RuntimeTypeID == nodeModelInfo.RuntimeTypeID);
                if (oldNodeModelInfo != null && oldNodeModelInfo.IsPlaceholder)
                {
                    AssetLogger.RecordNodeResolution(nodeModelInfo.Name, nodeModelInfo.RuntimeTypeString);
                }
            }

            void CreatePlaceholderNodeIfNeeded(BehaviorGraphNodeModel behaviorNode)
            {
                foreach (NodeModelInfo oldNodeModelInfo in oldNodeModelsInfos)
                {
                    var nodeTypeID = oldNodeModelInfo.RuntimeTypeID;
                    if (nodeTypeID == behaviorNode.NodeTypeID && !m_RuntimeNodeTypeIDToNodeModelInfo.ContainsKey(nodeTypeID))
                    {
                        var newNodeModelInfo = new NodeModelInfo(oldNodeModelInfo);
                        newNodeModelInfo.IsPlaceholder = true;
                        newNodeModelInfo.RuntimeTypeString = oldNodeModelInfo.RuntimeTypeString;
                        m_RuntimeNodeTypeIDToNodeModelInfo.Add(nodeTypeID, newNodeModelInfo);
                        m_NodeModelsInfo.Add(newNodeModelInfo);

                        // Only log if new placeholder with previously existing type - skip legacy node model.
                        if (oldNodeModelInfo.IsPlaceholder == false && !string.IsNullOrEmpty(oldNodeModelInfo.RuntimeTypeString))
                        {
                            AssetLogger.RecordNodeAsPlaceholder(newNodeModelInfo.Name, oldNodeModelInfo.RuntimeTypeString);
                        }
                        break;
                    }
                }
            }

            bool CheckManagedReferenceForPlaceholderNode(BehaviorGraphNodeModel behaviorNode, NodeInfo newModelInfo)
            {
                foreach (NodeModelInfo oldModelInfo in oldNodeModelsInfos)
                {
                    if (oldModelInfo.RuntimeTypeID != newModelInfo.TypeID)
                    {
                        continue;
                    }

                    // If the NodeDescription id is the same, but the type was migrated,
                    // create placeholder node to force runtime graph rebuild.
                    var newTypeString = newModelInfo.SerializableType.ToString();
                    var oldTypeString = oldModelInfo.RuntimeTypeString;
                    if (!string.IsNullOrEmpty(oldTypeString) && oldTypeString != newTypeString)
                    {
                        var newNodeModelInfo = new NodeModelInfo(oldModelInfo);
                        newNodeModelInfo.IsPlaceholder = true;
                        newNodeModelInfo.RuntimeTypeString = newTypeString;
                        m_RuntimeNodeTypeIDToNodeModelInfo.Add(newModelInfo.TypeID, newNodeModelInfo);
                        m_NodeModelsInfo.Add(newNodeModelInfo);

                        AssetLogger.RecordNodeMigration(newNodeModelInfo.Name, oldTypeString, newTypeString);
                        return true;
                    }
                }

                return false;
            }
        }

        private void CheckNodeModelInfoForPlaceholder()
        {
            AssetLogger.Reset();
            bool foundPlaceholder = false;

            foreach (var nodeModelInfo in m_NodeModelsInfo)
            {
                NodeInfo latestNodeInfo = NodeRegistry.GetInfoFromTypeID(nodeModelInfo.RuntimeTypeID);

                if (latestNodeInfo == null)
                {
                    foundPlaceholder = true;
                    nodeModelInfo.IsPlaceholder = true;
                    AssetLogger.RecordNodeAsPlaceholder(nodeModelInfo.Name, nodeModelInfo.RuntimeTypeString);
                }
            }

            if (foundPlaceholder)
            {
                SetAssetDirty(false);
                AssetLogger.LogResults(this, "automatically updated node(s) with missing type references");
            }
        }

        private void CreateNodeModelInfosDictionaryFromList()
        {
            if (m_RuntimeNodeTypeIDToNodeModelInfo == null)
            {
                m_RuntimeNodeTypeIDToNodeModelInfo = new Dictionary<SerializableGUID, NodeModelInfo>();
            }
            if (m_NodeModelsInfo == null)
            {
                return;
            }
            foreach (NodeModelInfo nodeModelInfo in m_NodeModelsInfo)
            {
                m_RuntimeNodeTypeIDToNodeModelInfo[nodeModelInfo.RuntimeTypeID] = nodeModelInfo;
            }
        }

        internal override void EnsureAssetHasBlackboard()
        {
            string blackboardName = name + " Blackboard";
            string path = AssetDatabase.GetAssetPath(this);
            BlackboardAsset existingBlackboard = AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(asset => asset is BlackboardAsset) as BlackboardAsset;
            BehaviorBlackboardAuthoringAsset blackboardAuthoring = existingBlackboard as BehaviorBlackboardAuthoringAsset;
            if (blackboardAuthoring == null)
            {
                blackboardAuthoring = CreateInstance<BehaviorBlackboardAuthoringAsset>();

                Blackboard = blackboardAuthoring;
                Blackboard.name = blackboardName;
                Blackboard.hideFlags = HideFlags.HideInHierarchy;
                m_MainBlackboardAuthoringAsset = blackboardAuthoring;

                if (existingBlackboard != null)
                {
                    Blackboard.AssetID = existingBlackboard.AssetID;
                    Blackboard.Variables = existingBlackboard.Variables;
                    AssetDatabase.RemoveObjectFromAsset(existingBlackboard);
                }
            }
            else if (blackboardAuthoring != null)
            {
                m_MainBlackboardAuthoringAsset = blackboardAuthoring;
                if (Blackboard != null && blackboardAuthoring == Blackboard)
                {
                    // Update the graph Blackboard name if needed.
                    if (blackboardAuthoring.name != blackboardName)
                    {
                        blackboardAuthoring.name = blackboardName;
                        if (blackboardAuthoring.RuntimeBlackboardAsset != null)
                        {
                            blackboardAuthoring.RuntimeBlackboardAsset.name = blackboardName;
                        }
                    }
                    Blackboard.hideFlags = HideFlags.HideInHierarchy;
                }
                return;
            }

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // If we reached this far, that means we are generating a new blackboard.
            AssetDatabase.AddObjectToAsset(Blackboard, this);
            blackboardAuthoring.BuildRuntimeBlackboard();
            AssetDatabase.SaveAssetIfDirty(this);
        }

        public override string ToString() => name;

        public void OnDestroy()
        {
            BehaviorGraphAssetRegistry.Remove(this);
        }

        /// <summary>
        /// Build the sub assets (Runtime Graph, Runtime Blackboard Asset, Debug Info).
        /// </summary>
        /// <param name="forceRebuild">Set to false allows to skip the full rebuild and just update VersionTimestamp in case there is no outstanding change.
        /// Default to true to preserve original behavior.</param>
        /// <returns>The up-to-date BehaviorGraph.</returns>
        public BehaviorGraph BuildRuntimeGraph(bool forceRebuild = true)
        {
            if (ContainsInvalidSerializedReferences())
            {
                Debug.LogWarning($"Graph asset {name} has missing types in managed references. Cannot build runtime graph.", this);
                return null;
            }

            if (HasPlaceholderNode())
            {
                // Fallback in case the graph had missing types in both BlackboardVariable that are now resolved
                // but still has pending placeholder node type. As the missing node type couldn't yet be stripped
                // out of the runtime graph because the graph could not be rebuilt,
                // we refresh the node models cache a last time before the rebuild.
                CreateNodeModelsInfoCache();
            }
            var runtimeGraph = GetOrCreateGraph(this);
            if (runtimeGraph == null)
            {
                return null;
            }

            if (runtimeGraph.RootGraph == null || HasOutstandingChanges || forceRebuild)
            {
#if BEHAVIOR_DEBUG_ASSET_IMPORT
                Debug.Log($"GraphAsset[<b>{name}</b>].BuildRuntimeGraph", this);
#endif
                var graphAssetProcessor = GraphAssetProcessor.CreateInstanceForRebuild(this, runtimeGraph);
                graphAssetProcessor.ProcessGraph();
                SetDirtyAndSyncRuntimeGraphTimestamp(outstandingChange: true);
            }
            else if (EditorUtility.IsDirty(this))
            {
#if BEHAVIOR_DEBUG_ASSET_IMPORT
                Debug.Log($"GraphAsset[<b>{name}</b>].SetAssetDirty - Asset don't have outstanding change but serialized data are dirty.", this);
#endif
                // If no outstanding change but serialized data are dirty, only update the timestamp.
                // This happens when only serialized field of cosmetic data are changed (like node position).
                SetDirtyAndSyncRuntimeGraphTimestamp(outstandingChange: false);
            }

            m_BlackboardMissingManagedRef = false;
            m_GraphMissingManagedRef = false;
            RefreshSubgraphDependencies();
            return runtimeGraph;
        }

        private void SetDirtyAndSyncRuntimeGraphTimestamp(bool outstandingChange)
        {
            SetAssetDirty(outstandingChange); // Set dirty update the timestamp of the main asset and dirty.
            m_RuntimeGraph.RootGraph.VersionTimestamp = VersionTimestamp;
        }

        private void SyncRuntimeGraphTimestamp()
        {
            m_RuntimeGraph.RootGraph.VersionTimestamp = VersionTimestamp;
            EditorUtility.SetDirty(this); // set dirty to ensure the change is written on the disk at the next import.
        }

        public bool ContainsInvalidSerializedReferences()
        {
            foreach (var subgraphInfo in m_SubgraphsInfo)
            {
                if (subgraphInfo.Asset != null && subgraphInfo.Asset.ContainsInvalidSerializedReferences())
                {
                    return true;
                }
            }

            return SerializationUtility.HasManagedReferencesWithMissingTypes(this)
                || SerializationUtility.HasManagedReferencesWithMissingTypes(m_RuntimeGraph)
                || SerializationUtility.HasManagedReferencesWithMissingTypes(Blackboard);
        }

        /// <summary>
        /// Expose graph framework API to BehaviorAssetPostProcessor.
        /// </summary>
        internal bool NeedRebuild => HasOutstandingChanges;

        /// <summary>
        /// Custom version of SaveAsset made for BehaviorAssetPostProcessor.
        /// This method manually rebuilds without saving the asset. Also rebuild the embedded asset if needed.
        /// This is required in order to prevent the asset being rebuilt everytime until GraphAsset.SaveAsset is called.
        /// After the rebuild, reset HasOutstandingChanges, readying the asset to be manually saved using AssetDatabase.SaveAsset.
        /// </summary>
        /// <remark>
        /// HasOutstandingChanges is internal to the Behavior.GraphFramework assembly.
        /// </remark>
        internal void RebuildGraphAndBlackboardRuntimeData()
        {
            if (ContainsInvalidSerializedReferences())
            {
                return;
            }

            if (Blackboard == null)
            {
                EnsureAssetHasBlackboard();
            }

            if (Blackboard is BehaviorBlackboardAuthoringAsset authoringBB)
            {
                // Rebuild embedded blackboard if needed
                if (authoringBB.IsAssetVersionUpToDate())
                {
                    authoringBB.BuildRuntimeBlackboard();
                }

                // Force rebuild as this method is only called if it needs it.
                BuildRuntimeGraph(forceRebuild: true);
                authoringBB.HasOutstandingChanges = false;
                HasOutstandingChanges = false;
            }
        }

        /// <summary>
        /// This method is used to validate the serialized runtime graph that has been generated during authoring time.
        /// This is needed to check to ensure backward compatibility with older graph that didn't serialized a
        /// direct link to the runtime asset.
        /// </summary>
        /// <param name="assetObject"></param>
        /// <returns></returns>
        public void EnsureAuthoringDataIsUpToDate()
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            // AssetID is asset GUID.
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AssetID = new SerializableGUID(guid);

            bool needSaving = true;
            // LoadAssetAtPath<T> doesn't work in some situation (Duplicated asset).
            BehaviorGraph graph = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .FirstOrDefault(asset => asset is BehaviorGraph) as BehaviorGraph;
            if (graph != null)
            {
                // The graph was just created and still need rebuilding. Skip for now.
                if (this.m_RuntimeGraph != null && this.m_RuntimeGraph.RootGraph == null)
                {
                    return;
                }

                // Usually happens when the asset is duplicated.
                if (graph != this.m_RuntimeGraph || m_RuntimeGraph.RootGraph.AuthoringAssetID != AssetID)
                {
                    m_RuntimeGraph = graph;
                    m_RuntimeGraph.RootGraph.AuthoringAssetID = AssetID;
                    SetDirtyAndSyncRuntimeGraphTimestamp(outstandingChange: false);
                }
                else if (m_RuntimeGraph.RootGraph.VersionTimestamp != VersionTimestamp)
                {
                    SyncRuntimeGraphTimestamp();
                }
                else
                {
                    // If no change, no need to save.
                    needSaving = false;
                }
            }
            else if (m_RuntimeGraph != null)
            {
                // In this case, the asset lost reference to it's runtime graph (deleted).
                m_RuntimeGraph = null;
                SetAssetDirty(true);
            }
            else
            {
                // Asset is still being created - create now.
                graph = CreateInstance<BehaviorGraph>();
                graph.name = name;
                m_RuntimeGraph = graph;
                AssetDatabase.AddObjectToAsset(graph, this);
                SetAssetDirty(true);
            }

            BehaviorGraphDebugInfo debugInfo = AssetDatabase.LoadAssetAtPath<BehaviorGraphDebugInfo>(assetPath);
            if (debugInfo == null)
            {
                debugInfo = CreateInstance<BehaviorGraphDebugInfo>();
                debugInfo.name = name + " Debug Info";
                AssetDatabase.AddObjectToAsset(debugInfo, this);
                EditorUtility.SetDirty(this);
                needSaving = true; // Ensure new asset is saved.
            }
            else if (m_DebugInfo != debugInfo)
            {
                // In case of duplication, ensure the asset is poiting toward the right debug
                m_DebugInfo = debugInfo;
            }

            if (needSaving)
            {
                // Saving will re-import the asset and validate.
                SaveAsset();
            }
        }

        /// <summary>
        /// Check if there are managed reference with missing type or placeholder data in the runtime graph.
        /// If a state mismatched, queue the asset for further validation.
        /// </summary>
        internal void ValidateSerializeReferenceTypeAndPlaceholder()
        {
            // If not written on disk or have pending changes, skip.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !EditorUtility.IsPersistent(this))
            {
                return;
            }

            var runtimeGraph = GetOrCreateGraph(this);
            if (runtimeGraph == null)
            {
                return;
            }

            bool hadPlaceholder = HasPlaceholderNode();
            long previousTimestamp = m_VersionTimestamp;
            bool isGraphMissingManagedRef = SerializationUtility.HasManagedReferencesWithMissingTypes(m_RuntimeGraph);
            bool isBlackboardMissingManagedRef = SerializationUtility.HasManagedReferencesWithMissingTypes(Blackboard);
            // If no placeholder but missing type in runtime graph - new placeholder
            // If placeholder but no missing type in runtime graph - resolved
            // if placeholder and missing type in runtime - potential new placeholder or type migration
            if (isGraphMissingManagedRef || hadPlaceholder)
            {
                CreateNodeModelsInfoCache();
            }
            else
            {
                CheckNodeModelInfoForPlaceholder();
            }

            AssetLogger.Reset();
            // Graph (Runtime)
            bool stillHasPlaceholder = HasPlaceholderNode();
            if (stillHasPlaceholder)
            {
                if (previousTimestamp != m_VersionTimestamp || runtimeGraph.ContainsPlaceholderNodes())
                {
                    s_GraphPathToValidate.Add(AssetDatabase.GetAssetPath(this));
                }
            }
            else if (runtimeGraph.CompiledWithPlaceholderNode)
            {
                // If authoring graph no longer have placeholder but was compiled with them
                s_GraphPathToValidate.Add(AssetDatabase.GetAssetPath(this));
            }
            else if (m_GraphMissingManagedRef && !isGraphMissingManagedRef)
            {
                RecordResolutionAndSetDirty(runtimeGraph);
            }

            // Blackboard
            if (m_BlackboardMissingManagedRef && !isBlackboardMissingManagedRef)
            {
                RecordResolutionAndSetDirty(Blackboard);
            }

            AssetLogger.LogResults(this, "resolved missing types in managed references");

            // If first time there is no placeholder but missing type in runtime graph
            // OR If first time there is missing type in blackboard
            // OR if authoring graph has missing type
            if ((!stillHasPlaceholder && isGraphMissingManagedRef)
                || isBlackboardMissingManagedRef
                || SerializationUtility.HasManagedReferencesWithMissingTypes(this))
            {
                AssetLogger.LogAssetManagedReferenceError(this);
            }

            m_GraphMissingManagedRef = isGraphMissingManagedRef;
            m_BlackboardMissingManagedRef = isBlackboardMissingManagedRef;

            void RecordResolutionAndSetDirty(ScriptableObject asset)
            {
                AssetLogger.RecordAssetResolution(asset);
                s_GraphPathToValidate.Add(AssetDatabase.GetAssetPath(this));
            }
        }

        // Internal for testing purposes.
        internal bool HasPlaceholderNode()
        {
            foreach (var info in m_NodeModelsInfo)
            {
                if (info.IsPlaceholder)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clear managed reference of placeholder node from runtime graph and set asset dirty with outstanding change.
        /// </summary>
        /// <param name="reimport">Set to false when doing batch operation (AssetPostProcessor)</param>
        internal static void CheckAndValidatePlaceholdersInGraphAssets(bool reimport = true)
        {
            if (s_GraphPathToValidate.Count == 0 || s_IsValidatingPlaceholderGraphAsset)
            {
                return;
            }

            s_IsValidatingPlaceholderGraphAsset = true;
            foreach (var graphPath in s_GraphPathToValidate)
            {
                var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(graphPath);
                var runtimeGraph = GetOrCreateGraph(graph);
                if (runtimeGraph == null)
                {
                    continue;
                }

                // If authoring graph has placeholder, ensure runtime graph don't reference them
                var managedRefs = SerializationUtility.GetManagedReferencesWithMissingTypes(runtimeGraph);
                foreach (var managedRef in managedRefs)
                {
                    bool managedRefCleared = false;
                    foreach (var modelInfo in graph.NodeModelsInfo)
                    {
                        if (!modelInfo.IsPlaceholder)
                        {
                            continue;
                        }

                        foreach (NodeModel node in graph.Nodes)
                        {
                            if (node is not BehaviorGraphNodeModel behaviorNode)
                            {
                                continue;
                            }

                            // Find the exact node model to retrive the serializableType
                            if (behaviorNode.NodeTypeID == modelInfo.RuntimeTypeID
                                && behaviorNode.NodeType.text.Contains(managedRef.className))
                            {
                                SerializationUtility.ClearManagedReferenceWithMissingType(runtimeGraph, managedRef.referenceId);
                                managedRefCleared = true;
                                break;
                            }
                        }

                        if (managedRefCleared)
                        {
                            break;
                        }
                    }
                }

                // Refresh now has we might have cleared out all managed ref with missing type.
                graph.m_GraphMissingManagedRef = SerializationUtility.HasManagedReferencesWithMissingTypes(runtimeGraph);
                // If graph was queue to be validated here, it means they need rebuild anyway.
                graph.SetAssetDirty(true);
            }

            if (reimport)
            {
                EditorApplication.delayCall += DelayedReimport;
            }
            else
            {
                s_GraphPathToValidate.Clear();
                s_IsValidatingPlaceholderGraphAsset = false;
            }
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void AssemblyReloadValidation()
        {
            AssemblyReloadEvents.afterAssemblyReload += () =>
            {
                CheckAndValidatePlaceholdersInGraphAssets();
            };
        }

        private static void DelayedReimport()
        {
            EditorApplication.delayCall -= DelayedReimport;

            foreach (var graphPath in s_GraphPathToValidate)
            {
                AssetDatabase.ImportAsset(graphPath);
            }

            s_GraphPathToValidate.Clear();
            s_IsValidatingPlaceholderGraphAsset = false;
        }
    }
}
