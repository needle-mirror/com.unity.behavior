using UnityEngine;
using UnityEditor;

namespace Unity.Behavior
{
    internal static class BehaviorAuthoringGraphUtilities
    {
        [MenuItem("Tools/Behavior/Validate All Graphs")]
        public static void ValidateAllGraphs()
        {
            // Find all assets of type BehaviorAuthoringGraph
            string[] guids = AssetDatabase.FindAssets("t:BehaviorAuthoringGraph");

            if (guids.Length == 0)
            {
                Debug.Log("No BehaviorAuthoringGraph assets found in the project.");
                return;
            }

            Debug.Log($"Validating {guids.Length} BehaviorAuthoringGraph asset(s)...");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);

                if (graph != null)
                {
                    graph.OnValidate(); // Assets will dirty themselves when needed.
                    Debug.Log($"Validated: {path}", graph);
                }
            }

            AssetDatabase.SaveAssets(); // Save changes if any were made
            Debug.Log("Validation complete.");
        }
    }
}