using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using UnityEngine;
using Unity.Properties;

namespace Unity.Behavior
{
    /// <summary>
    /// A data structure holding variables used by the Behavior Graph.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    public partial class Blackboard
    {
        /// <summary>
        /// Variables stored in the blackboard.
        /// </summary>
        public List<BlackboardVariable> Variables => m_Variables;

        [SerializeReference]
        internal List<BlackboardVariable> m_Variables = new();

        private Dictionary<SerializableGUID, BlackboardVariable> m_VariablesMap;

        /// <summary>
        /// Duplicates BBV from a source blackboard and generates metadata.
        /// If the source blackboard has shared BBV, SBBV are generated.
        /// </summary>
        internal void GenerateInstanceData(Blackboard sourceBlackboard, RuntimeBlackboardAsset sourceBlackboardAsset)
        {
            m_Variables.Clear();
            foreach (BlackboardVariable variable in sourceBlackboard.Variables)
            {
                if (variable == null)
                {
                    // Skip invalid variable - can happens when a variable serialized type has been renamed.
                    continue;
                }

                if (sourceBlackboardAsset.IsSharedVariable(variable.GUID))
                {
                    var sharedVariable = BlackboardVariable.CreateForType(variable.Type, true);
                    sharedVariable.Name = variable.Name;
                    sharedVariable.GUID = variable.GUID;
                    var sharedVariableInterface = (ISharedBlackboardVariable)sharedVariable;
                    sharedVariableInterface.SetSharedVariablesRuntimeAsset(sourceBlackboardAsset);
                    m_Variables.Add(sharedVariable);
                }
                else
                {
                    // Otherwise, duplicate the variable before adding it.
                    BlackboardVariable newVariable = variable.Duplicate();
                    m_Variables.Add(newVariable);
                }
            }

            CreateMetadata();
        }

        internal void ValidateVariables()
        {
            for (int i = m_Variables.Count - 1; i >= 0; i--)
            {
                BlackboardVariable variable = m_Variables[i];
                if (variable == null)
                {
                    m_Variables.RemoveAt(i);
                }
            }
        }

        internal void ReplaceBlackboardVariable(SerializableGUID guid, BlackboardVariable newVariable)
        {
            if (m_VariablesMap.TryGetValue(guid, out BlackboardVariable oldVariable))
            {
                m_Variables.Remove(oldVariable);
                m_VariablesMap.Remove(guid);
                m_Variables.Add(newVariable);
                m_VariablesMap[newVariable.GUID] = newVariable;
            }
        }

        internal bool AddVariable<TValue>(string name, TValue value)
        {
            foreach (BlackboardVariable variable in Variables)
            {
                if (name.Equals(variable.Name))
                {
                    Debug.LogWarning($"A variable with name {name} has already been added to the blackboard.");
                    return false;
                }
            }

            BlackboardVariable newVariable = BlackboardVariable.CreateForType(typeof(TValue));
            newVariable.Name = name;
            newVariable.GUID = SerializableGUID.Generate();

            if (newVariable is BlackboardVariable<TValue> typedVariable)
            {
                typedVariable.Value = value;
            }

            m_Variables.Add(newVariable);
            return true;
        }

        internal bool GetVariable<TValue>(string name, out BlackboardVariable<TValue> variable)
        {
            foreach (BlackboardVariable blackboardVariable in Variables)
            {
                if (!name.Equals(blackboardVariable.Name))
                {
                    continue;
                }

                if (blackboardVariable is BlackboardVariable<TValue> typedVariable)
                {
                    variable = typedVariable;
                    return true;
                }
            }
            variable = default;
            return false;
        }

        internal bool GetVariable(string name, out BlackboardVariable variable)
        {
            foreach (BlackboardVariable blackboardVariable in Variables)
            {
                if (name.Equals(blackboardVariable.Name))
                {
                    variable = blackboardVariable;
                    return true;
                }
            }

            variable = default;
            return false;
        }

        internal bool GetVariableValue<TValue>(string name, out TValue value)
        {
            foreach (BlackboardVariable variable in Variables)
            {
                if (name.Equals(variable.Name) && variable is BlackboardVariable<TValue> typedVariable)
                {
                    value = typedVariable.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        internal bool SetVariableValue<TValue>(string name, TValue value)
        {
            foreach (BlackboardVariable variable in Variables)
            {
                if (!name.Equals(variable.Name))
                {
                    continue;
                }

                if (variable is BlackboardVariable<TValue> typedVariable)
                {
                    typedVariable.Value = value;
                    return true;
                }
                if (value == null)
                {
                    if (variable.Type.IsValueType)
                    {
                        variable.ObjectValue = Activator.CreateInstance(variable.Type);
                    }
                    else
                    {
                        variable.ObjectValue = null;
                    }
                    return true;
                }

                if (value.GetType().IsAssignableFrom(variable.Type))
                {
                    var convertedValue = Convert.ChangeType(value, variable.Type);
                    if (convertedValue != null)
                    {
                        variable.ObjectValue = convertedValue;
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool GetVariableID(string name, out SerializableGUID id)
        {
            foreach (BlackboardVariable blackboardVariable in Variables)
            {
                if (blackboardVariable.Name == name)
                {
                    id = blackboardVariable.GUID;
                    return true;
                }
            }

            id = default;
            return false;
        }

        internal bool GetVariable(SerializableGUID guid, out BlackboardVariable variable)
        {
            if (m_VariablesMap == null || m_Variables.Count != m_VariablesMap.Count)
            {
                CreateMetadata();
            }

            return m_VariablesMap.TryGetValue(guid, out variable);
        }

        internal bool GetVariable<TValue>(SerializableGUID guid, out BlackboardVariable<TValue> variable)
        {
            if (m_VariablesMap == null || m_Variables.Count != m_VariablesMap.Count)
            {
                CreateMetadata();
            }

            if (m_VariablesMap.TryGetValue(guid, out BlackboardVariable var) && var is BlackboardVariable<TValue> typedVar)
            {
                variable = typedVar;
                return true;
            }

            variable = default;
            return false;
        }

        internal bool SetVariableValue<TValue>(SerializableGUID guid, TValue value)
        {
            if (m_VariablesMap == null || m_Variables.Count != m_VariablesMap.Count)
            {
                CreateMetadata();
            }

            if (!m_VariablesMap.TryGetValue(guid, out BlackboardVariable var))
            {
                Debug.LogError($"Variable of type {typeof(TValue)} not found. GUID: {guid}");
                return false;
            }

            if (var is BlackboardVariable<TValue> typedVar)
            {
                typedVar.Value = value;
                return true;
            }
            if (var is BlackboardVariable<GameObject> gameObjectVar && gameObjectVar.Type == typeof(TValue))
            {
                gameObjectVar.ObjectValue = value;
                return true;
            }
            if (value == null)
            {
                if (var.Type.IsValueType)
                {
                    var.ObjectValue = Activator.CreateInstance(var.Type);
                }
                else
                {
                    var.ObjectValue = null;
                }
                return true;
            }
            if (value.GetType().IsAssignableFrom(var.Type))
            {
                var convertedValue = Convert.ChangeType(value, var.Type);
                if (convertedValue != null)
                {
                    var.ObjectValue = convertedValue;
                    return true;
                }
            }

            Debug.LogError($"Incorrect value type ({typeof(TValue)}) specified for variable of type {var.Type}.");
            return false;
        }

        internal bool SetVariableValueWithoutNotify<TValue>(SerializableGUID guid, TValue value)
        {
            if (m_VariablesMap == null || m_Variables.Count != m_VariablesMap.Count)
            {
                CreateMetadata();
            }

            if (!m_VariablesMap.TryGetValue(guid, out BlackboardVariable var))
            {
                Debug.LogError($"Variable of type {typeof(TValue)} not found. GUID: {guid}");
                return false;
            }

            if (var is BlackboardVariable<TValue> typedVar)
            {
                typedVar.SetValueWithoutNotify(value);

                return true;
            }
            if (var is BlackboardVariable<GameObject> gameObjectVar && gameObjectVar.Type == typeof(TValue))
            {
                var.SetObjectValueWithoutNotify(value);
                return true;
            }
            if (value == null)
            {
                if (var.Type.IsValueType)
                {
                    var.SetObjectValueWithoutNotify(Activator.CreateInstance(var.Type));
                }
                else
                {
                    var.SetObjectValueWithoutNotify(null);
                }

                return true;
            }
            if (value.GetType().IsAssignableFrom(var.Type))
            {
                var convertedValue = Convert.ChangeType(value, var.Type);
                if (convertedValue != null)
                {
                    var.SetObjectValueWithoutNotify(convertedValue);
                    return true;
                }
            }

            Debug.LogError($"Incorrect value type ({typeof(TValue)}) specified for variable of type {var.Type}.");
            return false;
        }

        internal void CreateMetadata()
        {
            m_VariablesMap = new Dictionary<SerializableGUID, BlackboardVariable>(m_Variables.Count);
            foreach (BlackboardVariable var in m_Variables)
            {
                m_VariablesMap.Add(var.GUID, var);
            }
        }
    }
}