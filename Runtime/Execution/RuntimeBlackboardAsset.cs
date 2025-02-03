using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Behavior
{
    /// <summary>
    /// RuntimeBlackboardAsset is the runtime version of BehaviorBlackboardAsset.
    /// The data is baked at authoring time.
    /// </summary>
    [Serializable]
    public class RuntimeBlackboardAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField]
        [HideInInspector]
        internal long VersionTimestamp;

        [HideInInspector]
        [SerializeField]
        internal SerializableGUID AssetID;

        [SerializeField]
        private Blackboard m_Blackboard;

        /// <summary>
        /// The Blackboard for the RuntimeBlackboardAsset.
        /// </summary>
        public Blackboard Blackboard => m_Blackboard;

        [SerializeField]
        private List<SerializableGUID> m_SharedBlackboardVariableGuids = new List<SerializableGUID>();

        internal HashSet<SerializableGUID> m_SharedBlackboardVariableGuidHashset = new HashSet<SerializableGUID>();

#if UNITY_EDITOR
        /// <summary>
        /// Static HashSet to keep track of all the assets that need to be backed up.
        /// </summary>
        private static HashSet<RuntimeBlackboardAsset> s_AssetToBackup = new();

        /// <summary>
        /// RuntimeBlackboardAsset act as reference to Blackboard.
        /// Per asset list of variables to backup. This is used to restore the values when exiting playmode.
        /// </summary>
        private List<BlackboardVariable> m_ValueOnEnterPlaymode = new List<BlackboardVariable>();

        /// <summary>
        /// This is called after a domain reload before OnEnable. Reset the list of assets to backup.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            s_AssetToBackup.Clear();
        }

        /// <summary>
        /// This is called everytime we enter playmode.
        /// Backups the value of the variables and listen to the playmode state change.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeInitialize()
        {
            EditorApplication.playModeStateChanged += InternalPlaymodeStateChanged;
            s_AssetToBackup.RemoveWhere(asset => asset == null);
            foreach (var asset in s_AssetToBackup)
            {
                asset.BackupAuthoringValue();
            }
        }

        /// <summary>
        /// We restore the value of the variables when exiting playmode.
        /// </summary>
        /// <param name="change"></param>
        private static void InternalPlaymodeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredEditMode:
                    s_AssetToBackup.RemoveWhere(asset => asset == null);
                    foreach (var asset in s_AssetToBackup)
                    {
                        asset.RestoreAuthoringValue();
                    }

                    EditorApplication.playModeStateChanged -= InternalPlaymodeStateChanged;
                    break;
            }
        }

        /// <summary>
        /// This is called the first time the asset is loaded in the editor (after creation or after a domain reload).
        /// </summary>
        private void OnEnable()
        {
            s_AssetToBackup.Add(this);
        }

        private void BackupAuthoringValue()
        {
            m_ValueOnEnterPlaymode.Clear();
            foreach (BlackboardVariable variable in m_Blackboard.Variables)
            {
                // We skip owner.
                if (variable.GUID == BehaviorGraph.k_GraphSelfOwnerID) continue;

                m_ValueOnEnterPlaymode.Add(variable.Duplicate());
            }
        }

        private void RestoreAuthoringValue()
        {
            foreach (BlackboardVariable variable in m_ValueOnEnterPlaymode)
            {
                if (variable.GUID == BehaviorGraph.k_GraphSelfOwnerID) continue;

                if (!m_Blackboard.SetVariableValue(variable.GUID, variable.ObjectValue))
                {
                    Debug.LogWarning($"Failed to reset variable \"{variable.Name}\" back to value \"{variable.ObjectValue}\"", this);
                }
            }

            m_ValueOnEnterPlaymode.Clear();
        }

#endif

        internal bool IsSharedVariable(SerializableGUID guid)
        {
            return m_SharedBlackboardVariableGuidHashset.Contains(guid);
        }

        /// <inheritdoc cref="OnBeforeSerialize"/>
        public void OnBeforeSerialize()
        {
            m_SharedBlackboardVariableGuids.Clear();
            foreach (var serializableGuid in m_SharedBlackboardVariableGuidHashset)
            {
                m_SharedBlackboardVariableGuids.Add(serializableGuid);
            }
        }

        /// <inheritdoc cref="OnAfterDeserialize"/>
        public void OnAfterDeserialize()
        {
            Blackboard.ValidateVariables();

            if (m_SharedBlackboardVariableGuids == null)
            {
                m_SharedBlackboardVariableGuids = new List<SerializableGUID>();
            }

            m_SharedBlackboardVariableGuidHashset.Clear();
            for (int i = 0; i < m_SharedBlackboardVariableGuids.Count; i++)
            {
                m_SharedBlackboardVariableGuidHashset.Add(m_SharedBlackboardVariableGuids[i]);
            }
        }
    }
}
