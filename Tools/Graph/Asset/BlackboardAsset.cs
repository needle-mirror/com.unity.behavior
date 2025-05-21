using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Behavior.GraphFramework
{
    internal class BlackboardAsset : ScriptableObject
    {
        [HideInInspector]
        [SerializeField]
        public SerializableGUID AssetID = SerializableGUID.Generate();
        
        [SerializeReference, HideInInspector]
        private List<VariableModel> m_Variables = new();
        public List<VariableModel> Variables
        {
            get => m_Variables;
            internal set
            {
                m_Variables = value;
                OnBlackboardChanged.Invoke(BlackboardChangedType.ModelChanged);
            }
        }

        /// <summary>
        /// Does the asset needs to rebuilt its data.
        /// </summary>
        internal bool HasOutstandingChanges { get; set; }

        private void Awake()
        {
#if UNITY_EDITOR
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(GetInstanceID()));
            AssetID = new SerializableGUID(guid);
#endif
        }

        public enum BlackboardChangedType
        {
            ModelChanged,
            VariableAdded,
            VariableDeleted,
            VariableRenamed,
            VariableValueChanged,
            UndoRedo,
            VariableSetGlobal
        }

        [SerializeField][HideInInspector]
        internal long m_VersionTimestamp;
        public long VersionTimestamp => m_VersionTimestamp;

        /// <summary>
        /// Delegate for blackboard changes.
        /// </summary>
        public delegate void BlackboardChangedCallback(BlackboardChangedType changeType);
        
        /// <summary>
        /// Callback used for changes in the blackboard asset.
        /// </summary>
        public event BlackboardChangedCallback OnBlackboardChanged = delegate { };
        
        /// <summary>
        /// Invokes the OnBlackboardChanged callback.
        /// </summary>
        public void InvokeBlackboardChanged(BlackboardChangedType changeType) => OnBlackboardChanged.Invoke(changeType);
        
        /// <summary>
        /// Delegate for deleted blackboard assets.
        /// </summary>
        public delegate void BlackboardDeletedCallback(BlackboardAsset blackboard);
        
        /// <summary>
        /// Callback used for notifying when the asset is deleted.
        /// </summary>
        public event BlackboardDeletedCallback OnBlackboardDeleted = delegate { };
        
        /// <summary>
        /// Invokes the OnBlackboardDeleted callback.
        /// </summary>
        public void InvokeBlackboardDeleted() => OnBlackboardDeleted.Invoke(this);

        internal virtual void OnValidate()
        {
            Variables.RemoveAll(variable => variable == null);
            foreach (VariableModel variable in Variables)
            {
                variable.OnValidate();
            }
        }

        public void MarkUndo(string description)
        {
#if UNITY_EDITOR
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (description.Contains(assetPath) == false)
            {
                description += $" ({assetPath})";
            }

            UnityEditor.Undo.RegisterCompleteObjectUndo(this, description);
#endif
            // There are still a few lingering non-command changes to asset data preceded by MarkUndo() calls.
            // In order to pick up these changes, set the asset dirty here too.
            SetAssetDirty();
        }
        
        public void SetAssetDirty()
        {
            m_VersionTimestamp = DateTime.Now.Ticks;
            HasOutstandingChanges = true;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public virtual void SaveAsset()
        {
            HasOutstandingChanges = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
#endif
        }
    }
}