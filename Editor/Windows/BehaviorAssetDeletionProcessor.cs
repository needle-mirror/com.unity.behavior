using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    internal class BehaviorAssetDeletionProcessor : AssetModificationProcessor
    {
        // If an authoring graph or blackboard asset is deleted within Unity, this will close any editor window associated with the asset.
        private static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(BehaviorAuthoringGraph) && AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(BehaviorBlackboardAuthoringAsset))
            {
                return AssetDeleteResult.DidNotDelete;
            }
            
            // Close any matching Behavior Graph Windows.
            BehaviorAuthoringGraph graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
            foreach (BehaviorWindow window in Resources.FindObjectsOfTypeAll<BehaviorWindow>())
            {
                if (window.Asset == graph)
                {
                    window.Close();
                }
            }
            // Close any matching Blackboard Windows.
            BehaviorBlackboardAuthoringAsset blackboardAuthoring = AssetDatabase.LoadAssetAtPath<BehaviorBlackboardAuthoringAsset>(path);
            foreach (BlackboardWindow window in Resources.FindObjectsOfTypeAll<BlackboardWindow>())
            {
                if (window.Asset == blackboardAuthoring)
                {
                    window.Close();
                }
            }
            UpdateBlackboardAssetDependencies(blackboardAuthoring);
            
            // Update any Behavior Graph Windows which have a reference to the deleted Blackboard asset.
            blackboardAuthoring?.InvokeBlackboardDeleted();
            
            return AssetDeleteResult.DidNotDelete;
        }

        private static void UpdateBlackboardAssetDependencies(BehaviorBlackboardAuthoringAsset blackboardAsset)
        {
            if (blackboardAsset == null)
            {
                return;
            }

            List<BehaviorAuthoringGraph> graphToRebuild = new List<BehaviorAuthoringGraph>();
            foreach (BehaviorAuthoringGraph authoringGraph in BehaviorGraphAssetRegistry.GlobalRegistry.Assets)
            {
                if (!authoringGraph.ContainsReferenceTo(blackboardAsset))
                {
                    continue;
                }

                Debug.LogWarning($"Please review and upate the graph '{authoringGraph.name}' to ensure it functions correctly. " 
                    + $"Blackboard asset '{blackboardAsset.name}' has been deleted and all associated references has be removed.", authoringGraph);

                bool hasAtleastOneReference = false;
                authoringGraph.m_Blackboards.Remove(blackboardAsset);
                foreach (var nodeModel in authoringGraph.Nodes)
                {
                    if (nodeModel is not BehaviorGraphNodeModel behaviorNodeModel)
                    {
                        continue;
                    }
                    hasAtleastOneReference |= RemoveBlackboardVariableLinksFromFields(blackboardAsset, behaviorNodeModel.Fields);
                            
                    if (behaviorNodeModel is IConditionalNodeModel conditionalNodeModel)
                    {
                        // Delete variable references from conditions.
                        foreach (var condition in conditionalNodeModel.ConditionModels)
                        {
                            hasAtleastOneReference |= RemoveBlackboardVariableLinksFromFields(blackboardAsset, condition.Fields);
                        }
                    }
                }
                
                // We dirty the asset because we did remove the blackboard reference.
                EditorUtility.SetDirty(authoringGraph);

                // If outstanding change was made, we put the graph aside for postprocessing.
                if (hasAtleastOneReference)
                {
                    graphToRebuild.Add(authoringGraph);
                }
            }

            if (graphToRebuild.Count == 0)
            {
                return;
            }

            BehaviorAssetPostProcessor.RequestGraphsRebuild(graphToRebuild);
        }

        private static bool RemoveBlackboardVariableLinksFromFields(BehaviorBlackboardAuthoringAsset blackboardAsset, IEnumerable<BehaviorGraphNodeModel.FieldModel> fields)
        {
            bool hasMatchingField = false;
            foreach (var field in fields)
            {
                if (blackboardAsset.Variables.Contains(field.LinkedVariable) || (field.Type.Type == typeof(BehaviorBlackboardAuthoringAsset) && (BehaviorBlackboardAuthoringAsset)field.LinkedVariable?.ObjectValue == blackboardAsset))
                {
                    field.LinkedVariable = null;
                    hasMatchingField = true;

                    // we don't break here, in case a user have a node that reference the same field several times.
                }
            }

            return hasMatchingField;
        }
    }
}