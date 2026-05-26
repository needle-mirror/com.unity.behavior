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
        [SerializeReference] public BlackboardVariable<BehaviorGraph> SubgraphVariable;
        // The source asset.
        private BehaviorGraph SourceSubgraph { get => SubgraphVariable?.Value; }

        [SerializeField] public RuntimeBlackboardAsset RequiredBlackboard;

        [SerializeReference] public List<DynamicBlackboardVariableOverride> DynamicOverrides;

        // Runtime instance, re-created from SourceSubgraph during deserialization.
        private BehaviorGraph m_InstancedSubgraph = null;

#if UNITY_EDITOR        
        internal BehaviorGraph InstancedSubgraph => m_InstancedSubgraph; // For testing purposes.
#endif

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

            if (TryAcquireInstance() == false)
            {
                LogFailure($"Failed to initialize subgraph '{SourceSubgraph.name}'.");
                return Status.Failure;
            }

            m_InstancedSubgraph.Start();
            return GetSubgraphResultStatus();
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            if (m_InstancedSubgraph == null)
            {
                LogFailure(kInitFailsDuringUpdateErrorMessage);
                return Status.Failure;
            }

            if (!m_InstancedSubgraph.IsRunning)
            {
                return GetSubgraphResultStatus();
            }

            m_InstancedSubgraph.Tick();
            return GetSubgraphResultStatus();
        }

        /// <inheritdoc cref="OnEnd" />
        protected override void OnEnd()
        {
            if (m_InstancedSubgraph == null)
            {
                return;
            }

            ClearVariableBindings();
            m_InstancedSubgraph.End();
        }

        /// <summary>
        /// Acquires a subgraph instance from the source asset when needed.
        /// If a valid instance already exists for the current source authoring asset, it is reused.
        /// If the source changed, the existing instance is released and a new one is acquired.
        /// </summary>
        /// <remarks>
        /// Do NOT warm up in OnSetup as acquiring nested dynamic subgraphs during setup can recurse through graph initialization.
        /// </remarks>
        private bool TryAcquireInstance()
        {
            if (SourceSubgraph == null || SourceSubgraph.RootGraph == null)
            {
                return false;
            }

            if (GameObject != null && Agent != null && SourceSubgraph.HasSameSourceAssetAs(Agent.Graph))
            {
                LogFailure(string.Format(kCyclicReferenceErrorMessage, SourceSubgraph.name), true);
                return false;
            }

            if (IsInstancedRuntimeGraphValid())
            {
                SetVariablesOnSubgraph();
                return true;
            }

            CleanupGraphInstance();

            // Acquire new instance.
            m_InstancedSubgraph = BehaviorGraph.AcquireInstance(GameObject, SourceSubgraph);
            SetVariablesOnSubgraph();
            if (SubgraphVariable != null)
            {
                SubgraphVariable.OnValueChanged += OnSubgraphChanged;
            }
            return true;
        }

        private void OnSubgraphChanged()
        {
            // The source changed at runtime: stop and release the currently running instance first.
            // If the new source is invalid (null), the next OnUpdate will fail initialization.
            CleanupGraphInstance();

            if (!TryAcquireInstance() || m_InstancedSubgraph == null)
            {
                return;
            }

            // If the RunSubgraphDynamic is currently running, start the newly acquired instance immediately.
            if (IsRunning)
            {
                m_InstancedSubgraph.Restart();
            }
        }

        protected override void OnTeardown()
        {
            CleanupGraphInstance();
        }

        private void CleanupGraphInstance()
        {
            ClearVariableBindings();
            if (SubgraphVariable != null)
            {
                SubgraphVariable.OnValueChanged -= OnSubgraphChanged;
            }
            BehaviorGraph.ReleaseInstance(m_InstancedSubgraph);
            m_InstancedSubgraph = null;
        }

        private Status GetSubgraphResultStatus()
        {
            // This should never happen as graph always has a Start node.
            // But just in case, we return Failure by default.
            if (m_InstancedSubgraph?.RootGraph?.Root == null)
            {
                return Status.Failure;
            }

            return m_InstancedSubgraph.CurrentStatus switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Running
            };
        }

        /// <summary>
        /// Sets the variables on the subgraph by applying the dynamic overrides to the blackboard reference.
        /// Needs to be called every time the subgraph is started (or re-initialized).
        /// </summary>
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

        /// <summary>
        /// Registers the dynamic overrides to the given blackboard reference.
        /// We use a double binding mechanism to ensure the variables are kept in sync between the subgraph and the override.
        /// </summary>
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
            if (m_InstancedSubgraph == null)
            {
                return;
            }

            m_InstancedSubgraph.SerializeGraphModules();
        }

        protected override void OnDeserialize()
        {
            if (SubgraphVariable == null || SourceSubgraph == null)
            {
                return;
            }

            if (TryAcquireInstance() && IsRunning)
            {
                m_InstancedSubgraph.Start();
            }
        }

        private bool IsInstancedRuntimeGraphValid()
        {
            if (SourceSubgraph?.RootGraph == null || m_InstancedSubgraph?.RootGraph == null)
            {
                return false;
            }

            return m_InstancedSubgraph.RootGraph.AuthoringAssetID == SourceSubgraph.RootGraph.AuthoringAssetID;
        }

        private class DynamicBinding
        {
            // Guard flags used to only propagate changes if they didn't come from a sync operation
            private bool m_IsSyncingToParent = false;
            private bool m_IsSyncingToChild = false;
            private BlackboardVariable m_SubgraphVariable; // Reference to the loop variable
            private BlackboardVariable m_OverrideVariable; // Reference to the loop variable

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
