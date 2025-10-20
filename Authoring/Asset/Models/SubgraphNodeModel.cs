using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
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

        protected SubgraphNodeModel(SubgraphNodeModel nodeModelOriginal, BehaviorAuthoringGraph asset) : base(nodeModelOriginal, asset)
        {
            ShowStaticSubgraphRepresentation = nodeModelOriginal.ShowStaticSubgraphRepresentation;

            GetOrCreateField(k_SubgraphFieldName, typeof(BehaviorGraph));
            if (nodeModelOriginal.RuntimeSubgraph != null)
            {
                SubgraphField.LinkedVariable = nodeModelOriginal.SubgraphField.LinkedVariable;
            }

            GetOrCreateField(k_BlackboardFieldName, typeof(BehaviorBlackboardAuthoringAsset));
            if (nodeModelOriginal.RequiredBlackboard != null)
            {
                BlackboardAssetField.LinkedVariable = nodeModelOriginal.BlackboardAssetField.LinkedVariable;
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
                UpdateIsDynamic();
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
            bool isExpectedDynamic = false;
            foreach (VariableModel variable in Asset.Blackboard.Variables)
            {
                if (variable == SubgraphField.LinkedVariable)
                {
                    isExpectedDynamic = true;
                    break;
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
