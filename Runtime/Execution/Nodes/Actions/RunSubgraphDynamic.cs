using System.Collections.Generic;
using UnityEngine;

namespace Unity.Behavior
{
    internal partial class RunSubgraphDynamic : Action
    {
        [SerializeReference] public BlackboardVariable<BehaviorGraph> SubgraphVariable;
        public BehaviorGraphModule Subgraph => SubgraphVariable.Value.RootGraph;
        [SerializeReference] public RuntimeBlackboardAsset RequiredBlackboard;
        [SerializeReference] public List<DynamicBlackboardVariableOverride> DynamicOverrides;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            SubgraphVariable.OnValueChanged += OnSubgraphChanged;

            if (SubgraphVariable?.ObjectValue == null)
            {
                return Status.Failure;
            }

            if (Subgraph?.Root == null)
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
                        Debug.LogWarning($"Running {SubgraphVariable.Value.name} will create a cycle and can not be used as subgraph for {graph}. Select a different graph to run dynamically.");
                        return Status.Failure;
                    }
                }
            }

            InitChannelAndBlackboard();

            return Subgraph.StartNode(Subgraph.Root) switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Running,
            };
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            Subgraph.Tick();
            return Subgraph.Root.CurrentStatus switch
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

            if (Subgraph?.Root != null)
            {
                Subgraph.EndNode(Subgraph.Root);
            }
        }

        private void OnSubgraphChanged()
        {
            if (Subgraph != null)
            {
                SubgraphVariable.OnValueChanged -= OnSubgraphChanged;
                InitChannelAndBlackboard();
                StartNode(this);
            }
        }

        private void InitChannelAndBlackboard()
        {
            // Initialize default event channels for unassigned channel variables.
            foreach (BlackboardVariable variable in Subgraph.Blackboard.Variables)
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
                foreach (BlackboardReference reference in Subgraph.BlackboardGroupReferences)
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

                    variable.OnValueChanged += () =>
                    {
                        // Update the original assigned variable if it has been modified in the subgraph.
                        dynamicOverride.Variable.ObjectValue = variable.ObjectValue;
                    };
                }
            }
        }
    }
}