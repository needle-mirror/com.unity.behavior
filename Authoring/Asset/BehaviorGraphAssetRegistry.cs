using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
#endif
using UnityEngine;
using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    /// <summary>
    /// A registry asset which contains references to <see cref="BehaviorAuthoringGraph"/> assets.
    /// </summary>
    [Serializable]
    internal class BehaviorGraphAssetRegistry : ScriptableObject, ISerializationCallbackReceiver
    {
        private const string k_GlobalRegistryPath = "Assets/GlobalAssetRegistry";

        /// <summary>
        /// Returns wether the global registry is in a valid state. When false, the registry is still being updated
        /// and trying to retrieve value from it might result in unexpected behavior.
        /// </summary>
        public static bool IsRegistryStateValid { get; private set; } = false;

        /// <summary>
        /// All <see cref="BehaviorAuthoringGraph"/> assets referenced by the registry instance.
        /// </summary>
        [SerializeField]
        public List<BehaviorAuthoringGraph> Assets = new();

        private Dictionary<SerializableGUID, BehaviorAuthoringGraph> m_GuidToAsset = new Dictionary<SerializableGUID, BehaviorAuthoringGraph>();
        private readonly Dictionary<string, HashSet<BehaviorAuthoringGraph>> m_EnumTypeToGraphAssets = new(StringComparer.Ordinal);

        private static BehaviorGraphAssetRegistry m_GlobalRegistry;
        private static readonly HashSet<string> s_MissingEnumTypeIdsWarned = new(StringComparer.Ordinal);
        private static List<string> s_LastEnumChangedGraphPaths = new();
        private static int s_LastEnumChangedGraphPathIndex = -1;

        /// <summary>
        /// An instance of a <see cref="BehaviorGraphAssetRegistry" /> which holds references to all known
        /// assets contained within the project.
        /// </summary>
        public static BehaviorGraphAssetRegistry GlobalRegistry
        {
            get
            {
                if (!m_GlobalRegistry)
                {
                    m_GlobalRegistry = ResourceLoadAPI.Load<BehaviorGraphAssetRegistry>(k_GlobalRegistryPath);
                }
                if (!m_GlobalRegistry)
                {
                    m_GlobalRegistry = CreateInstance<BehaviorGraphAssetRegistry>();
                }

                m_GlobalRegistry.PurgeNullAndDuplicateAssets();
                return m_GlobalRegistry;
            }
        }

        /// <inheritdoc cref="OnEnable"/>
        public void OnEnable()
        {
            if (m_GuidToAsset == null)
            {
                m_GuidToAsset = new Dictionary<SerializableGUID, BehaviorAuthoringGraph>();
            }
            m_GlobalRegistry = this;

            IsRegistryStateValid = false;
#if UNITY_EDITOR
            UpdateGlobalRegistry();
#endif
            IsRegistryStateValid = true;
        }

        /// <inheritdoc cref="OnDisable"/>
        public void OnDisable()
        {
            m_GlobalRegistry = null;
        }

        public static void Add(BehaviorAuthoringGraph asset)
        {
            BehaviorGraphAssetRegistry globalRegistry = GlobalRegistry;
            if (!globalRegistry.Assets.Contains(asset))
            {
                globalRegistry.Assets.Add(asset);
                try
                {
                    globalRegistry.m_GuidToAsset.Add(asset.AssetID, asset);
                }
                catch (ArgumentException)
                {
                    if (globalRegistry.m_GuidToAsset.TryGetValue(asset.AssetID, out BehaviorAuthoringGraph existingAsset))
                    {
                        Debug.Log($"Graph asset '{existingAsset.name}' with the ID {asset.AssetID} already exists.");
                    }
                }
            }
        }

        public static bool Remove(BehaviorAuthoringGraph asset)
        {
            BehaviorGraphAssetRegistry globalRegistry = GlobalRegistry;
            if (globalRegistry.Assets.Contains(asset))
            {
                globalRegistry.Assets.Remove(asset);
                globalRegistry.m_GuidToAsset.Remove(asset.AssetID);
                globalRegistry.RemoveGraphFromEnumDependencyIndex(asset);
                return true;
            }
            return false;
        }

        internal static void RegisterGraphEnumDependencies(BehaviorAuthoringGraph asset, IReadOnlyDictionary<string, string> enumDependencySignatures)
        {
            if (asset == null)
            {
                return;
            }

            BehaviorGraphAssetRegistry registry = GlobalRegistry;
            registry.RemoveGraphFromEnumDependencyIndex(asset);
            registry.AddGraphToEnumDependencyIndex(asset, enumDependencySignatures);
        }

#if UNITY_EDITOR
        public static BehaviorAuthoringGraph TryGetAssetFromGraphPath(BehaviorGraph graph)
        {
            string assetPath = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }
            AssetLogger.CanLogMissingTypeInManagedRefErrorMessage = false;
            var asset = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(assetPath);
            AssetLogger.CanLogMissingTypeInManagedRefErrorMessage = true;
            return asset;
        }
