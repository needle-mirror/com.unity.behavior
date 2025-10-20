using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    internal static class BehaviorAssetValidationUtility
    {
        [MenuItem("Tools/Behavior/Validate All Graphs", priority = 0)]
        public static void ValidateAllGraphs()
        {
            // Find all assets of type BehaviorAuthoringGraph
            string[] guids = AssetDatabase.FindAssets("t:BehaviorAuthoringGraph");

            if (guids.Length == 0)
            {
                Debug.Log("No BehaviorGraph assets found in the project.");
                return;
            }

            Debug.Log($"Validating {guids.Length} BehaviorGraph asset(s)...");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
                AssetDatabase.SetMainObject(graph, path); // Ensures main object properly set.
                EditorUtility.SetDirty(graph);
            }

            AssetDatabase.SaveAssets();
            // Reimport all assets and let post-processor handle validation if needed
            Debug.Log("Validation completed.");
        }

        [MenuItem("Tools/Behavior/Check Assets Integrity", priority = 1)]
        public static void LogAssetsWithMissingTypes()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(BehaviorAuthoringGraph)}");

            if (guids.Length > 0)
            {
                Debug.Log($"Checking {guids.Length} BehaviorGraph asset(s)...");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
                    graph.ValidateAsset();
                    if (graph.HasPlaceholderNode())
                    {
                        Debug.LogWarning($"BehaviorGraph asset has placeholder nodes: {path}.", graph);
                    }
                }
            }


            guids = AssetDatabase.FindAssets($"t:{nameof(BehaviorBlackboardAuthoringAsset)}");
            if (guids.Length > 0)
            {
                Debug.Log($"Checking {guids.Length} Blackboard asset(s)...");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var blackboard = AssetDatabase.LoadAssetAtPath<BehaviorBlackboardAuthoringAsset>(path);
                    if (blackboard != null && AssetDatabase.IsMainAsset(blackboard)
                        && blackboard.ContainsInvalidSerializedReferences())
                    {
                        AssetLogger.LogAssetManagedReferenceError(blackboard);
                    }
                }
            }

            Debug.Log("Check completed.");
        }

        /// <summary>
        /// Validates all BehaviorGraph assets for managed references with missing types.
        /// </summary>
        /// <returns>False if at least one asset was found with a missing tyoe.</returns>
        public static bool ValidateGraphManagedReferences(out string errorMessage)
            => ValidateBehaviorAssetManagedReference<BehaviorGraph>(out errorMessage);

        /// <summary>
        /// Validates all BlackboardAssets for managed references with missing types.
        /// </summary>
        /// <returns>False if at least one asset was found with a missing tyoe.</returns>
        public static bool ValidateBlackboardManagedReferences(out string errorMessage)
            => ValidateBehaviorAssetManagedReference<BlackboardAsset>(out errorMessage);

        private static bool ValidateBehaviorAssetManagedReference<T>(out string errorMessage)
            where T : ScriptableObject
        {
            errorMessage = string.Empty;

            var invalidAssets = new List<string>();
            var grapAssets = FindAssetsOfType<T>();

            foreach (var asset in grapAssets)
            {
                if (SerializationUtility.HasManagedReferencesWithMissingTypes(asset))
                {
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    invalidAssets.Add(assetPath);
                    Debug.LogWarning($"Asset has missing serialized reference types: {assetPath}.", asset);
                }
            }

            if (invalidAssets.Count > 0)
            {
                errorMessage = ErrorMessages.GetValidationErrorMessage<T>(invalidAssets);
                return false;
            }

            return true;
        }

        private static T[] FindAssetsOfType<T>() where T : UnityEngine.ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            return guids.Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                       .Where(asset => asset != null)
                       .ToArray();
        }
    }
}
