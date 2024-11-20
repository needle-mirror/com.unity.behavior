using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// RuntimeBlackboardAsset is the runtime version of BehaviorBlackboardAsset.
    /// </summary>
    [Serializable]
    public class RuntimeBlackboardAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField][HideInInspector]
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
        private List<BlackboardVariable> m_ValueOnEnterPlaymode = new List<BlackboardVariable>();

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            UnityEditor.EditorApplication.playModeStateChanged += InternalPlaymodeStateChanged;
        }

        private static bool s_IsInPlayMode;
        private static void InternalPlaymodeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            switch (change)
            {
                case UnityEditor.PlayModeStateChange.ExitingEditMode:
                case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                    s_IsInPlayMode = true;
                    break;

                default:
                    s_IsInPlayMode = false;
                    break;
            }
        }

        private void OnEnable()
        {
            if (!s_IsInPlayMode)
            {
                return;
            }
            m_ValueOnEnterPlaymode.Clear();
            Application.quitting += OnApplicationQuit;
            foreach (BlackboardVariable variable in m_Blackboard.Variables)
            {
                m_ValueOnEnterPlaymode.Add(variable.Duplicate());
            }
        }

        private void OnApplicationQuit()
        {
            if (s_IsInPlayMode)
            {
                return;
            }

            m_Blackboard.m_Variables.Clear();
            foreach (BlackboardVariable variable in m_ValueOnEnterPlaymode)
            {
                m_Blackboard.m_Variables.Add(variable);
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