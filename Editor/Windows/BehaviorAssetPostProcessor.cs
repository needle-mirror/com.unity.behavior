using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// PostProcess Utility class to rebuild and save graphs that can be dirty by blackboard assets modification.
    /// </summary>
    internal class BehaviorAssetPostProcessor : AssetPostprocessor
    {
        private const string k_PlaymodeUndoRedoEditWarning = "You've performed an undo/redo operation on a Behavior Graph during Play mode.\n" +
                        "\n- Running agents in the scene will continue using their existing graph instances" +
                        "\n- The graph asset has been updated, but won't affect running agents" +
                        "\n- Debug visualization may show inconsistent or incorrect information" +
                        "\nTo apply these changes to running agents, use 'Reinitialize And Restart Graph' " +
                        "from the agent's context menu.";

        private const string k_UndoScheduledBoolName = "BehaviorAsset_UndoRedoScheduled";

        private static HashSet<BehaviorAuthoringGraph> s_GraphsToRebuild = new ();
        private static bool s_HasRebuildAssets = false;
        /// <summary>
        /// Set to true once user accepts undo redo during playmode. Last for a unique playmode session.
        /// More granular than "Don't Show Again This Session" which is lasting for the editor session.
        /// </summary>
        private static bool s_PlayModeUndoRedoGranted = false;

        [InitializeOnLoadMethod()]
        private static void RegisterUndoListener()
        {
            Undo.undoRedoEvent += OnUndoRedoEvent;
        }

        [RuntimeInitializeOnLoadMethod()]
        private static void ResetUndoRedoPermission()
        {
            s_PlayModeUndoRedoGranted = false;
        }

        // This is the only place we can retrieve the undo message to check for outstanding flag.
        // Allows to rebuild graph asset post undo-redo even when no GraphEditor is opened.
        private static void OnUndoRedoEvent(in UnityEditor.UndoRedoInfo undo)
        {
#if BEHAVIOR_DEBUG_UNDO_REDO
            UnityEngine.Debug.Log($"BehaviorAssetPostProcessor.OnUndoRedoEvent: \"{undo.undoName}\"");
#endif
            var entries = undo.undoName.Split(new[] { "(", ")" }, System.StringSplitOptions.RemoveEmptyEntries);
            if (entries.Length == 0)
            {
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetBool(k_UndoScheduledBoolName, false);
                }

                return;
            }

            string assetPath = entries.Where(x => x.Contains(".asset")).FirstOrDefault();
            if (string.IsNullOrEmpty(assetPath))
            {
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetBool(k_UndoScheduledBoolName, false);
                }

                return;
            }

            var graphAsset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(BehaviorAuthoringGraph)) as BehaviorAuthoringGraph;
            if (graphAsset != null)
            {
                bool isOutstandingChange = entries.Contains("outstanding");

                if (EditorUtility.IsDirty(graphAsset.Blackboard))
                {
                    isOutstandingChange = true;
                    graphAsset.Blackboard.SetAssetDirty();
                    var graphBlackboard = graphAsset.Blackboard as BehaviorBlackboardAuthoringAsset;
                    if (graphBlackboard != null)
                    {
                        graphBlackboard.BuildRuntimeBlackboard();
                        AssetDatabase.SaveAssetIfDirty(graphBlackboard);
                    }
                }

                graphAsset.SetAssetDirty(isOutstandingChange);
                if (isOutstandingChange)
                {
                    s_GraphsToRebuild.Add(graphAsset);
                }
                AssetDatabase.SaveAssetIfDirty(graphAsset);

                if (EditorApplication.isPlaying)
                {
                    HandlePlayModeUndoRedo(undo);
                }

                return;
            }

            var blackboardAsset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(BehaviorBlackboardAuthoringAsset)) as BehaviorBlackboardAuthoringAsset;
            if (blackboardAsset != null)
            {
                blackboardAsset.BuildRuntimeBlackboard();
                AssetDatabase.SaveAssetIfDirty(blackboardAsset);

                if (EditorApplication.isPlaying)
                {
                    HandlePlayModeUndoRedo(undo);
                }
            }
        }

        private static void HandlePlayModeUndoRedo(in UndoRedoInfo undo)
        {
            if (s_PlayModeUndoRedoGranted == true)
            {
                return;
            }

            if (SessionState.GetBool(k_UndoScheduledBoolName, false) == false)
            {
                if (!EditorUtility.DisplayDialog(
                        "Behavior Graph Editing During Play Mode (Undo/Redo)",
                        k_PlaymodeUndoRedoEditWarning, "OK",
                        DialogOptOutDecisionType.ForThisSession, "Don't Show Again This Session"))
                {
                    SessionState.SetBool(k_UndoScheduledBoolName, true);

                    // If cancelled, undo or redo depending on the current action.
                    if (undo.isRedo)
                    {
                        Undo.PerformUndo();
                    }
                    else
                    {
                        Undo.PerformRedo();
                    }
                }
                else
                {
                    s_PlayModeUndoRedoGranted = true;
                }
            }
            else
            {
                SessionState.SetBool(k_UndoScheduledBoolName, false);
            }
        }

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
                // GraphAsset.HasOutstandingChanges property is only set to false when the GraphAsset.SaveAsset is called.
                // We call a custom method to rebuild graph and embedded blackboard and we will save all the asset afterward.
                // This is an optimization because SaveAssetIfDirty doesn't scale when working with lots of graph dependencies (subgraphs and blackboard assets).
                referencingGraph.RebuildGraphAndBlackboardRuntimeData();

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