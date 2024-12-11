#if UNITY_EDITOR
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEngine;

namespace Unity.Behavior
{
    sealed class BehaviorAssetSettingsProvider : SettingsProvider
    {
        private const string k_PrefsKeyGraphOwnerName = "DefaultGraphOwnerName"; 
        
        public BehaviorAssetSettingsProvider() : base("Project/Behavior/Asset Settings", SettingsScope.Project) { }

        public override void OnGUI(string search)
        {
            EditorGUIUtility.labelWidth = 260.0f;
            BehaviorProjectSettings settings = BehaviorProjectSettings.instance;
            var behaviorProjectSettingsSO = settings.GetSerializedObject();

            EditorGUI.BeginChangeCheck();
            string graphOwnerName = EditorGUILayout.TextField("Default Graph Owner Name", settings.GraphOwnerName);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "Default Graph Owner Name");
                settings.GraphOwnerName = graphOwnerName;
                GraphPrefsUtility.SetString(k_PrefsKeyGraphOwnerName, graphOwnerName, true);
            }


            //EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_Namespace"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_AutoSaveLastSaveLocation"));
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_UseSeparateSaveFolders"));
            EditorGUI.BeginDisabledGroup(settings.AutoSaveLastSaveLocation);

            EditorGUI.BeginDisabledGroup(settings.UseSeparateSaveFolders);
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolder"));
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!settings.UseSeparateSaveFolders);
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolderAction"));
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolderModifier"));
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolderFlow"));
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolderCondition"));
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolderEnum"));
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_SaveFolderEventChannels"));
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.PropertyField(behaviorProjectSettingsSO.FindProperty("m_AutoOpenNodeScriptsInExternalEditor"), new GUIContent("Auto-Open Node Scripts in External Editor"));

            behaviorProjectSettingsSO.ApplyModifiedProperties();
        }

        private static BehaviorAssetSettingsProvider m_Instance;
        internal static BehaviorAssetSettingsProvider Instance
        {
            get
            {
                m_Instance ??= new BehaviorAssetSettingsProvider();
                return m_Instance;
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateCustomSettingsProvider() => new BehaviorAssetSettingsProvider();
    }
}
#endif
