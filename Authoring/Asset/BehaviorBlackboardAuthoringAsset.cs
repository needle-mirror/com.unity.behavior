using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Behavior.GraphFramework;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Blackboard asset type used by Unity Behavior. The asset contains a collection of Blackboard variables.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Blackboard", menuName = "Behavior/Blackboard")]
    internal class BehaviorBlackboardAuthoringAsset : BlackboardAsset, ISerializationValidator
    {
        [SerializeField]
        private SerializableCommandBuffer m_CommandBuffer = new SerializableCommandBuffer();
        public SerializableCommandBuffer CommandBuffer => m_CommandBuffer;

        [SerializeField]
        private RuntimeBlackboardAsset m_RuntimeBlackboardAsset;

        internal RuntimeBlackboardAsset RuntimeBlackboardAsset => m_RuntimeBlackboardAsset;

        private void OnEnable()
        {
            if (m_RuntimeBlackboardAsset == null)
            {
                BuildRuntimeBlackboard();
            }
            else if (m_RuntimeBlackboardAsset.name != name)
            {
                m_RuntimeBlackboardAsset.name = name; // dirty the asset
            }
        }

        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !EditorUtility.IsPersistent(this)
                || !ContainsInvalidSerializedReferences())
            {
                return;
            }

            // if a persistent standalone blackboard asset has missing type, warn user.
            var assetPath = AssetDatabase.GetAssetPath(this);
            if (!AssetDatabase.LoadAssetAtPath<GraphAsset>(assetPath))
            {
                AssetLogger.LogAssetManagedReferenceError(this);
            }
        }

        internal override void ValidateAsset()
        {
            // We can't validate if asset isn't written on the disk or have missing type from serialized data.
            if (!EditorUtility.IsPersistent(this) || ContainsInvalidSerializedReferences())
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(this);
            // If this is a standalone Blackboard asset, make sure it's the main object.
            if (!AssetDatabase.LoadAssetAtPath<GraphAsset>(assetPath) && !AssetDatabase.IsMainAsset(this))
            {
                AssetDatabase.SetMainObject(this, assetPath);
            }
            base.ValidateAsset();
        }

        [OnOpenAsset(1)]
        public static bool OnOpenBlackboardAsset(int instanceID, int line)
        {
            BehaviorBlackboardAuthoringAsset asset = EditorUtility.InstanceIDToObject(instanceID) as BehaviorBlackboardAuthoringAsset;
            if (asset == null)
            {
                return false;
            }
            BlackboardWindowDelegate.Open(asset);
            return true;
        }

        private static RuntimeBlackboardAsset GetOrCreateBlackboardAsset(BehaviorBlackboardAuthoringAsset assetObject)
        {
            RuntimeBlackboardAsset reference;
            string assetPath = AssetDatabase.GetAssetPath(assetObject);
            if (!EditorUtility.IsPersistent(assetObject) || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            reference = AssetDatabase.LoadAllAssetsAtPath(assetPath).
                FirstOrDefault(asset => asset is RuntimeBlackboardAsset) as RuntimeBlackboardAsset;
            if (reference != null)
            {
                return reference;
            }

            reference = CreateInstance<RuntimeBlackboardAsset>();
            reference.name = assetObject.name;
            reference.AssetID = assetObject.AssetID;

            AssetDatabase.AddObjectToAsset(reference, assetObject);
            EditorUtility.SetDirty(assetObject);
            AssetDatabase.SaveAssetIfDirty(assetObject);
            return reference;
        }

        public bool IsAssetVersionUpToDate()
        {
            return !HasOutstandingChanges
                && !EditorUtility.IsDirty(this); // Only efficient way to notice an undo action on Blackboard.
        }

        public RuntimeBlackboardAsset BuildRuntimeBlackboard()
        {
            if (ContainsInvalidSerializedReferences())
            {
                Debug.LogWarning($"Blackboard asset {name} has missing types in managed references. Cannot build runtime blackboard.", this);
                return null;
            }

            m_RuntimeBlackboardAsset = GetOrCreateBlackboardAsset(this);
            if (m_RuntimeBlackboardAsset == null)
            {
                return null;
            }

            // Renaming dirty the asset, so we want to do it only when required.
            if (m_RuntimeBlackboardAsset.name != name)
            {
                m_RuntimeBlackboardAsset.name = name;
            }

            if (m_RuntimeBlackboardAsset.VersionTimestamp == VersionTimestamp)
            {
                return m_RuntimeBlackboardAsset;
            }

#if BEHAVIOR_DEBUG_ASSET_IMPORT
            Debug.Log($"BlackboardAsset[<b>{name}</b>].BuildRuntimeBlackboard", this);
#endif
            m_RuntimeBlackboardAsset.VersionTimestamp = VersionTimestamp;

            // Refreshing AssetID, just in case.
            m_RuntimeBlackboardAsset.AssetID = AssetID;
            HasOutstandingChanges = true;

            // Need to check each value for change... We want to make sure we keep the rid identical
            // So, do not clear all the variables! (i.e. m_RuntimeBlackboardAsset.Blackboard.m_Variables.Clear())
            HashSet<BlackboardVariable> remainingDirtyVariables = new HashSet<BlackboardVariable>(m_RuntimeBlackboardAsset.Blackboard.Variables);
            m_RuntimeBlackboardAsset.m_SharedBlackboardVariableGuidHashset.Clear();
            foreach (VariableModel variable in Variables)
            {
                if (variable == null)
                {
                    continue;
                }

                var blackboardVariable = m_RuntimeBlackboardAsset.Blackboard.Variables.Find(obj =>
                {
                    // If missing type, variable will appear null.
                    if (obj == null) return false;
                    return obj.GUID == variable.ID;
                });

                if (blackboardVariable != null)
                {
                    if (blackboardVariable.Name != variable.Name)
                    {
                        blackboardVariable.Name = variable.Name;
                    }

                    // No need to do complex check as this is done when the runtime variable is created.
                    if (blackboardVariable.ObjectValue != variable.ObjectValue)
                    {
                        blackboardVariable.ObjectValue = variable.ObjectValue;
                    }

                    if (variable.IsShared)
                    {
                        m_RuntimeBlackboardAsset.m_SharedBlackboardVariableGuidHashset.Add(variable.ID);
                    }

                    remainingDirtyVariables.Remove(blackboardVariable);
                    continue;
                }

                // If we reached this point, that means a new blackboard variable has been created.
                blackboardVariable = BlackboardVariable.CreateForType(variable.Type);
                blackboardVariable.Name = variable.Name;
                blackboardVariable.GUID = variable.ID;

                if (typeof(UnityEngine.Object).IsAssignableFrom(variable.Type))
                {
                    UnityEngine.Object unityObject = variable.ObjectValue as UnityEngine.Object;
                    if (unityObject != null)
                    {
                        blackboardVariable.ObjectValue = variable.ObjectValue;
                    }
                }
                else if (variable.ObjectValue != null)
                {
                    blackboardVariable.ObjectValue = variable.ObjectValue;
                }

                if (variable.IsShared)
                {
                    m_RuntimeBlackboardAsset.m_SharedBlackboardVariableGuidHashset.Add(variable.ID);
                }

                m_RuntimeBlackboardAsset.Blackboard.m_Variables.Add(blackboardVariable);
            }

            foreach (var dirtyVar in remainingDirtyVariables)
            {
                m_RuntimeBlackboardAsset.Blackboard.Variables.Remove(dirtyVar);
            }
            remainingDirtyVariables.Clear();

            // Sort m_RuntimeBlackboardAsset.Blackboard.m_Variables in-place based on the Variables (models) positions.
            Dictionary<SerializableGUID, int> desiredPositions = new(Variables.Count);
            for (int i = 0; i < Variables.Count; i++)
            {
                if (Variables[i] == null)
                {
                    continue;
                }

                desiredPositions[Variables[i].ID] = i;
            }

            m_RuntimeBlackboardAsset.Blackboard.m_Variables.Sort((a, b) =>
            {
                int posA = desiredPositions[a.GUID];
                int posB = desiredPositions[b.GUID];
                return posA.CompareTo(posB);
            });

            EditorUtility.SetDirty(this);
            return m_RuntimeBlackboardAsset;
        }

        public bool ContainsInvalidSerializedReferences()
        {
            return SerializationUtility.HasManagedReferencesWithMissingTypes(this) ||
                   SerializationUtility.HasManagedReferencesWithMissingTypes(m_RuntimeBlackboardAsset);
        }
    }
}
