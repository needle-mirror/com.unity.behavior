using System.Collections.Generic;
using UnityEditor;

namespace Unity.Behavior
{
    /// <summary>
    /// PostProcess Utility class to rebuild and save graphs that can be dirty by blackboard assets modification.
    /// </summary>
    internal class BehaviorAssetPostProcessor : AssetPostprocessor
    {
        private static bool s_PostProcessSaveRequested = false;

        private static HashSet<BehaviorAuthoringGraph> s_GraphsToRebuild = new HashSet<BehaviorAuthoringGraph>();

        /// <summary>
        /// Use this function when you want to save changes in batch post asset modification.
        /// This is necessary in situation such as OnWillAssetSaves to prevent recursive call, 
        /// or avoid the cost of AssetDatabase.SaveAssetIfDirty.
        /// </summary>
        public static void RequestSave()
        {
            s_PostProcessSaveRequested = true;
        }

        /// <summary>
        /// Provided graphs will be rebuild and save on asset post process.
        /// </summary>
        /// <param name="graphs">Collection of graphs to rebuild.</param>
        public static void RequestGraphsRebuild(IEnumerable<BehaviorAuthoringGraph> graphs)
        {
            s_GraphsToRebuild.UnionWith(graphs);
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (s_GraphsToRebuild.Count > 0)
            {
                foreach (var referencingGraph in s_GraphsToRebuild)
                {
                    // Debug.Log($"BehaviorAssetPostProcessor: Rebuilding referencing graph \"{referencingGraph.name}\".");
                    referencingGraph.BuildRuntimeGraph();

                    // GraphAsset.HasOutstandingChanges property is only set to false when the GraphAsset.SaveAsset is called.
                    // Here we are doing all the work manually, or the asset will be rebuild everytime until GraphAsset.SaveAsset is called.
                    referencingGraph.ResetOutstandingChange();
                }

                s_PostProcessSaveRequested = true;
                s_GraphsToRebuild.Clear();
            }

            if (!s_PostProcessSaveRequested)
            {
                return;
            }

            // Debug.Log("BehaviorAssetPostProcessor: Saving now");
            s_PostProcessSaveRequested = false;
            AssetDatabase.SaveAssets();
        }
    }
}