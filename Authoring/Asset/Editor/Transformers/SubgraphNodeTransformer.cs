using Unity.Behavior.GraphFramework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Behavior
{
    internal class SubgraphNodeTransformer : INodeTransformer
    {
        public Type NodeModelType => typeof(SubgraphNodeModel);

        public Node CreateNodeFromModel(GraphAssetProcessor graphAssetProcessor, NodeModel nodeModel)
        {
            SubgraphNodeModel subgraphNodeModel = nodeModel as SubgraphNodeModel;

            if (subgraphNodeModel.IsDynamic)
            {
                subgraphNodeModel.NodeType = typeof(RunSubgraphDynamic);
            }
            else
            {
                subgraphNodeModel.NodeType = typeof(RunSubgraph);
            }
            
            var node = Activator.CreateInstance(subgraphNodeModel.NodeType) as Node;

            return node;
        }

        public void ProcessNode(GraphAssetProcessor graphAssetProcessor, NodeModel nodeModel, Node node)
        {
            SubgraphNodeModel subgraphNodeModel = nodeModel as SubgraphNodeModel;

            subgraphNodeModel.ValidateCachedRuntimeGraph();
            
            if (subgraphNodeModel.IsDynamic)
            {
                BehaviorGraphNodeModel.FieldModel graphField = null;
                foreach (BehaviorGraphNodeModel.FieldModel fieldModel in subgraphNodeModel.Fields)
                {
                    if (fieldModel.FieldName == SubgraphNodeModel.k_SubgraphFieldName)
                    {
                        graphField = fieldModel;
                    }
                }

                // Set the correct linked variable as the SubgraphVariable.
                RunSubgraphDynamic subgraphDynamic = node as RunSubgraphDynamic;
                BlackboardVariable graphVariable = graphAssetProcessor.GetVariableFromFieldModel(graphField);
                if (graphVariable != null)
                {
                    subgraphDynamic!.SubgraphVariable = graphVariable as BlackboardVariable<BehaviorGraph>;
                }
                // If a required Blackboard has been set for the subgraphs that can be run, set it on the node.
                if (subgraphNodeModel.RequiredBlackboard != null)
                {
                    subgraphDynamic!.RequiredBlackboard = subgraphNodeModel.RequiredBlackboard.BuildRuntimeBlackboard();
                }
                
                subgraphDynamic!.DynamicOverrides = GetDynamicOverrides(subgraphNodeModel, graphAssetProcessor);
            }
            else
            {
                // If the subgraph isn't assigned, nothing needs to be processed.
                if (subgraphNodeModel.SubgraphAuthoringAsset == null)
                {
                    return;
                }
                BehaviorAuthoringGraph subgraphAsset = subgraphNodeModel.SubgraphAuthoringAsset;

                RunSubgraph runSubgraph = node as RunSubgraph;
                GraphAssetProcessor subgraphAssetProcessor = new GraphAssetProcessor(subgraphAsset, graphAssetProcessor.Graph);
                subgraphAssetProcessor.Cleanup();
                var variableOverridesFromFields = GetVariableOverridesFromFields(graphAssetProcessor, subgraphNodeModel, subgraphAsset);
                subgraphAssetProcessor.InitializeBlackboard(variableOverridesFromFields);
                BehaviorGraphModule subgraph = subgraphAssetProcessor.BuildGraph();

                // The only value assigned to the runtime node is an instance of the runtime graph.
                runSubgraph!.Subgraph = subgraph;
            }
        }

        private static Dictionary<SerializableGUID, BlackboardVariable> GetVariableOverridesFromFields(GraphAssetProcessor graphAssetProcessor, SubgraphNodeModel subgraphNodeModel, BehaviorAuthoringGraph subgraphAsset)
        {
            // Build a runtime graph for the subgraph using variable overrides.
            Dictionary<SerializableGUID, BlackboardVariable> variableOverrides = new();
            foreach (BehaviorGraphNodeModel.FieldModel fieldModel in subgraphNodeModel.Fields)
            {
                if (!IsFieldVariableOverride(fieldModel))
                {
                    continue;
                }

                BlackboardVariable variableToAssign = graphAssetProcessor.GetVariableFromFieldModel(fieldModel);
                if (variableToAssign.GUID == default)
                {
                    variableToAssign.Name = fieldModel.FieldName;
                    variableToAssign.GUID = SerializableGUID.Generate();
                }
                
                VariableModel variableToReplace =
                    // Find a matching blackboard variable by name/type, then assign the new variable as an override.
                    subgraphAsset.Blackboard.Variables.FirstOrDefault(variable =>
                        variable.Type == variableToAssign.Type
                        && variable.Name.Equals(fieldModel.FieldName, StringComparison.CurrentCultureIgnoreCase) 
                        && variable.IsExposed);

                if (variableToReplace != null)
                {
                    // If we have no linked variable & there is no overriden local variable value.
                    if (fieldModel.LinkedVariable == null && fieldModel.LocalValue.ObjectValue == variableToReplace.ObjectValue)
                    {
                        continue;
                    }
                    
                    variableOverrides.TryAdd(variableToReplace.ID, variableToAssign);
                    variableToReplace = null;
                }

                // Additionally, check for variables in the added blackboards.
                foreach (BehaviorBlackboardAuthoringAsset blackboard in subgraphAsset.m_Blackboards)
                {
                    variableToReplace = blackboard.Variables.FirstOrDefault(variable =>
                        variable.Type == variableToAssign.Type
                        && variable.Name.Equals(fieldModel.FieldName, StringComparison.CurrentCultureIgnoreCase) 
                        && variable.IsExposed);
                }

                if (variableToReplace != null)
                {
                    // If we have no linked variable & there is no overriden local variable value.
                    if (fieldModel.LinkedVariable == null && fieldModel.LocalValue.ObjectValue == variableToReplace.ObjectValue)
                    {
                        continue;
                    }
                    
                    variableOverrides.TryAdd(variableToReplace.ID, variableToAssign);
                }
            }

            return variableOverrides;
        }

        private List<DynamicBlackboardVariableOverride> GetDynamicOverrides(SubgraphNodeModel subgraphNodeModel, GraphAssetProcessor graphAssetProcessor)
        {
            List<DynamicBlackboardVariableOverride> dynamicOverrides = new ();
            
            graphAssetProcessor.BlackboardReference.GetVariable(BehaviorGraph.k_GraphSelfOwnerID, out BlackboardVariable selfVariable);
            if (selfVariable != null)
            {
                dynamicOverrides.Add(new DynamicBlackboardVariableOverride
                {
                    Name = BehaviorGraphEditor.k_SelfDefaultGraphOwnerName,
                    Variable = selfVariable,
                });
            }
            
            foreach (BehaviorGraphNodeModel.FieldModel fieldModel in subgraphNodeModel.Fields)
            {
                if (!IsFieldVariableOverride(fieldModel))
                {
                    continue;
                }

                BlackboardVariable variableToAssign = graphAssetProcessor.GetVariableFromFieldModel(fieldModel);
                if (variableToAssign == null) // If no variable is linked to the field, it is not an override.
                {
                    continue;
                }
                
                DynamicBlackboardVariableOverride dynamicOverride = new DynamicBlackboardVariableOverride
                {
                    Name = fieldModel.FieldName,
                    Variable = variableToAssign
                };
                
                dynamicOverrides.Add(dynamicOverride);
            }

            return dynamicOverrides;
        }

        private static bool IsFieldVariableOverride(BehaviorGraphNodeModel.FieldModel fieldModel)
        {
            if (fieldModel.FieldName == SubgraphNodeModel.k_SubgraphFieldName) // The graph field is not an override.
            {
                return false;
            }

            if (fieldModel.FieldName == SubgraphNodeModel.k_BlackboardFieldName) // The blackboard field is not an override.
            {
                return false;
            }
            return true;
        }
    }
}