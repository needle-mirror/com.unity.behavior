using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    internal static class TypeMigrationUtility
    {
        /// <summary>
        /// Scan for missing types in GraphAsset and BlackboardAsset
        /// </summary>
        public static List<AssetMissingTypeInfo> ScanForMissingType()
        {
            var assetsWithMissingTypes = new List<AssetMissingTypeInfo>();
            var processedTypes = new Dictionary<string, MissingTypeData>();

            var graphAssetGuids = AssetDatabase.FindAssets("t:BehaviorAuthoringGraph");
            foreach (var guid in graphAssetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(assetPath) as BehaviorAuthoringGraph;
                var info = ScanForMissingTypes(asset);
                if (info != null)
                {
                    assetsWithMissingTypes.AddRange(info);
                }
            }

            var blackboardAssetGuids = AssetDatabase.FindAssets("t:BehaviorBlackboardAuthoringAsset");
            foreach (var guid in graphAssetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BehaviorBlackboardAuthoringAsset>(assetPath) as BehaviorBlackboardAuthoringAsset;
                var info = ScanForMissingTypes(asset);
                if (info != null)
                {
                    assetsWithMissingTypes.Add(info);
                }
            }

            return assetsWithMissingTypes;
        }

        /// <summary>
        /// Scan given graph and returns list of missing type info if any.
        /// </summary>
        public static List<AssetMissingTypeInfo> ScanForMissingTypes(BehaviorAuthoringGraph graph, Dictionary<string, MissingTypeData> processedTypes = null)
        {
            if (graph == null)
            {
                return null;
            }

            var assetsWithMissingTypes = new List<AssetMissingTypeInfo>();
            void AddMissingTypeInfo(List<AssetMissingTypeInfo> assetsWithMissingTypes, AssetMissingTypeInfo assetInfo)
            {
                if (assetInfo != null && assetInfo.MissingTypes.Count > 0)
                {
                    assetsWithMissingTypes.Add(assetInfo);
                }
            }

            if (processedTypes == null)
            {
                processedTypes = new();
            }

            // Scan GraphAsset, RuntimeGraph and BlackboardReference
            var asset = graph;
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (asset is BehaviorAuthoringGraph graphAsset && graphAsset.ContainsInvalidSerializedReferences())
            {
                // Looking for linked model type with invalid type.
                var assetInfo = ProcessAssetMissingTypes(assetPath, graphAsset, processedTypes);
                AddMissingTypeInfo(assetsWithMissingTypes, assetInfo);

                // Looking for BBV with missing type.
                assetInfo = ProcessAssetMissingTypes(assetPath, graphAsset.RuntimeGraph, processedTypes);
                AddMissingTypeInfo(assetsWithMissingTypes, assetInfo);

                foreach (var subgraphInfo in graphAsset.SubgraphsInfo)
                {
                    var assetInfoList = ScanForMissingTypes(subgraphInfo.Asset, processedTypes);
                    if (assetInfoList != null)
                    {
                        foreach (var newAssetInfo in assetInfoList)
                        {
                            AddMissingTypeInfo(assetsWithMissingTypes, newAssetInfo);
                        }
                    }
                }

                foreach (var blackboardRef in graphAsset.BlackboardReferences)
                {
                    assetInfo = ScanForMissingTypes(blackboardRef, processedTypes);
                    AddMissingTypeInfo(assetsWithMissingTypes, assetInfo);
                }
            }

            // Scan BlackboardAsset
            var runtimeBlackboardAsset = AssetDatabase.LoadAssetAtPath<RuntimeBlackboardAsset>(assetPath);

            if (runtimeBlackboardAsset != null && SerializationUtility.HasManagedReferencesWithMissingTypes(runtimeBlackboardAsset))
            {
                // Looking for BBV with missing type.
                var assetInfo = ProcessAssetMissingTypes(assetPath, runtimeBlackboardAsset, processedTypes);
                AddMissingTypeInfo(assetsWithMissingTypes, assetInfo);
            }

            return assetsWithMissingTypes;
        }

        /// <summary>
        /// Scan given blackboard and returns list of missing type info if any.
        /// </summary>
        public static AssetMissingTypeInfo ScanForMissingTypes(BehaviorBlackboardAuthoringAsset blackboard, Dictionary<string, MissingTypeData> processedTypes = null)
        {
            if (blackboard == null)
            {
                return null;
            }

            AssetMissingTypeInfo assetsWithMissingTypes = null;
            if (processedTypes == null)
            {
                processedTypes = new Dictionary<string, MissingTypeData>();
            }

            string assetPath = AssetDatabase.GetAssetPath(blackboard);
            if (blackboard.ContainsInvalidSerializedReferences())
            {
                assetsWithMissingTypes = ProcessAssetMissingTypes(assetPath, blackboard, processedTypes);
            }

            return assetsWithMissingTypes;
        }

        private static AssetMissingTypeInfo ProcessAssetMissingTypes(string assetPath, ScriptableObject asset, Dictionary<string, MissingTypeData> processedTypes)
        {
            if (asset == null)
            {
                return null;
            }

            var assetInfo = new AssetMissingTypeInfo(assetPath, asset);
            var missingTypes = SerializationUtility.GetManagedReferencesWithMissingTypes(asset);

            foreach (var missingType in missingTypes)
            {
                string actualClassName = missingType.className;
                string actualNamespace = missingType.namespaceName;
                string actualAssembly = missingType.assemblyName;
                string originalContext = null;

                // Handle generic types like TypedVariableModel`1[[TestMono, Assembly-CSharp]]
                if (actualClassName.Contains("`") && actualClassName.Contains("[["))
                {
                    // Store the original generic context for reference
                    originalContext = actualClassName;

                    var genericMatch = System.Text.RegularExpressions.Regex.Match(
                        actualClassName,
                        @"([^`]+)`\d+\[\[([^,]+),\s*([^\]]+)\]\]"
                    );

                    if (genericMatch.Success)
                    {
                        // Store the generic container pattern for reference
                        string genericContainer = genericMatch.Groups[1].Value;
                        originalContext = $"{genericContainer}[[{{0}}]]";

                        // Extract the inner type information
                        actualClassName = genericMatch.Groups[2].Value;
                        actualAssembly = genericMatch.Groups[3].Value;

                        // Check if the inner type has a namespace (NameSpace.ClassName format)
                        var namespaceMatch = System.Text.RegularExpressions.Regex.Match(actualClassName, @"^(.+)\.([^.]+)$");
                        if (namespaceMatch.Success)
                        {
                            actualNamespace = namespaceMatch.Groups[1].Value;
                            actualClassName = namespaceMatch.Groups[2].Value;
                        }
                        else
                        {
                            actualNamespace = ""; // No namespace in the inner type
                        }
                    }
                }

                string fullTypeName = string.IsNullOrEmpty(actualNamespace) ?
                    $"{actualClassName}, {actualAssembly}" :
                    $"{actualNamespace}.{actualClassName}, {actualAssembly}";

                if (!processedTypes.ContainsKey(fullTypeName))
                {
                    processedTypes[fullTypeName] = new MissingTypeData(
                        actualClassName,
                        actualNamespace,
                        actualAssembly,
                        originalContext
                    );
                }

                var typeData = processedTypes[fullTypeName];
                if (!typeData.AffectedAssetPaths.Contains(assetPath))
                {
                    typeData.AffectedAssetPaths.Add(assetPath);
                }

                if (!assetInfo.MissingTypes.Contains(typeData))
                {
                    assetInfo.MissingTypes.Add(typeData);
                }
            }

            return assetInfo;
        }
    }
}
