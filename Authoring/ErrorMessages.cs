using UnityEngine;
using System.Collections.Generic;

namespace Unity.Behavior
{
    internal class ErrorMessages
    {
        // Used by testing suite to disabled blocking process.
        public static bool DialogMessageEnabled { get; set; } = true;

        public const string k_TroubleshootingLink = "https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/serialize-ref-troubleshoot.html";

        // Serialize Reference messages
        public const string k_MissingTypeInAssetHelpboxError =
            "This asset cannot be edited or used at runtime. It contains missing types in managed reference." +
            "\nSee console for more details.";

        public const string k_PlaceholderInGraphAssetHelpboxWarning =
            "This asset contains placeholder nodes that will be skipped. This happens when node types are missing. " +
            "Replace placeholder nodes in the graph or recover the missing type." +
            "\nSee console for more details.";

        public const string k_MissingTypeInGraphAssetMessageError =
            "'{0}' contains {1} missing types in managed references. Asset cannot be edited or used at runtime." +
            "\nConsult documentation for guidance: " + k_TroubleshootingLink +
            "\nMissing type(s): {2}";

        public const string k_PlaceholderInGraphAssetMessageWarning =
            "'{0}' contains {1} placeholder node(s). Placeholder nodes will be excluded from runtime execution." +
            "\nConsult documentation for guidance: " + k_TroubleshootingLink +
            "\nPlaceholder node(s): {2}";

        public const string k_SerializedRefenceWindowError =
            "The Behavior asset '{0}' has missing types in its managed references. " +
            "Inspect the asset for more information.";

        public const string k_SerializedReferenceBuildWarning =
            "Missing managed reference types detected in behavior assets: {0}";

        public const string k_SerializedReferenceBuildSkip =
            "Build proceeding because 'Ignore Missing Managed References in Build' is enabled. " +
            "To make these errors block the build, disable this setting in Project Settings > Behavior > Asset Settings.";

        public const string k_SerializedReferenceBuildError =
            "Build cancelled: Behavior asset validation error. " +
            "To prevent the validation from blocking the build, enable 'Ignore Missing Managed References in Build' " +
            "in Project Settings > Behavior > Asset Settings.";

        private const string k_SerializedReferenceWindowClosingTitle =
            "Invalid Asset Detected";

        private const string k_SerializedReferenceWindowClosingDescription =
            "The asset '{0}' contains missing types. Window will close to avoid corruption.";

        public static string GetValidationErrorMessage<T>(IReadOnlyList<string> invalidAssets)
        {
            return $"Found {invalidAssets.Count} {typeof(T).Name} asset(s) with missing serialized reference types:\n" +
                                     string.Join("\n", invalidAssets);
        }

        public static void LogSerializedReferenceWindowError(ScriptableObject asset)
        {
            var name = asset == null ? "Unknown" : asset.name;
            Debug.LogError(string.Format(ErrorMessages.k_SerializedRefenceWindowError, name), asset);
        }

        public static void DisplayWindowClosingDialog(ScriptableObject asset)
        {
#if UNITY_EDITOR
            if (DialogMessageEnabled) // Dialog needs to be disable for tests.
            {
                var name = asset == null ? "Unknown" : asset.name;
                UnityEditor.EditorUtility.DisplayDialog(k_SerializedReferenceWindowClosingTitle,
                            string.Format(k_SerializedReferenceWindowClosingDescription, name),
                            "Close");
            }
#endif
        }
    }
}
