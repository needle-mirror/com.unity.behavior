using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    internal class GraphAssetSubgraphUpdater : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (paths.Length == 0)
            {
                return paths;
            }
            List<BehaviorAuthoringGraph> s_ChangedGraphAssets = new();
            List<(BehaviorAuthoringGraph, string)> s_AssetsToRebuild = new();
            
            // Retrieve all authoring graphs being saved.
            s_ChangedGraphAssets.Clear();
            s_ChangedGraphAssets.AddRange(paths
                .Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(BehaviorAuthoringGraph))
                .Select(path => AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path))
            // We could improve even further by checking HasOutstandingChange to ignore rebuilding for subgraph with cosmetic change.
            );

            if (s_ChangedGraphAssets.Count == 0)
            {
                return paths;
            }

            // Get all assets that contain references to the assets being saved.
            s_AssetsToRebuild.Clear();
            foreach (BehaviorAuthoringGraph asset in BehaviorGraphAssetRegistry.GlobalRegistry.Assets)
            {
                foreach (BehaviorAuthoringGraph changedGraph in s_ChangedGraphAssets)
                {
                    bool containRef = asset.ContainsReferenceTo(changedGraph);

                    if (!ReferenceEquals(asset, changedGraph) && containRef)
                    {
                        s_AssetsToRebuild.Add((asset, changedGraph.name));
                        break;
                    }
                }
            }

            if (s_AssetsToRebuild.Count == 0)
            {
                return paths;
            }

            // Rebuild all assets that reference the changed assets.
            foreach ((BehaviorAuthoringGraph referencingGraph, string subgraphName) in s_AssetsToRebuild)
            {
                // Debug.Log($"Behavior: Graph \"{subgraphName}\" updated. Rebuilding referencing graph \"{referencingGraph.name}\".");
                referencingGraph.BuildRuntimeGraph();
            }

            // We cannot save the rebuilt graphs right away as the target subgraph asset is still not saved yet.
            // Instead we wait post asset re-import to batch save all the modified assets.
            // DO NOT use AssetDatabase.SaveAssetIfDirty in the loop. It works, but it is NOT scalable on performance (~300ms per asset).
            BehaviorAssetPostProcessor.RequestSave();

            return paths;
        }
    }
}