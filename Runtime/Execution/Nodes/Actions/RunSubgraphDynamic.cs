using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Run Subgraph Dynamically",
        description: "You're going to run a subgraph dynamically. Make sure you have your subgraph implement a " +
        "Blackboard asset that you can refer to in this inspector.",
        category: "Subgraphs",
        hideInSearch: true,
        id: "a9ca68fd9e704c8abdaacf6697e42a4a")]
    internal partial class RunSubgraphDynamic : Action
    {
        internal const string kMissingTypesErrorMessage = "Cannot run dynamic subgraph '{0}' because it contains SerializeReference types which are missing.";
        internal const string kCyclicReferenceErrorMessage = "Running '{0}' would create a cyclic reference. Please choose a different subgraph to run dynamically.";
        internal const string kInitFailsDuringUpdateErrorMessage = "Failed to initialize subgraph on update. This can happen when setting the subgraph to null while it is running.";
        // Serialize the list of additional BehaviorGraphModule for runtime serialization -> Override module method in BehaviorGraph
        // We may still need a reference or id to grab the runtime asset to duplicate from -> BehaviorGraph has no asset id, it needs one -> Get it from the behaviorGraphModule.
        // Copy this asset & override the BehaviorGraphModule's at runtime.

        [SerializeReference, DontCreateProperty] public BlackboardVariable<BehaviorGraph> SubgraphVariable;
        // The source asset.
        private BehaviorGraph SourceSubgraph { get => SubgraphVariable.Value; }

        // Just save the assetId for runtime -> Investigate the removal of the RequiredBlackboard field, but don't take risks. Use AssetId Below.
        [SerializeField, DontCreateProperty] public RuntimeBlackboardAsset RequiredBlackboard;

        [SerializeReference] public List<DynamicBlackboardVariableOverride> DynamicOverrides;

        // The instantiated asset used by this node.
        [CreateProperty]
        private BehaviorGraph m_InstancedSubgraph = null;

        [SerializeField, CreateProperty]
        private bool m_IsInitialized = false;

        [CreateProperty]
        private bool m_SubgraphStarted = false;

        // Keeps track of the subgraph's variable and dynamic override that needs to be kept in sync.
        private List<DynamicBinding> m_ActiveBindings = new List<DynamicBinding>();

        private ObjectPool<DynamicBinding> m_DynamicBindingPool;
        private ObjectPool<DynamicBinding> DynamicBindingPool
        {
            get
            {
                m_DynamicBindingPool ??= new ObjectPool<DynamicBinding>(
                        createFunc: () => new DynamicBinding(),
                        actionOnGet: null,
                        actionOnRelease: (capture) => capture.Release(),
                        actionOnDestroy: (capture) => capture.Release(),
                        collectionCheck: true,
                        defaultCapacity: 10,
                        maxSize: 124
                    );

                return m_DynamicBindingPool;
            }
        }

        private BehaviorGraphAgent Agent
        {
            get
            {
                m_Agent ??= GameObject.GetComponent<BehaviorGraphAgent>();
                return m_Agent;
            }
        }
        private BehaviorGraphAgent m_Agent;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (SourceSubgraph == null || SourceSubgraph.RootGraph == null)
            {
                LogFailure("No valid graph asset assigned.");
                return Status.Failure;
            }
#if UNITY_EDITOR
            // Editor guardrail.
            if (UnityEditor.SerializationUtility.HasManagedReferencesWithMissingTypes(SourceSubgraph))
            {
                LogFailure(string.Format(kMissingTypesErrorMessage, SourceSubgraph.name));
                return Status.Failure;
            }
#endif

            if (GameObject != null && Agent != null)
            {
                if (SourceSubgraph.HasSameSourceAssetAs(Agent.Graph))
                {
                    LogFailure(string.Format(kCyclicReferenceErrorMessage, SourceSubgraph.name), true);
                    return Status.Failure;
                }
            }

            if (TryInitialize() == false)
            {
                LogFailure($"Failed to initialize subgraph '{SourceSubgraph.name}'.");
                return Status.Failure;
            }

            m_SubgraphStarted = true;
            return m_InstancedSubgraph.RootGraph.StartNode(m_InstancedSubgraph.RootGraph.Root) switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Running,
            };
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            if (!m_IsInitialized && TryInitialize() == false)
            {
                LogFailure(kInitFailsDuringUpdateErrorMessage);
                return Status.Failure;
            }

            if (m_InstancedSubgraph == null)
            {
                LogFailure("The dynamic subgraph you are trying to run is null.");
                return Status.Failure;
            }

            // Start subgraph if needed.
            if (m_SubgraphStarted == false && m_InstancedSubgraph.RootGraph.StartNode(m_InstancedSubgraph.RootGraph.Root) == Status.Failure)
            {
                return Status.Failure;
            }

            m_InstancedSubgraph.Tick();
            return m_InstancedSubgraph.RootGraph.Root.CurrentStatus switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Running,
            };
        }

        /// <inheritdoc cref="OnEnd" />
        protected override void OnEnd()
        {
            if (!m_IsInitialized || m_InstancedSubgraph == null)
            {
                return;
            }

            ClearVariableBindings();

            SubgraphVariable.OnValueChanged -= OnSubgraphChanged;
            if (m_InstancedSubgraph?.RootGraph?.Root != null)
            {
                m_InstancedSubgraph.RootGraph.EndNode(m_InstancedSubgraph.RootGraph.Root);
            }
        }

        private bool TryInitialize()
        {
            if (!IsInstancedRuntimeGraphValid())
            {
                m_IsInitialized = false;
            }

            if (m_IsInitialized)
            {
                return true;
            }

            // Can happens when setting Subgraph to null while the node is running.
            if (SourceSubgraph == null || SourceSubgraph.RootGraph == null)
            {
                return false;
            }

            ClearVariableBindings();

            SubgraphVariable.OnValueChanged -= OnSubgraphChanged;
            // Instantiate a new copy based on the source asset
            m_InstancedSubgraph = ScriptableObject.Instantiate(SourceSubgraph);
            m_InstancedSubgraph.AssignGameObjectToGraphModules(GameObject);
            // Listens to the source asset changing
            SubgraphVariable.OnValueChanged += OnSubgraphChanged;

            InitChannelAndBlackboard();

            m_IsInitialized = true;
            m_SubgraphStarted = false;
            return true;
        }

        private void OnSubgraphChanged()
        {
            m_IsInitialized = false;
            TryInitialize();
        }

        private void InitChannelAndBlackboard()
        {
            // Initialize default event channels for unassigned channel variables.
            foreach (BlackboardVariable variable in m_InstancedSubgraph.RootGraph.Blackboard.Variables)
            {
                if (typeof(EventChannelBase).IsAssignableFrom(variable.Type) && variable.ObjectValue == null)
                {
                    ScriptableObject channel = ScriptableObject.CreateInstance(variable.Type);
                    channel.name = $"Default {variable.Name} Channel";
                    variable.ObjectValue = channel;
                }
            }

            SetVariablesOnSubgraph();
        }

        private void SetVariablesOnSubgraph()
        {
            // Blackboard value cannot be null but the list can be empty.
            if (DynamicOverrides.Count == 0)
            {
                return;
            }

            ApplyOverridesToBlackboardReference(m_InstancedSubgraph.BlackboardReference);

            bool matchingBlackboard = false;

            if (RequiredBlackboard != null)
            {
                foreach (BlackboardReference reference in m_InstancedSubgraph.RootGraph.BlackboardGroupReferences)
                {
                    if (reference.SourceBlackboardAsset.AssetID != RequiredBlackboard.AssetID)
                    {
                        continue;
                    }

                    ApplyOverridesToBlackboardReference(reference);

                    matchingBlackboard = true;
                }

                if (!matchingBlackboard)
                {
                    Debug.LogWarning($"No matching Blackboard of type {RequiredBlackboard.name} found for graph {SourceSubgraph.name}. Any assigned variables will not be set.");
                }
            }
        }

        private void ApplyOverridesToBlackboardReference(BlackboardReference reference)
        {
            foreach (DynamicBlackboardVariableOverride dynamicOverride in DynamicOverrides)
            {
                foreach (BlackboardVariable variable in reference.Blackboard.Variables)
                {
                    // Serialization issue nullcheck
                    if (variable == null || dynamicOverride == null)
                    {
                        continue;
                    }

                    // Shared variables cannot be assigned/modified by this node.
                    if (reference.SourceBlackboardAsset.IsSharedVariable(variable.GUID))
                    {
                        continue;
                    }

                    if (variable.GUID != dynamicOverride.Variable.GUID &&
                        (variable.Name != dynamicOverride.Name || variable.Type != dynamicOverride.Variable.Type))
                    {
                        continue;
                    }

                    // No need to notify during initialization.
                    variable.SetObjectValueWithoutNotify(dynamicOverride.Variable.ObjectValue);

                    // If the variable is a Blackboard Variable and not a local value assigned from the Inspector.
                    if (string.IsNullOrEmpty(dynamicOverride.Variable.Name))
                    {
                        continue;
                    }

                    var capture = DynamicBindingPool.Get();
                    capture.Register(variable, dynamicOverride.Variable);
                    m_ActiveBindings.Add(capture);
                }
            }
        }

        private void ClearVariableBindings()
        {
            foreach (var closure in m_ActiveBindings)
            {
                closure.Release();
                DynamicBindingPool.Release(closure);
            }
            m_ActiveBindings.Clear();
        }

        protected override void OnSerialize()
        {
            m_InstancedSubgraph.SerializeGraphModules();
            if (IsInstancedRuntimeGraphValid())
            {
                m_IsInitialized = false;
            }
        }

        protected override void OnDeserialize()
        {
        }

        private bool IsInstancedRuntimeGraphValid()
        {
            if (SourceSubgraph == null)
            {
                return false;
            }

            return m_InstancedSubgraph != null && m_InstancedSubgraph.RootGraph.AuthoringAssetID == SourceSubgraph.RootGraph.AuthoringAssetID;
        }

        private class DynamicBinding
        {
            // Guard flags used to only propagate changes if they didn't come from a sync operation
            public bool m_IsSyncingToParent = false;
            public bool m_IsSyncingToChild = false;
            public BlackboardVariable m_SubgraphVariable; // Reference to the loop variable
            public BlackboardVariable m_OverrideVariable; // Reference to the loop variable

            public void Register(BlackboardVariable subgraphVar, BlackboardVariable overrideVar)
            {
                Debug.Assert(subgraphVar != null);
                Debug.Assert(overrideVar != null);
                m_SubgraphVariable = subgraphVar;
                m_OverrideVariable = overrideVar;
                m_SubgraphVariable.OnValueChanged += VariableChangedHandler;
                m_OverrideVariable.OnValueChanged += OverrideChangedHandler;
            }

            public void Release()
            {
                if (m_SubgraphVariable != null)
                {
                    m_SubgraphVariable.OnValueChanged -= VariableChangedHandler;
                }
                if (m_OverrideVariable != null)
                {
                    m_OverrideVariable.OnValueChanged -= OverrideChangedHandler;
                }

                m_IsSyncingToParent = false;
                m_IsSyncingToChild = false;
            }

            private void VariableChangedHandler()
            {
                if (!m_IsSyncingToChild)
                {
                    m_IsSyncingToParent = true;
                    try
                    {
                        // Update the original assigned variable if it has been modified in the subgraph.
                        // Make sure to raise the parent OnValueChanged so the change is propagated as needed.
                        m_OverrideVariable.ObjectValue = m_SubgraphVariable.ObjectValue;
                    }
                    finally
                    {
                        m_IsSyncingToParent = false;
                    }
                }
            }

            private void OverrideChangedHandler()
            {
                if (!m_IsSyncingToParent)
                {
                    m_IsSyncingToChild = true;
                    try
                    {
                        // Update the subgraph variable if the original variable is modified.
                        // Can happens when subgraph is decoupled and running in parallel from main graph.
                        m_SubgraphVariable.ObjectValue = m_OverrideVariable.ObjectValue;
                    }
                    finally
                    {
                        m_IsSyncingToChild = false;
                    }
                }
            }
        }

    }
}
