using System.Text;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [CustomEditor(typeof(BehaviorBlackboardAuthoringAsset))]
    [CanEditMultipleObjects]
    internal class BehaviorBlackboardAuthoringAssetEditor : Editor
    {
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

            // Track changes to refresh the UI
            m_Root.TrackSerializedObjectValue(serializedObject, _ => RefreshUI());

            RefreshUI();
            return m_Root;
        }

        private static string GetMissingTypeInfo(BehaviorBlackboardAuthoringAsset authoringBlackboard)
        {
            var missingTypeData = TypeMigrationUtility.ScanForMissingTypes(authoringBlackboard);
            StringBuilder stringBuilder = new StringBuilder();
            if (missingTypeData != null)
            {
                stringBuilder.AppendLine("\n");
                foreach (var type in missingTypeData.MissingTypes)
                {
                    stringBuilder.AppendLine($"\t{type.FullTypeName}");
                }
            }

            return stringBuilder.ToString();
        }

        private void RefreshUI()
        {
            m_Root.Clear();

            int i = 0;
            foreach (Object targetAsset in targets)
            {
                BehaviorBlackboardAuthoringAsset authoringBlackboard = targetAsset as BehaviorBlackboardAuthoringAsset;
                if (authoringBlackboard == null)
                {
                    continue;
                }

                if (i++ > 0)
                {
                    BehaviorAssetEditorUtility.AddSeparator(m_Root);
                }

                // Asset name label
                var nameLabel = new Label(authoringBlackboard.name);
                nameLabel.AddToClassList("behavior-asset-title");
                m_Root.Add(nameLabel);

                // Check for missing SerializeReference types
                if (authoringBlackboard.ContainsInvalidSerializedReferences())
                {
                    BehaviorAssetEditorUtility.DrawCustomHelpbox(m_Root, true, ErrorMessages.k_MissingTypeInAssetHelpboxError, () =>
                    {
                        Debug.LogError(ErrorMessages.k_MissingTypeInAssetHelpboxError + GetMissingTypeInfo(authoringBlackboard), authoringBlackboard);
                    });
                    continue;
                }

                // Draw asset data
                DrawAssetData(m_Root, authoringBlackboard);

                // Edit button
                var editButton = BehaviorAssetEditorUtility.CreateEditButton(authoringBlackboard, "Edit Blackboard");
                m_Root.Add(editButton);
            }
        }

        private void DrawAssetData(VisualElement parent, BehaviorBlackboardAuthoringAsset asset)
        {
            var container = new VisualElement();
            container.AddToClassList("behavior-container");

            // Runtime Assets section
            var runtimeLabel = new Label("Runtime Assets");
            runtimeLabel.AddToClassList("behavior-section-title");
            container.Add(runtimeLabel);

            var runtimeBlackboardRow = BehaviorAssetEditorUtility.CreateBehaviorAssetField(asset.RuntimeBlackboardAsset);
            runtimeBlackboardRow.AddToClassList("behavior-indented-content");
            container.Add(runtimeBlackboardRow);

            // Experimental for 1.0.14: Variables section
            // BehaviorAssetEditorUtility.DrawBlackboardVariable(container, asset);

            parent.Add(container);
        }
    }
}
