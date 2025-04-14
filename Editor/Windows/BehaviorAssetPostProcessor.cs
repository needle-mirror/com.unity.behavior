using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Unity.Behavior
{
    /// <summary>
    /// PostProcess Utility class to rebuild and save graphs that can be dirty by blackboard assets modification.
    /// </summary>
    internal class BehaviorAssetPostProcessor : AssetPostprocessor
    {
        private static HashSet<BehaviorAuthoringGraph> s_GraphsToRebuild = new ();
        private static bool s_HasRebuildAssets = false;

        /// <summary>
        /// Provided graphs will be rebuild and save on asset post process.
        /// </summary>
        /// <param name="graphs">Collection of graphs to rebuild.</param>
        public static void RequestGraphsRebuild(IEnumerable<BehaviorAuthoringGraph> graphs)
        {
            s_GraphsToRebuild.UnionWith(graphs);
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Prevent infinite rebuild loops caused by this callback.
            if (s_HasRebuildAssets)
            {
                // Trigger validation for any graphs that have been rebuilt.
                foreach (var referencingGraph in s_GraphsToRebuild)
                {
                    referencingGraph.OnValidate();
                }

                s_HasRebuildAssets = false; 
                s_GraphsToRebuild.Clear();

                // Save after validation to ensure parent graphs can access the latest version of subgraphs.
                // This is critical when rebuilding with a clean library, as Unity saves assets in bulk and in a fuzzy order.
                // During such bulk saves, parent graphs may fail to access updated subgraphs until all assets are written to disk.
                // Recursive calls are not problematic here, as they ensure all assets are eventually up to date.
                AssetDatabase.SaveAssets();
                return;
            }

            // Identify graph or blackboard assets that have been saved or reimported.
            GatherAssetsToRebuild(importedAssets, ref s_GraphsToRebuild);

            if (s_GraphsToRebuild.Count == 0)
            {
                return;
            }

            foreach (var referencingGraph in s_GraphsToRebuild)
            {
                referencingGraph.BuildRuntimeGraph();

                // GraphAsset.HasOutstandingChanges property is only set to false when the GraphAsset.SaveAsset is called.
                // Here we are doing all the work manually, or the asset will be rebuild everytime until GraphAsset.SaveAsset is called.
                referencingGraph.ResetOutstandingChange();

                // WARNING: DO NOT use `AssetDatabase.SaveAssetIfDirty` in the loop.
                // It works, but it is NOT scalable on performance (~300ms per asset).
            }

            s_HasRebuildAssets = true;

            // The save is going to cause a recurse asset import.
            AssetDatabase.SaveAssets();
        }

        public static bool GatherAssetsToRebuild(IReadOnlyList<string> paths, ref HashSet<BehaviorAuthoringGraph> assetsToRebuild)
        {
            if (paths.Count == 0)
            {
                return false;
            }
            
            // Retrieve any blackboards.
            var changedBlackboardAssets = new List<BehaviorBlackboardAuthoringAsset>(paths
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(BehaviorBlackboardAuthoringAsset))
                .Select(path => AssetDatabase.LoadAssetAtPath<BehaviorBlackboardAuthoringAsset>(path)));

            // Retrieve graphs that had outstanding change or was reverted.
            var changedGraphAssets = new List<BehaviorAuthoringGraph>(paths
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(BehaviorAuthoringGraph))
                .Select(path => AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path)));

            if (changedBlackboardAssets.Count == 0 && changedGraphAssets.Count == 0)
            {
                return false;
            }

            int newAssetFound = 0;
            // Get all assets that contain references to the assets being saved.
            foreach (BehaviorAuthoringGraph asset in BehaviorGraphAssetRegistry.GlobalRegistry.Assets)
            {
                // Skip check if we are already planning on rebuilding this asset.
                if (assetsToRebuild.Contains(asset))
                {
                    continue;
                }

                foreach (BehaviorBlackboardAuthoringAsset changedBlackboard in changedBlackboardAssets)
                {
                    if (asset.ContainsReferenceTo(changedBlackboard))
                    {
                        ++newAssetFound;
                        assetsToRebuild.Add(asset);
                        break;
                    }
                }

                foreach (BehaviorAuthoringGraph changedGraph in changedGraphAssets)
                {
                    if (ReferenceEquals(asset, changedGraph))
                    {
                        continue;
                    }

                    // Scenario 1: Subgraph is changed and parent need rebuild -> Parent need rebuild
                    if (asset.ContainsStaticSubgraphReferenceTo(changedGraph) && !asset.IsDependencyUpToDate(changedGraph))
                    {
#if BEHAVIOR_DEBUG_ASSET_IMPORT
                        UnityEngine.Debug.Log($"Subgraph {changedGraph.name} was changed and the dependent graph '{asset.name}' need to be updated", changedGraph);
#endif
                        ++newAssetFound;
                        asset.AddOrUpdateDependency(changedGraph);
                        assetsToRebuild.Add(asset);
                    }

                    // Scenario 2: Parent is changed and have mistmatching subgraph version -> Parent need rebuild
                    // When an asset is imported, it is going to call OnValidate before OnPostprocessAllAssets.
                    // Because of this, we have to update all the timestamp related dependency from this place to normalize the behavior.
                    if (changedGraph.ContainsStaticSubgraphReferenceTo(asset) && !changedGraph.IsDependencyUpToDate(asset))
                    {
                        ++newAssetFound;
                        changedGraph.AddOrUpdateDependency(asset);
                        assetsToRebuild.Add(changedGraph);
                    }
                }
            }

            return newAssetFound > 0;
        }
    }
}