#endif

        public static bool TryGetAssetFromId(SerializableGUID id, out BehaviorAuthoringGraph asset)
        {
            return GlobalRegistry.m_GuidToAsset.TryGetValue(id, out asset);
        }

        public static BehaviorAuthoringGraph TryGetAssetFromGraphBlackboard(BehaviorBlackboardAuthoringAsset blackboard)
        {
            if (blackboard == null)
            {
                return null;
            }

            foreach (BehaviorAuthoringGraph asset in GlobalRegistry.Assets)
            {
                if (asset.Blackboard.AssetID == blackboard.AssetID)
                {
                    return asset;
                }
            }

            return null;
        }

        private void RemoveGraphFromEnumDependencyIndex(BehaviorAuthoringGraph graph)
        {
            if (graph == null || m_EnumTypeToGraphAssets.Count == 0)
            {
                return;
            }

            HashSet<string> keyToRemove = null;
            foreach (var kvp in m_EnumTypeToGraphAssets)
            {
                if (kvp.Value.Remove(graph) && kvp.Value.Count == 0)
                {
                    keyToRemove ??= new ();
                    keyToRemove.Add(kvp.Key);
                }
            }

            if (keyToRemove == null)
            {
                return;
            }

            foreach (string key in keyToRemove)
            {
                m_EnumTypeToGraphAssets.Remove(key);
            }
        }

        private void AddGraphToEnumDependencyIndex(BehaviorAuthoringGraph graph, IReadOnlyDictionary<string, string> enumDependencySignatures)
        {
            if (graph == null || enumDependencySignatures == null || enumDependencySignatures.Count == 0)
            {
                return;
            }

            foreach (string enumTypeId in enumDependencySignatures.Keys)
            {
                if (string.IsNullOrEmpty(enumTypeId))
                {
                    continue;
                }

                if (!m_EnumTypeToGraphAssets.TryGetValue(enumTypeId, out HashSet<BehaviorAuthoringGraph> graphSet))
                {
                    graphSet = new HashSet<BehaviorAuthoringGraph>();
                    m_EnumTypeToGraphAssets[enumTypeId] = graphSet;
                }

                graphSet.Add(graph);
            }
        }

        private void RebuildEnumDependencyIndex()
        {
            m_EnumTypeToGraphAssets.Clear();
            foreach (BehaviorAuthoringGraph graph in Assets)
            {
                if (graph == null)
                {
                    continue;
                }

                AddGraphToEnumDependencyIndex(graph, graph.EnumDependencySignatures);
            }
        }

        private void PurgeNullAndDuplicateAssets()
        {
            Assets.RemoveAll(asset => asset == null);
            Assets = new HashSet<BehaviorAuthoringGraph>(Assets).ToList();
            m_GuidToAsset.Clear();
            foreach (BehaviorAuthoringGraph asset in Assets)
            {
                try
                {
                    m_GuidToAsset.Add(asset.AssetID, asset);
                }
                catch (ArgumentException)
                {
                    if (m_GuidToAsset.TryGetValue(asset.AssetID, out BehaviorAuthoringGraph existingAsset))
                    {
                        Debug.Log($"Graph asset '{existingAsset.name}' with the ID {asset.AssetID} already exists.");
                    }
                }
            }

            RebuildEnumDependencyIndex();
        }

