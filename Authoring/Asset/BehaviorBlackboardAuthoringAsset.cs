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
#if UNITY_EDITOR
    [CreateAssetMenu(fileName = "Blackboard", menuName = "Behavior/Blackboard")]
#endif
    internal class BehaviorBlackboardAuthoringAsset : BlackboardAsset
    {
        [SerializeField]
        private SerializableCommandBuffer m_CommandBuffer = new SerializableCommandBuffer();        
        public SerializableCommandBuffer CommandBuffer => m_CommandBuffer;
            
        [SerializeReference]
        private RuntimeBlackboardAsset m_RuntimeBlackboardAsset;
        
        internal RuntimeBlackboardAsset RuntimeBlackboardAsset => m_RuntimeBlackboardAsset;
        
        private void OnEnable()
        {
#if UNITY_EDITOR
            bool HasGraphInPath(string path)
            {
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var embeddedObject in objects)
                {
                    if (typeof(GraphAsset).IsAssignableFrom(embeddedObject.GetType()))
                    {
                        return true;
                    }
                }

                return false;
            }
            var assetPath = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(assetPath) && !EditorApplication.isPlayingOrWillChangePlaymode && !HasGraphInPath(assetPath))
            {
                string assetPathName =
                    System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (name != assetPathName)
                {
                    name = assetPathName;
                    AssetDatabase.SetMainObject(this, assetPath);
                }
            }
            if (m_RuntimeBlackboardAsset != null && m_RuntimeBlackboardAsset.name != name)
            {
                m_RuntimeBlackboardAsset.name = name;
            }
#endif
            if (m_RuntimeBlackboardAsset == null)
            {
                BuildRuntimeBlackboard();
            }
        }

        internal override void OnValidate()
        {
            base.OnValidate();
        }

#if UNITY_EDITOR
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
#endif
        
        private static RuntimeBlackboardAsset GetOrCreateBlackboardAsset(BehaviorBlackboardAuthoringAsset assetObject)
        {
            RuntimeBlackboardAsset reference;
#if UNITY_EDITOR
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
#endif

            reference = CreateInstance<RuntimeBlackboardAsset>();
            reference.name = assetObject.name;
            reference.AssetID = assetObject.AssetID;
            
#if UNITY_EDITOR
            AssetDatabase.AddObjectToAsset(reference, assetObject);
            EditorUtility.SetDirty(assetObject);
            AssetDatabase.SaveAssetIfDirty(assetObject);
#endif
            return reference;
        }

        public bool IsAssetVersionUpToDate()
        {
            return !HasOutstandingChanges;
        }

        public RuntimeBlackboardAsset BuildRuntimeBlackboard()
        {
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
                var blackboardVariable = m_RuntimeBlackboardAsset.Blackboard.Variables.Find(obj => obj.GUID == variable.ID);
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

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            return m_RuntimeBlackboardAsset;
        }
    }
}