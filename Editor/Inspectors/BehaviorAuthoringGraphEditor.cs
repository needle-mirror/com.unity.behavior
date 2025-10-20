using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    /// <summary>
    /// A custom editor for inspecting <see cref="BehaviorAuthoringGraph"/> assets.
    /// When an asset is selected, the inspector displays the variable and runtime sub-assets.
    /// Also display error and guide users to resolve issues (like missing type).
    /// </summary>
    [CustomEditor(typeof(BehaviorAuthoringGraph))]
    [CanEditMultipleObjects]
    internal class BehaviorAuthoringGraphEditor : Editor
    {
        private Dictionary<SerializableGUID, string> m_GuidToName = new();
        private VisualElement m_Root;

        public override VisualElement CreateInspectorGUI()
        {
            m_Root = new VisualElement();
            m_Root.AddToClassList("behavior-editor-root");

            BehaviorUIThemeManager.RegisterElement(m_Root);
            var stylesheet = ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Editor/Inspectors/Assets/BehaviorEditorStyles.uss");
            if (stylesheet != null)
            {
                m_Root.styleSheets.Add(stylesheet);
            }

            m_Root.TrackSerializedObjectValue(serializedObject, CheckForRename);

            RefreshUI();
            return m_Root;
        }

        private static string GetMissingTypeInfo(BehaviorAuthoringGraph authoringGraph, out int missingTypeCount)
        {
            missingTypeCount = 0;
            var missingTypeData = TypeMigrationUtility.ScanForMissingTypes(authoringGraph);
            if (missingTypeData == null || missingTypeData.Count == 0)
            {
                return string.Empty;
            }

            var missingTypeNames = new HashSet<string>();
            foreach (var typeData in missingTypeData)
            {
                foreach (var type in typeData.MissingTypes)
                {
                    missingTypeNames.Add(type.FullTypeName);
                }
            }

            if (missingTypeNames.Count == 0)
            {
                return string.Empty;
            }

            missingTypeCount = missingTypeNames.Count;
            var stringBuilder = new StringBuilder("\n");
            foreach (var typeName in missingTypeNames)
            {
                stringBuilder.AppendLine($"\t{typeName}");
            }
            return stringBuilder.ToString();
        }

        private static string GetPlaceholderTypeInfo(BehaviorAuthoringGraph authoringGraph, out int count)
        {
            count = 0;
            var stringBuilder = new StringBuilder("\n");
            foreach (var nodeModelInfo in authoringGraph.NodeModelsInfo)
            {
                if (!nodeModelInfo.IsPlaceholder)
                {
                    continue;
                }

                count++;
                stringBuilder.AppendLine($"\t{nodeModelInfo.Name} ({nodeModelInfo.RuntimeTypeString})");
            }

            return stringBuilder.ToString();
        }

        private static void DrawAssetsWithMissingType(VisualElement parent, BehaviorAuthoringGraph authoringGraph)
        {
            var container = new VisualElement();
            container.AddToClassList("behavior-container");

            var missingData = TypeMigrationUtility.ScanForMissingTypes(authoringGraph);

            // Display linked authoring graph assets
            var linkedLabel = new Label("Assets With Missing Type(s)");
            linkedLabel.AddToClassList("behavior-section-title");
            container.Add(linkedLabel);

            foreach (var info in missingData)
            {
                var fieldRow = BehaviorAssetEditorUtility.CreateBehaviorAssetField(info.Asset);
                fieldRow.AddToClassList("behavior-indented-content");
                container.Add(fieldRow);
            }

            parent.Add(container);
        }

        private void RefreshUI()
        {
            m_Root.Clear();
            m_GuidToName.Clear();

            foreach (Object targetAsset in serializedObject.targetObjects)
            {
                BehaviorAuthoringGraph authoringGraph = targetAsset as BehaviorAuthoringGraph;
                if (authoringGraph)
                {
                    m_GuidToName[authoringGraph.AssetID] = authoringGraph.name;
                }
            }

            int i = 0;
            foreach (Object targetAsset in targets)
            {
                BehaviorAuthoringGraph authoringGraph = targetAsset as BehaviorAuthoringGraph;
                if (authoringGraph == null)
                {
                    continue;
                }

                if (i++ > 0)
                {
                    BehaviorAssetEditorUtility.AddSeparator(m_Root);
                }

                // Asset name label
                var nameLabel = new Label(authoringGraph.name);
                nameLabel.AddToClassList("behavior-asset-title");
                m_Root.Add(nameLabel);

                // Check for missing SerializeReference types
                if (authoringGraph.ContainsInvalidSerializedReferences())
                {
                    BehaviorAssetEditorUtility.DrawCustomHelpbox(m_Root, true, ErrorMessages.k_MissingTypeInAssetHelpboxError, () =>
                        {
                            string affectedTypes = GetMissingTypeInfo(authoringGraph, out int count);
                            Debug.LogError(string.Format(ErrorMessages.k_MissingTypeInGraphAssetMessageError,
                                authoringGraph.name, count, affectedTypes), authoringGraph);
                        });
                    BehaviorAssetEditorUtility.AddSpace(m_Root);
                    DrawAssetsWithMissingType(m_Root, authoringGraph);
                    continue;
                }
                else if (authoringGraph.HasPlaceholderNode())
                {
                    BehaviorAssetEditorUtility.DrawCustomHelpbox(m_Root, false, ErrorMessages.k_PlaceholderInGraphAssetHelpboxWarning, () =>
                    {
                        string affectedNodes = GetPlaceholderTypeInfo(authoringGraph, out int count);
                        Debug.LogWarning(string.Format(ErrorMessages.k_PlaceholderInGraphAssetMessageWarning,
                            authoringGraph.name, count, affectedNodes), authoringGraph);
                    });
                    BehaviorAssetEditorUtility.AddSpace(m_Root);
                }

                DrawAssetData(m_Root, authoringGraph);

                var editButton = BehaviorAssetEditorUtility.CreateEditButton(authoringGraph, "Edit Behavior Graph");
                m_Root.Add(editButton);
            }
        }

        private void CheckForRename(SerializedObject changedObject)
        {
            BehaviorAuthoringGraph authoringGraph = changedObject.targetObject as BehaviorAuthoringGraph;
            SerializableGUID assetID = authoringGraph!.AssetID;
            string newName = authoringGraph.name;

            // If the new name is the same as the old name, do nothing.
            if (m_GuidToName.TryGetValue(assetID, out string oldName) && string.Equals(oldName, newName))
            {
                RefreshUI();
                return;
            }

            // Asset has new name, so rename runtime graph.
            m_GuidToName[authoringGraph.AssetID] = newName;
            string assetPath = AssetDatabase.GetAssetPath(authoringGraph);
            BehaviorGraph runtimeGraph = AssetDatabase.LoadAssetAtPath<BehaviorGraph>(assetPath);
            if (runtimeGraph)
            {
                runtimeGraph.name = newName;
            }
            BehaviorGraphDebugInfo debugInfo = AssetDatabase.LoadAssetAtPath<BehaviorGraphDebugInfo>(assetPath);
            if (debugInfo)
            {
                debugInfo.name = $"{newName} Debug Info";
            }
            RuntimeBlackboardAsset blackboardAsset = AssetDatabase.LoadAssetAtPath<RuntimeBlackboardAsset>(assetPath);
            if (blackboardAsset)
            {
                blackboardAsset.name = $"{newName} Blackboard";
            }
            EditorUtility.SetDirty(authoringGraph);
            AssetDatabase.SaveAssetIfDirty(authoringGraph);

            RefreshUI();
        }

        private void DrawAssetData(VisualElement parent, BehaviorAuthoringGraph targetAsset)
        {
            var container = new VisualElement();
            container.AddToClassList("behavior-container");

            // Display linked authoring graph assets
            var linkedLabel = new Label("Linked Authoring Graph Assets");
            linkedLabel.AddToClassList("behavior-section-title");
            container.Add(linkedLabel);

            if (targetAsset.SubgraphsInfo.Count == 0)
            {
                var noneLabel = new Label("(None)");
                noneLabel.AddToClassList("behavior-indented-content");
                noneLabel.AddToClassList("behavior-disabled-label");
                container.Add(noneLabel);
            }
            else
            {
                foreach (var subgraphInfo in targetAsset.SubgraphsInfo)
                {
                    var fieldRow = BehaviorAssetEditorUtility.CreateBehaviorAssetField(subgraphInfo.Asset);
                    fieldRow.AddToClassList("behavior-indented-content");
                    container.Add(fieldRow);
                }
            }

            BehaviorAssetEditorUtility.AddSpace(container);

            // Display runtime assets
            var runtimeLabel = new Label("Runtime Assets");
            runtimeLabel.AddToClassList("behavior-section-title");
            container.Add(runtimeLabel);

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(targetAsset));
            var runtimeGraph = assets.FirstOrDefault(asset => asset is BehaviorGraph) as BehaviorGraph;
            var runtimeBlackboard = assets.FirstOrDefault(asset => asset is RuntimeBlackboardAsset) as RuntimeBlackboardAsset;
            var debugInfo = assets.FirstOrDefault(asset => asset is BehaviorGraphDebugInfo) as BehaviorGraphDebugInfo;

            // Create a container for the runtime graph field
            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("behavior-indented-content");
            container.Add(fieldContainer);

            var runtimeGraphRow = BehaviorAssetEditorUtility.CreateBehaviorAssetField(runtimeGraph);
            fieldContainer.Add(runtimeGraphRow);

            var runtimeBlackboardRow = BehaviorAssetEditorUtility.CreateBehaviorAssetField(runtimeBlackboard);
            fieldContainer.Add(runtimeBlackboardRow);

            // Blackboard references
            if (targetAsset.m_Blackboards.Count > 0)
            {
                var blackboardLabel = new Label("Blackboard References");
                blackboardLabel.AddToClassList("behavior-section-title");
                container.Add(blackboardLabel);

                var blackboardFieldContainer = new VisualElement();
                blackboardFieldContainer.AddToClassList("behavior-indented-content");
                container.Add(blackboardFieldContainer);

                foreach (var asset in targetAsset.m_Blackboards)
                {
                    var blackboardRow = BehaviorAssetEditorUtility.CreateBehaviorAssetField(asset);
                    blackboardFieldContainer.Add(blackboardRow);
                }
            }

            parent.Add(container);
        }

        [MenuItem("CONTEXT/BehaviorAuthoringGraph/Regenerate Runtime Assets", isValidateFunction: false, validate = false)]
        private static void RegenerateRuntimeAssets(MenuCommand command)
        {
            BehaviorAuthoringGraph targetAsset = command.context as BehaviorAuthoringGraph;
            if (targetAsset == null)
                return;

            // Show a confirmation dialog before proceeding
            bool shouldProceed = EditorUtility.DisplayDialog(
                "Delete & Regenerate Runtime Assets",
                "This action will delete and regenerate runtime assets." +
                "\n\nAll hard dependencies (prefabs, scene objects, and BlackboardVariable<Subgraph>) " +
                "will be lost and need to be manually re-assigned." +
                "\n\nGraphs that depend on this asset as static subgraph will be automatically rebuilt and have their dependencies updated.",
                "Delete And Regenerate",
                "Cancel"
            );

            if (!shouldProceed)
            {
                return;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(targetAsset));
            foreach (Object asset in assets)
            {
                if (asset is BehaviorGraph or BehaviorGraphDebugInfo)
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    // Cleanup the existing hard references (prefab, scene object and assets).
                    // Only soft reference like subgraph dependency will be regenerated.
                    DestroyImmediate(asset, true);
                }
            }

            targetAsset.BuildRuntimeGraph();
            targetAsset.SaveAsset();
        }

        [MenuItem("CONTEXT/BehaviorAuthoringGraph/Regenerate Runtime Assets", isValidateFunction: true, validate = true)]
        private static bool RegenerateRuntimeAssets_Validate(MenuCommand command)
        {
            BehaviorAuthoringGraph targetAsset = command.context as BehaviorAuthoringGraph;
            if (targetAsset == null)
            {
                return false;
            }

            return !targetAsset.ContainsInvalidSerializedReferences();
        }
    }
}
