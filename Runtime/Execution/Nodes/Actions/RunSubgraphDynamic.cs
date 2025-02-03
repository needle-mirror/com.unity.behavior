using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    internal partial class RunSubgraphDynamic : Action
    {
        [SerializeReference] public BlackboardVariable<BehaviorGraph> SubgraphVariable;
        
        internal BehaviorGraph Subgraph => SubgraphVariable.Value;
        [SerializeReference] public RuntimeBlackboardAsset RequiredBlackboard;
        [SerializeReference] public List<DynamicBlackboardVariableOverride> DynamicOverrides;
        private BehaviorGraph m_InitializedGraph = null;
        [SerializeField][CreateProperty]
        private bool m_IsInitialized = false;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            SubgraphVariable.OnValueChanged += OnSubgraphChanged;

            if (SubgraphVariable?.ObjectValue == null)
            {
                return Status.Failure;
            }

            if (Subgraph == null || Subgraph.RootGraph == null)
            {
                return Status.Failure;
            }
            
            if (GameObject != null)
            {
                BehaviorGraphAgent agent = GameObject.GetComponent<BehaviorGraphAgent>();
                if (agent != null)
                {
                    BehaviorGraph graph = agent.Graph;
                    if (graph != null && SubgraphVariable.Value == graph)
                    {
                        LogFailure($"Running {SubgraphVariable.Value.name} will create a cycle and can not be used as subgraph for {graph}. " +
                            $"Select a different graph to run dynamically.", true);
                        return Status.Failure;
                    }
                }
            }

            if (TryInitialize() == false)
            {
                LogFailure($"Failed to initialize subgraph '{Subgraph.name}'.");
                return Status.Failure;
            }

            return Subgraph.RootGraph.StartNode(Subgraph.RootGraph.Root) switch
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
                LogFailure($"Failed to initialize subgraph on update. This can happen when setting the subgraph to null while it is running.");
                return Status.Failure;
            }

            Subgraph.Tick();
            return Subgraph.RootGraph.Root.CurrentStatus switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Running,
            };
        }

        /// <inheritdoc cref="OnEnd" />
        protected override void OnEnd()
        {
            SubgraphVariable.OnValueChanged -= OnSubgraphChanged;

            if (SubgraphVariable.ObjectValue == null)
            {
                return;
            }

            if (Subgraph?.RootGraph?.Root != null)
            {
                Subgraph.RootGraph.EndNode(Subgraph.RootGraph.Root);
            }
        }

        private bool TryInitialize()
        {
            if (m_InitializedGraph != SubgraphVariable.Value)
            {
                m_IsInitialized = false;
            }
            if (m_IsInitialized)
            {
                return true;
            }

            if (Subgraph == null || Subgraph.RootGraph == null)
            {
                return false;
            }

            SubgraphVariable.OnValueChanged -= OnSubgraphChanged;
            SubgraphVariable.Value = ScriptableObject.Instantiate(Subgraph);
            SubgraphVariable.OnValueChanged += OnSubgraphChanged;
                
            SubgraphVariable.Value.AssignGameObjectToGraphModules(GameObject);
                
            InitChannelAndBlackboard();

            m_IsInitialized = true;
            m_InitializedGraph = SubgraphVariable.Value;
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
            foreach (BlackboardVariable variable in Subgraph.RootGraph.Blackboard.Variables)
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

            ApplyOverridesToBlackboardReference(Subgraph.BlackboardReference);

            bool matchingBlackboard = false;

            if (RequiredBlackboard != null)
            {
                foreach (BlackboardReference reference in Subgraph.RootGraph.BlackboardGroupReferences)
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
                    Debug.LogWarning($"No matching Blackboard of type {RequiredBlackboard.name} found for graph {SubgraphVariable.Value.name}. Any assigned variables will not be set.");
                }
            }
        }

        private void ApplyOverridesToBlackboardReference(BlackboardReference reference)
        {
            foreach (DynamicBlackboardVariableOverride dynamicOverride in DynamicOverrides)
            {
                foreach (BlackboardVariable variable in reference.Blackboard.Variables)
                {
                    // Shared variables cannot be assigned/modified by this node.
                    if (reference.SourceBlackboardAsset.IsSharedVariable(variable.GUID))
                    {
                        continue;
                    }

                    if (variable.GUID == dynamicOverride.Variable.GUID)
                    {
                        variable.ObjectValue = dynamicOverride.Variable.ObjectValue;
                        continue;
                    }

                    if (variable.Name != dynamicOverride.Name || variable.Type != dynamicOverride.Variable.Type)
                    {
                        continue;
                    }

                    variable.ObjectValue = dynamicOverride.Variable.ObjectValue;

                    // If the variable is a Blackboard Variable and not a local value assigned from the Inspector.
                    if (string.IsNullOrEmpty(dynamicOverride.Variable.Name))
                    {
                        continue;
                    }

                    // There is no risk of recursive set value because we link their value without notification.
                    variable.OnValueChanged += () =>
                    {
                        // Update the original assigned variable if it has been modified in the subgraph.
                        dynamicOverride.Variable.SetObjectValueWithoutNotify(variable.ObjectValue);
                    };
                    dynamicOverride.Variable.OnValueChanged += () =>
                    {
                        // Update the subgraph variable if the original variable is modified.
                        // Can happens when subgraph is decoupled and running in parallel from main graph . 
                        variable.SetObjectValueWithoutNotify(dynamicOverride.Variable.ObjectValue);
                    };
                }
            }
        }
        
        protected override void OnSerialize()
        {
            if (Subgraph != m_InitializedGraph)
            {
                m_IsInitialized = false;
            }
        }

        protected override void OnDeserialize()
        {
            if (m_IsInitialized)
            {
                m_InitializedGraph = Subgraph;
            }
        }
    }
}