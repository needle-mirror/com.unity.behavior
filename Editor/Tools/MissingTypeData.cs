using System.Collections.Generic;
using UnityEngine;

namespace Unity.Behavior
{
    internal class MissingTypeData
    {
        public string ClassName { get; private set; }
        public string NamespaceName { get; private set; }
        public string AssemblyName { get; private set; }
        public string FullTypeName { get; private set; }
        public List<string> AffectedAssetPaths { get; private set; } = new();

        // Store original generic context for reference (i.e. "TypedVariableModel`1[[{0}]]").
        public string OriginalGenericContext;

        public bool WasExtractedFromGeneric;

        public MissingTypeData(string className, string namespaceName, string assemblyName, string originalContext = null)
        {
            ClassName = className;
            NamespaceName = namespaceName;
            AssemblyName = assemblyName;
            FullTypeName = string.IsNullOrEmpty(namespaceName) ?
                $"{className}, {assemblyName}" :
                $"{namespaceName}.{className}, {assemblyName}";
            OriginalGenericContext = originalContext;
            WasExtractedFromGeneric = !string.IsNullOrEmpty(originalContext);
        }
    }

    internal class AssetMissingTypeInfo
    {
        public string AssetPath { get; private set; }
        public ScriptableObject Asset { get; private set; }
        public List<MissingTypeData> MissingTypes { get; private set; } = new();

        public AssetMissingTypeInfo(string assetPath, ScriptableObject asset)
        {
            AssetPath = assetPath;
            Asset = asset;
        }
    }
}
