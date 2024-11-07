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

            foreach (BehaviorAuthoringGraph authoringGraph in BehaviorGraphAssetRegistry.GlobalRegistry.Assets)
            {
                if (!authoringGraph.ContainsReferenceTo(blackboardAsset))
                {
                    continue;
                }

                Debug.LogWarning($"Graph {authoringGraph.name} has a reference to deleted {blackboardAsset.name}. References will be removed and the graph might need updating to function correctly.", authoringGraph);
                authoringGraph.m_Blackboards.Remove(blackboardAsset);
                foreach (var nodeModel in authoringGraph.Nodes)
                {
                    if (nodeModel is not BehaviorGraphNodeModel behaviorNodeModel)
                    {
                        continue;
                    }
                    RemoveBlackboardVariableLinksFromFields(blackboardAsset, behaviorNodeModel.Fields);
                            
                    if (behaviorNodeModel is IConditionalNodeModel conditionalNodeModel)
                    {
                        // Delete variable references from conditions.
                        foreach (var condition in conditionalNodeModel.ConditionModels)
                        {
                            RemoveBlackboardVariableLinksFromFields(blackboardAsset, condition.Fields);
                        }
                    }
                }
                EditorUtility.SetDirty(authoringGraph);
            }
        }

        private static void RemoveBlackboardVariableLinksFromFields(BehaviorBlackboardAuthoringAsset blackboardAsset, IEnumerable<BehaviorGraphNodeModel.FieldModel> fields)
        {
            foreach (var field in fields)
            {
                if (blackboardAsset.Variables.Contains(field.LinkedVariable) || (field.Type.Type == typeof(BehaviorBlackboardAuthoringAsset) && (BehaviorBlackboardAuthoringAsset)field.LinkedVariable?.ObjectValue == blackboardAsset))
                {
                    field.LinkedVariable = null;
                }
            }
        }
    }
}