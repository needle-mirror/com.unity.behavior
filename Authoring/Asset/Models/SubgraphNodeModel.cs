using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(RunSubgraph))]
    internal class SubgraphNodeModel : BehaviorGraphNodeModel
    {
        internal const string k_SubgraphFieldName = "Subgraph";
        internal const string k_BlackboardFieldName = "Blackboard";

        [SerializeField] private List<SerializableGUID> m_OveriddenblackboardVariableGuids = new();
        [SerializeField] private SerializableGUID m_SubgraphAssetId; // To remove in future version in favor of hard ref.
        [SerializeField] private BehaviorAuthoringGraph m_SubgraphAuthoringAsset;
        [SerializeField] private bool m_IsDynamic;

        public override bool IsSequenceable => true;

        public bool IsDynamic => m_IsDynamic;

        public BehaviorGraph RuntimeSubgraph => GetLinkedSubgraph();

        public bool ShowStaticSubgraphRepresentation
        {
            get => m_ShowStaticSubgraphRepresentation;
            set => m_ShowStaticSubgraphRepresentation = value;
        }

        [SerializeField]
        private bool m_ShowStaticSubgraphRepresentation;

        private BehaviorGraph GetLinkedSubgraph()
        {
            TypedVariableModel<BehaviorGraph> linkedVariable = SubgraphField.LinkedVariable as TypedVariableModel<BehaviorGraph>;
            if (linkedVariable != null)
            {
                return linkedVariable.m_Value;
            }

            return null;
        }

        public BehaviorAuthoringGraph SubgraphAuthoringAsset => GetAuthoringAssetFromRuntimeGraph();

        public BehaviorBlackboardAuthoringAsset RequiredBlackboard => GetBlackboardAsset();

        internal FieldModel SubgraphField => Fields.FirstOrDefault(field => field.FieldName == k_SubgraphFieldName);
        internal FieldModel BlackboardAssetField => Fields.FirstOrDefault(field => field.FieldName == k_BlackboardFieldName);

        [SerializeReference]
        internal List<FieldModel> m_StoryFields = new();

        public SubgraphNodeModel(NodeInfo nodeInfo) : base(nodeInfo) { }

        // Chain captures of LinkedVariable references before base constructor's
        // SynchronizeNodeFieldsWithOriginal clears them (as inline vars aren't in any blackboard).
        protected SubgraphNodeModel(SubgraphNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset)
            : this(nodeModelOriginal, asset,
                  nodeModelOriginal.SubgraphField?.LinkedVariable,
                  nodeModelOriginal.BlackboardAssetField?.LinkedVariable)
        {
        }

        private SubgraphNodeModel(SubgraphNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset,
            VariableModel originalSubgraphLinkedVariable, VariableModel originalBlackboardLinkedVariable)
            : base(nodeModelOriginal, asset)
        {
            // Restore the original's inline LinkedVariables that SynchronizeNodeFieldsWithOriginal cleared.
            if (originalSubgraphLinkedVariable != null)
            {
                nodeModelOriginal.SubgraphField.LinkedVariable = originalSubgraphLinkedVariable;
            }
            if (originalBlackboardLinkedVariable != null)
            {
                nodeModelOriginal.BlackboardAssetField.LinkedVariable = originalBlackboardLinkedVariable;
            }

            m_SubgraphAuthoringAsset = nodeModelOriginal.m_SubgraphAuthoringAsset;
            m_IsDynamic = nodeModelOriginal.m_IsDynamic;
            m_OveriddenblackboardVariableGuids = nodeModelOriginal.m_OveriddenblackboardVariableGuids != null
                ? new List<SerializableGUID>(nodeModelOriginal.m_OveriddenblackboardVariableGuids)
                : new List<SerializableGUID>();
            ShowStaticSubgraphRepresentation = nodeModelOriginal.ShowStaticSubgraphRepresentation;

            GetOrCreateField(k_SubgraphFieldName, typeof(BehaviorGraph));
            if (originalSubgraphLinkedVariable != null)
            {
                SubgraphField.LinkedVariable = originalSubgraphLinkedVariable;
            }

            GetOrCreateField(k_BlackboardFieldName, typeof(BehaviorBlackboardAuthoringAsset));
            if (originalBlackboardLinkedVariable != null)
            {
                BlackboardAssetField.LinkedVariable = originalBlackboardLinkedVariable;
            }
        }

        public void SetVariableOverride(SerializableGUID variableGuid, bool isOverridden)
        {
            if (!isOverridden)
            {
                m_OveriddenblackboardVariableGuids.Remove(variableGuid);
                return;
            }

            if (!m_OveriddenblackboardVariableGuids.Contains(variableGuid))
            {
                m_OveriddenblackboardVariableGuids.Add(variableGuid);
            }
        }

        public bool IsVariableOverridden(SerializableGUID variableGuid)
        {
            return m_OveriddenblackboardVariableGuids.Contains(variableGuid);
        }

        public void ClearOverriddenVariables()
        {
            m_OveriddenblackboardVariableGuids.Clear();
        }

        public override void OnDefineNode()
        {
            base.OnDefineNode();
            GetOrCreateField(k_SubgraphFieldName, typeof(BehaviorGraph));
            GetOrCreateField(k_BlackboardFieldName, typeof(BehaviorBlackboardAuthoringAsset));
            UpdateIsDynamic();
        }

        protected override bool IsBlackboardLinkedField(FieldModel field)
        {
            // Subgraph and Blackboard fields use inline variable models (direct asset references),
            // not blackboard variable references.
            return field.FieldName != k_SubgraphFieldName && field.FieldName != k_BlackboardFieldName;
        }

        private BehaviorAuthoringGraph GetAuthoringAssetFromRuntimeGraph()
        {
            if (SubgraphField.LinkedVariable == null)
            {
                return null;
            }

#if UNITY_EDITOR
            // Virtual instance are not valid candidate for authoring model.
            if (!UnityEditor.EditorUtility.IsPersistent(RuntimeSubgraph))
            {
                return null;
            }

            BehaviorAuthoringGraph asset = BehaviorGraphAssetRegistry.TryGetAssetFromGraphPath(RuntimeSubgraph);
#else
            BehaviorAuthoringGraph asset = null;
#endif

            return asset;
        }

        private BehaviorBlackboardAuthoringAsset GetBlackboardAsset()
        {
            if (BlackboardAssetField?.LinkedVariable != null)
            {
                return BlackboardAssetField.LinkedVariable.ObjectValue as BehaviorBlackboardAuthoringAsset;
            }

            return null;
        }

        protected override void EnsureFieldValuesAreUpToDate()
        {
            if (SubgraphField == null)
            {
                GetOrCreateField(k_SubgraphFieldName, typeof(BehaviorGraph));
            }

            if (BlackboardAssetField == null)
            {
                GetOrCreateField(k_BlackboardFieldName, typeof(BehaviorBlackboardAuthoringAsset));
            }

            if (SubgraphField?.LinkedVariable == null)
            {
                // No subgraph is assigned, so remove variable fields and set the node back to static.
                ClearFields();
                m_IsDynamic = false;
                return;
            }

            if (!RuntimeSubgraph || !SubgraphAuthoringAsset || SubgraphAuthoringAsset.Story == null)
            {
                return;
            }

            EnsureVariableFieldsAreUpToDate();

            List<VariableInfo> subgraphStoryParameters = SubgraphAuthoringAsset.Story.Variables;

            // Check if number of subgraph story param types is correct
            if (subgraphStoryParameters.Count != m_StoryFields.Count)
            {
                RecreateStoryFields(subgraphStoryParameters);
                return;
            }

            // Check if subgraph story param types align with field types
            for (int i = 0; i < subgraphStoryParameters.Count; ++i)
            {
                VariableInfo info = subgraphStoryParameters[i];
                Type fieldValueType = m_StoryFields[i]?.Type;
                if (!fieldValueType.IsAssignableFrom(info.Type))
                {
                    RecreateStoryFields(subgraphStoryParameters);
                    return;
                }
            }
        }

        private void EnsureVariableFieldsAreUpToDate()
        {
            HashSet<FieldModel> deprecatedFields = null;
            if (IsDynamic)
            {
                if (RequiredBlackboard != null)
                {
                    foreach (var fieldModel in m_FieldValues)
                    {
                        if (fieldModel.FieldName == k_SubgraphFieldName || fieldModel.FieldName == k_BlackboardFieldName)
                        {
                            continue;
                        }

                        if (IsFieldModelOutdated(fieldModel, RequiredBlackboard))
                        {
                            deprecatedFields ??= new HashSet<FieldModel>();
                            deprecatedFields.Add(fieldModel);
                        }
                    }

                    foreach (VariableModel variable in RequiredBlackboard.Variables)
                    {
                        RemoveFieldIfShared(variable);
                    }
                }
            }
            else if (SubgraphAuthoringAsset != null)
            {
                foreach (var fieldModel in m_FieldValues)
                {
                    if (fieldModel.FieldName == k_SubgraphFieldName || fieldModel.FieldName == k_BlackboardFieldName)
                    {
                        continue;
                    }

                    if (IsFieldModelOutdated(fieldModel, SubgraphAuthoringAsset.Blackboard)
                        && SubgraphAuthoringAsset.m_Blackboards.All(blackboard =>
                            IsFieldModelOutdated(fieldModel, blackboard)))
                    {
                        deprecatedFields ??= new HashSet<FieldModel>();
                        deprecatedFields.Add(fieldModel);
                    }
                }
                foreach (VariableModel variable in SubgraphAuthoringAsset.Blackboard.Variables)
                {
                    RemoveFieldIfShared(variable);
                }

                foreach (var blackboard in SubgraphAuthoringAsset.m_Blackboards)
                {
                    foreach (var variable in blackboard.Variables)
                    {
                        RemoveFieldIfShared(variable);
                    }
                }
            }

            if (deprecatedFields != null)
            {
                foreach (var deprecatedField in deprecatedFields)
                {
                    m_FieldValues.Remove(deprecatedField);
                }
                Asset?.SetAssetDirty(false);
            }

            bool IsFieldModelOutdated(FieldModel fieldModel, BlackboardAsset asset)
            {
                return asset.Variables.Find(model =>
                            model.Name == fieldModel.FieldName && fieldModel.Type.Equals(model.Type)) == null;
            }
        }

        private void RemoveFieldIfShared(VariableModel variable)
        {
            if (!variable.IsShared)
            {
                return;
            }

            FieldModel field = GetOrCreateField(variable.Name, variable.Type);
            if (field != null)
            {
                m_FieldValues.Remove(field);
            }
        }

        private void ClearFields()
        {
            m_FieldValues.Clear();
            m_StoryFields.Clear();
            GetOrCreateField(k_SubgraphFieldName, typeof(BehaviorGraph));
            GetOrCreateField(k_BlackboardFieldName, typeof(BehaviorBlackboardAuthoringAsset));
        }

        private void RecreateStoryFields(List<VariableInfo> storyParameters)
        {
            var oldStoryFields = m_StoryFields.ToList();
            m_StoryFields.Clear();
            for (int m = 0; m < storyParameters.Count; m++)
            {
                VariableInfo info = storyParameters[m];
                var field = GetOrCreateField(Util.NicifyVariableName(info.Name), info.Type);
                m_StoryFields.Add(field);
                oldStoryFields.Remove(field);
            }

            foreach (var oldStoryField in oldStoryFields)
            {
                m_FieldValues.Remove(oldStoryField);
            }
        }

        public override void OnValidate()
        {
            base.OnValidate();

            if (BehaviorGraphAssetRegistry.IsRegistryStateValid)
            {
                ValidateCachedRuntimeGraph();

                if (SubgraphAuthoringAsset.ContainsCyclicReferenceTo(Asset as BehaviorAuthoringGraph))
                {
                    Debug.LogWarning($"Subgraph {RuntimeSubgraph.name} contains a cyclic reference to {Asset.name}. The subgraph {RuntimeSubgraph.name} will be removed.");
                    SubgraphField.LinkedVariable.ObjectValue = null;
                    ClearFields();
                }
            }

            UpdateNodeType();
        }

#if UNITY_EDITOR
        private void RefreshSubgraphVersion()
        {
            BehaviorAuthoringGraph cachedGraph = SubgraphAuthoringAsset;
            if (cachedGraph == null)
            {
                // Reference was lost, wait for the rebuild to cleanup.
                return;
            }

            var behaviorAuthGraph = Asset as BehaviorAuthoringGraph;
            if (IsDynamic)
            {
                behaviorAuthGraph.RemoveDependency(cachedGraph);
            }
            else
            {
                behaviorAuthGraph.AddOrUpdateDependency(cachedGraph);
            }
        }
#endif

        public void ValidateCachedRuntimeGraph()
        {
#if UNITY_EDITOR
            if (m_SubgraphAssetId != default)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(m_SubgraphAssetId.ToString());
                if (!string.IsNullOrEmpty(path))
                {
                    BehaviorAuthoringGraph result = UnityEditor.AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
                    if (result != null)
                    {
                        m_SubgraphAuthoringAsset = result;
                        m_SubgraphAssetId = default;
                        Asset?.SetAssetDirty(false);
                    }
                }
                else
                {
                    // This typically occurs when a subgraph with the corresponding ID is missing from the project during
                    // graph import. Log a warning for the user to verify that all RunSubgraph nodes are properly configured.
                    Debug.LogWarning(
                        $"Failed to locate Behavior subgraph with ID '{m_SubgraphAssetId}'. " +
                        $"Please verify the RunSubgraph node(s) integrity in this asset.\n", this.Asset);
                }
            }
#endif

            // If no linked value (reset node)
            if (SubgraphField.LinkedVariable == null)
            {
                m_ShowStaticSubgraphRepresentation = false;
                m_IsDynamic = false;
                return;
            }
            else
            {
                bool wasDynamic = m_IsDynamic;
                UpdateIsDynamic();

                // If the node was dynamic but the BBV was deleted from its blackboard,
                // IsDynamic transitions to false. Clear the stale BBV reference.
                if (wasDynamic && !m_IsDynamic && !IsSubgraphFieldLinkedToBlackboardVariable())
                {
                    Debug.LogWarning($"{Asset.name}: Linked subgraph blackboard variable has been deleted. Clearing the RunSubgraph node reference.", Asset);
                    Asset.MarkUndo("Clear linked subgraph variable", true);
                    SubgraphField.LinkedVariable = null;
                    ClearFields();
                    Asset.SetAssetDirty(true);
                    return;
                }
            }

            // If the referenced subgraph asset has been deleted, clear the stale reference.
            // Skip if the linked variable is a blackboard variable (from any blackboard, including linked ones).
            if (!RuntimeSubgraph && !m_SubgraphAuthoringAsset && !IsSubgraphFieldLinkedToBlackboardVariable())
            {
                Debug.LogWarning($"{Asset.name}: Referenced subgraph asset has been deleted. Clearing the RunSubgraph node reference.", Asset);
                Asset.MarkUndo("Clear linked subgraph reference", true);
                SubgraphField.LinkedVariable = null;
                ClearFields();
                Asset.SetAssetDirty(true);
                return;
            }

            // For RunSubgraph (Static):
            // If linked runtime graph isn't linking to a valid authoring graph anymore,
            // retrieve the up to date runtime graph.
            if (GetAuthoringAssetFromRuntimeGraph() == null && m_SubgraphAuthoringAsset != null)
            {
#if UNITY_EDITOR
                // At this stage, the target subgraph was deleted or moved. We try to resolve the missing dependency.
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(m_SubgraphAuthoringAsset.ToString());

                // Retrieve the runtime graph, but in case it was deleted, rebuild it.
                SubgraphField.LinkedVariable.ObjectValue = m_SubgraphAuthoringAsset.BuildRuntimeGraph(forceRebuild: false);

                // In case we the target subgraph was rebuild, save now to force rebuild the parent graph.
                UnityEditor.AssetDatabase.SaveAssetIfDirty(m_SubgraphAuthoringAsset);
#endif
            }
        }

        public void CacheRuntimeGraphId()
        {
            BehaviorAuthoringGraph cachedGraph = SubgraphAuthoringAsset;
            ClearOverriddenVariables();
            if (cachedGraph == null)
            {
                m_SubgraphAuthoringAsset = default;
            }
            else
            {
                m_SubgraphAuthoringAsset = cachedGraph;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RefreshSubgraphVersion();
            }
#endif
        }

        private bool IsSubgraphFieldLinkedToBlackboardVariable()
        {
            if (Asset is not BehaviorAuthoringGraph behaviorGraph || SubgraphField.LinkedVariable == null)
            {
                return false;
            }

            SerializableGUID id = SubgraphField.LinkedVariable.ID;
            foreach (VariableModel variable in behaviorGraph.Blackboard.Variables)
            {
                if (variable.ID == id)
                {
                    return true;
                }
            }

            foreach (BehaviorBlackboardAuthoringAsset blackboard in behaviorGraph.m_Blackboards)
            {
                foreach (VariableModel variable in blackboard.Variables)
                {
                    if (variable.ID == id)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void UpdateIsDynamic()
        {
            if (SubgraphField.LinkedVariable == null)
            {
                // If nothing is linked to the field, the node is static by default.
                m_IsDynamic = false;
                return;
            }

            // Workaround for legacy graph edge-cases.
            if (Asset == null)
            {
                return;
            }

            // Ensure that node is linked to a BBV for Dynamic to be valid.
            // Check main blackboard and linked blackboards.
            bool isExpectedDynamic = false;
            foreach (VariableModel variable in Asset.Blackboard.Variables)
            {
                if (variable == SubgraphField.LinkedVariable)
                {
                    isExpectedDynamic = true;
                    break;
                }
            }

            if (!isExpectedDynamic && Asset is BehaviorAuthoringGraph behaviorGraph)
            {
                foreach (BehaviorBlackboardAuthoringAsset blackboard in behaviorGraph.m_Blackboards)
                {
                    foreach (VariableModel variable in blackboard.Variables)
                    {
                        if (variable == SubgraphField.LinkedVariable)
                        {
                            isExpectedDynamic = true;
                            break;
                        }
                    }
                    if (isExpectedDynamic) break;
                }
            }

            if (m_IsDynamic != isExpectedDynamic)
            {
                // Mismatched detected - need rebuild.
                m_IsDynamic = isExpectedDynamic;
                Asset.SetAssetDirty(true);
            }
        }

        // This would usually be handled in SubgraphNodeTransformer.
        // However, because assets can be edited from outside the editor (e.g. source control),
        // the node model also needs a way to resolve itself when the asset ValidateAsset is called.
        private void UpdateNodeType()
        {
            var expectedType = IsDynamic ? typeof(RunSubgraphDynamic) : typeof(RunSubgraph);

            if (NodeType != null && NodeType.Type == expectedType)
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
