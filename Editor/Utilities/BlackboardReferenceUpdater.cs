using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Unity.Behavior
{
    internal class BlackboardReferenceUpdater : AssetModificationProcessor
    {
        private static List<BehaviorBlackboardAuthoringAsset> s_ChangedBlackboardAssets = new();
        private static HashSet<BehaviorAuthoringGraph> s_AssetsToRebuild = new();

        private static string[] OnWillSaveAssets(string[] paths)
        {
            // Early-out if no asset to rebuild.
            if (!TryGatherLinkedGraphs(paths))
            {
                return paths;
            }

            // Rebuild all assets that reference the changed assets.
            foreach (var referencingGraph in s_AssetsToRebuild)
            {
                // Debug.Log($"Blackboard: \"{blackboardName}\" updated. Rebuilding referencing graph \"{referencingGraph.name}\".");
                referencingGraph.BuildRuntimeGraph();
            }

            // We cannot save the rebuilt graphs right away as the target Blackboard assets are still not saved yet.
            // Instead we wait post asset re-import to batch save all the modified assets.
            // DO NOT use AssetDatabase.SaveAssetIfDirty in the loop. It works, but it is NOT scalable on performance (~300ms per asset).
            BehaviorAssetPostProcessor.RequestSave();

            return paths;
        }

        // Return false if no blackboard asset is saved or if it is not linked to a graph asset. True if found at least a graph to rebuild.</returns>
        private static bool TryGatherLinkedGraphs(string[] paths)
        {
            if (paths.Length == 0)
                return false;

            // Retrieve all authoring graphs being saved.
            s_ChangedBlackboardAssets.Clear();
            s_ChangedBlackboardAssets.AddRange(paths
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(BehaviorBlackboardAuthoringAsset))
                .Select(path => AssetDatabase.LoadAssetAtPath<BehaviorBlackboardAuthoringAsset>(path)));

            if (s_ChangedBlackboardAssets.Count == 0)
            {
                return false;
            }

            // Get all assets that contain references to the assets being saved.
            s_AssetsToRebuild.Clear();
            foreach (BehaviorAuthoringGraph authoringGraph in BehaviorGraphAssetRegistry.GlobalRegistry.Assets)
            {
                foreach (BehaviorBlackboardAuthoringAsset blackboardAsset in s_ChangedBlackboardAssets)
                {
                    if (authoringGraph.ContainsReferenceTo(blackboardAsset))
                    {
                        s_AssetsToRebuild.Add(authoringGraph);
                        break;
                    }
                }
            }

            if (s_AssetsToRebuild.Count == 0)
            {
                return false;
            }

            return true;
        }
    }
}