#if UNITY_EDITOR
        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            UpdateGlobalRegistry();
            DetectAndReimportGraphsForChangedEnums();
        }

        public static void UpdateGlobalRegistry()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(BehaviorAuthoringGraph)}");

            // No guids are returned during deserialization, so don't update the registry.
            if (guids.Length == 0)
            {
                return;
            }

            BehaviorGraphAssetRegistry globalRegistry = GlobalRegistry;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetLogger.CanLogMissingTypeInManagedRefErrorMessage = false;
                BehaviorAuthoringGraph asset = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
                AssetLogger.CanLogMissingTypeInManagedRefErrorMessage = true;
                if (asset != null && !globalRegistry.Assets.Contains(asset))
                {
                    globalRegistry.Assets.Add(asset);
                    globalRegistry.m_GuidToAsset.Add(asset.AssetID, asset);
                }
            }
            EditorUtility.SetDirty(globalRegistry);
        }

        internal static void DetectAndReimportGraphsForChangedEnums()
        {
            BehaviorGraphAssetRegistry registry = GlobalRegistry;
            registry.RebuildEnumDependencyIndex();
            if (registry.m_EnumTypeToGraphAssets.Count == 0)
            {
                return;
            }

            HashSet<BehaviorAuthoringGraph> graphsToReimport = new();
            List<string> changedEnumTypeIds = new();

            foreach (var kvp in registry.m_EnumTypeToGraphAssets)
            {
                string enumTypeId = kvp.Key;
                HashSet<BehaviorAuthoringGraph> graphs = kvp.Value;
                bool hasCurrentSignature = GraphAssetUtility.TryComputeEnumDependencySignatureFromTypeId(enumTypeId, out string currentSignature);
                if (!hasCurrentSignature)
                {
                    if (s_MissingEnumTypeIdsWarned.Add(enumTypeId))
                    {
                        Debug.LogWarning($"Skipping enum dependency reimport check for missing enum type '{enumTypeId}'.");
                    }
                    continue;
                }
                bool enumChanged = false;

                foreach (BehaviorAuthoringGraph graph in graphs)
                {
                    if (graph == null || graph.EnumDependencySignatures == null)
                    {
                        continue;
                    }

                    if (!graph.EnumDependencySignatures.TryGetValue(enumTypeId, out string previousSignature))
                    {
                        continue;
                    }

                    if (!string.Equals(previousSignature, currentSignature, StringComparison.Ordinal))
                    {
                        enumChanged = true;
                        graphsToReimport.Add(graph);
                    }
                }

                if (enumChanged)
                {
                    changedEnumTypeIds.Add(enumTypeId);
                }
            }

            if (graphsToReimport.Count == 0)
            {
                return;
            }

            List<string> graphPathsToReimport = graphsToReimport
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (graphPathsToReimport.Count == 0)
            {
                return;
            }

            s_LastEnumChangedGraphPaths = graphPathsToReimport
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            s_LastEnumChangedGraphPathIndex = -1;

            List<string> changedEnumDisplayNames = changedEnumTypeIds
                .Select(enumTypeId =>
                {
                    GraphAssetUtility.TryGetEnumTypeFromTypeId(enumTypeId, out Type enumType);
                    return enumType != null ? (enumType.FullName ?? enumType.Name) : $"{enumTypeId} (missing type)";
                })
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            Debug.LogWarning(
                $"Detected enum signature changes for {changedEnumTypeIds.Count} enum type(s). " +
                $"Changed enums: {string.Join(", ", changedEnumDisplayNames)}. " +
                $"Reimporting {graphPathsToReimport.Count} behavior graph(s). " +
                $"Use 'Tools/Behavior/Select Graphs Affected by Last Enum Change' to review impacted assets.");

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string graphPath in graphPathsToReimport)
                {
                    AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        [MenuItem("Tools/Behavior/Select Next Graph Affected by Enum Change", priority = 3)]
        private static void SelectNextGraphAffectedByEnumChange()
        {
            if (s_LastEnumChangedGraphPaths == null || s_LastEnumChangedGraphPaths.Count == 0)
            {
                Debug.Log("No graph was recorded from the last enum change detection.");
                return;
            }

            s_LastEnumChangedGraphPathIndex = (s_LastEnumChangedGraphPathIndex + 1) % s_LastEnumChangedGraphPaths.Count;
            string graphPath = s_LastEnumChangedGraphPaths[s_LastEnumChangedGraphPathIndex];
            BehaviorAuthoringGraph graphAsset = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(graphPath);
            if (graphAsset == null)
            {
                Debug.LogWarning($"Could not load graph asset at path '{graphPath}'.");
                return;
            }

            Selection.activeObject = graphAsset;
            EditorGUIUtility.PingObject(graphAsset);
            Debug.Log($"Selected affected graph [{s_LastEnumChangedGraphPathIndex + 1}/{s_LastEnumChangedGraphPaths.Count}]: {graphPath}", graphAsset);
        }

        [MenuItem("Tools/Behavior/Select Graphs Affected by Last Enum Change", priority = 4)]
        private static void SelectGraphsAffectedByLastEnumChange()
        {
            if (s_LastEnumChangedGraphPaths == null || s_LastEnumChangedGraphPaths.Count == 0)
            {
                Debug.Log("No graph was recorded from the last enum change detection.");
                return;
            }

            List<UnityEngine.Object> graphAssets = new();
            foreach (string graphPath in s_LastEnumChangedGraphPaths)
            {
                BehaviorAuthoringGraph graphAsset = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(graphPath);
                if (graphAsset == null)
                {
                    continue;
                }

                graphAssets.Add(graphAsset);
            }

            if (graphAssets.Count == 0)
            {
                Debug.LogWarning("No affected graphs could be loaded for selection.");
                return;
            }

            Selection.objects = graphAssets.ToArray();
            Selection.activeObject = graphAssets[0];
            EditorGUIUtility.PingObject(graphAssets[0]);
            Debug.Log($"Selected {graphAssets.Count}/{s_LastEnumChangedGraphPaths.Count} affected graph(s).");
        }

#if BEHAVIOR_LOCAL_TEST
        [MenuItem("Tools/Behavior/Dump Registered Enum Dependencies", priority = 2)]
        private static void DumpRegisteredEnumDependencies()
        {
            BehaviorGraphAssetRegistry registry = GlobalRegistry;
            registry.RebuildEnumDependencyIndex();

            if (registry.m_EnumTypeToGraphAssets.Count == 0)
            {
                Debug.Log("Behavior registry has no registered enum dependencies.");
                return;
            }

            var builder = new StringBuilder(1024);
            builder.AppendLine("Behavior Graph Registry - Registered Enum Dependencies");
            builder.AppendLine($"Enum Types: {registry.m_EnumTypeToGraphAssets.Count}");

            foreach (var kvp in registry.m_EnumTypeToGraphAssets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                string enumTypeId = kvp.Key;
                HashSet<BehaviorAuthoringGraph> graphs = kvp.Value;
                GraphAssetUtility.TryGetEnumTypeFromTypeId(enumTypeId, out Type enumType);
                bool hasSignature = GraphAssetUtility.TryComputeEnumDependencySignatureFromTypeId(enumTypeId, out string currentSignature);

                builder.AppendLine("----------------------------------------");
                builder.AppendLine($"Enum: {enumTypeId}");
                builder.AppendLine($"Current Signature: {(hasSignature ? currentSignature : "<missing type>")}");
                builder.AppendLine($"Graph Count: {graphs.Count}");

                if (enumType != null && enumType.IsEnum)
                {
                    builder.AppendLine("Values:");
                    foreach (var entry in GraphAssetUtility.GetEnumValueEntries(enumType))
                    {
                        builder.AppendLine($"\t- {entry.Name} = {entry.NumericValue}");
                    }
                }

                foreach (BehaviorAuthoringGraph graph in graphs.OrderBy(g => g != null ? g.name : string.Empty, StringComparer.Ordinal))
                {
                    if (graph == null)
                    {
                        continue;
                    }

                    graph.EnumDependencySignatures.TryGetValue(enumTypeId, out string storedSignature);
                    string graphPath = AssetDatabase.GetAssetPath(graph);
                    builder.AppendLine($"\t- {graph.name} ({graphPath})");
                    builder.AppendLine($"\t\tStored Signature: {(string.IsNullOrEmpty(storedSignature) ? "<none>" : storedSignature)}");
                }
            }

            Debug.Log(builder.ToString());
        }
#endif
        private class AssetRegistryBuildPopulator : BuildPlayerProcessor
        {
            public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
            {
                UpdateGlobalRegistry();
            }
        }
#endif

        public void OnBeforeSerialize()
        {

        }

        public void OnAfterDeserialize()
        {
            m_GuidToAsset = new Dictionary<SerializableGUID, BehaviorAuthoringGraph>();
            PurgeNullAndDuplicateAssets();
        }
    }
}
