using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Unity.Behavior
{
    internal class AssetValidationBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // No need for the error message as BehaviorGraph assets will throw an exception if they have issues.
            BehaviorAssetValidationUtility.ValidateBlackboardManagedReferences(out var _);

            if (!BehaviorAssetValidationUtility.ValidateGraphManagedReferences(out var errorMessage))
            {
                // Check if the user wants to ignore missing managed reference types
                if (BehaviorProjectSettings.instance.IgnoreMissingManagedReferencesInBuild)
                {
                    Debug.LogWarning(string.Format(ErrorMessages.k_SerializedReferenceBuildWarning, errorMessage));
                    Debug.LogWarning(ErrorMessages.k_SerializedReferenceBuildSkip);
                }
                else
                {
                    Debug.LogError(ErrorMessages.k_SerializedReferenceBuildError);
                    throw new BuildFailedException(errorMessage);
                }
            }
        }
    }
}
