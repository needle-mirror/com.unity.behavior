using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Behavior
{
    internal class SharedBlackboardVariable : BlackboardVariable, ISharedBlackboardVariable
    {
        [SerializeField] private RuntimeBlackboardAsset m_GlobalVariablesRuntimeAsset;
        public override Type Type { get; }
        public override object ObjectValue   
        {
            get
            {
                m_GlobalVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable variable);

                if (variable == this)
                {
                    // use of implicit cast.
                    return this;
                }

                return variable;
            }
            set
            {
                m_GlobalVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable variable);
                
                if (variable == this)
                {
                    bool valueChanged = !Equals(this, value);
                    if (valueChanged && TrySetVariableValue(this, value))
                    {
                        InvokeValueChanged();
                    }
                }
                else if (!Equals(variable, value))
                {
                    m_GlobalVariablesRuntimeAsset.Blackboard.SetVariableValue(variable.GUID, value);
                    InvokeValueChanged();
                }
            }
        }
        
        public SharedBlackboardVariable()
        {
            Type = GetType();
        }

        public void SetSharedVariablesRuntimeAsset(RuntimeBlackboardAsset globalVariablesRuntimeAsset)
        {
            m_GlobalVariablesRuntimeAsset = globalVariablesRuntimeAsset;
        }

        internal override BlackboardVariable Duplicate()
        {
            var blackboardVariableDuplicate = CreateForType(Type, true);
            blackboardVariableDuplicate.Name = Name;
            blackboardVariableDuplicate.GUID = GUID;
            return blackboardVariableDuplicate;
        }

        public override bool ValueEquals(BlackboardVariable other)
        {
           return ObjectValue.Equals(other.ObjectValue);
        }

        /// <summary>
        /// Attempts to set variable value from an object type.
        /// </summary>
        /// <returns>false if the value type is not compatible</returns>
        private bool TrySetVariableValue<TValue>(BlackboardVariable variable, TValue value)
        {
            if (variable is BlackboardVariable<TValue> typedVar)
            {
                typedVar.Value = value;
                return true;
            }
            else if (variable is BlackboardVariable<GameObject> gameObjectVar && gameObjectVar.Type == typeof(TValue))
            {
                gameObjectVar.ObjectValue = value;
                return true;
            }
            else
            {
                Debug.LogError($"Incorrect value type ({typeof(TValue)}) specified for variable of type {variable.Type}.");
                return false;
            }
        }
    }
    
    [Serializable]
    internal class SharedBlackboardVariable<DataType> : BlackboardVariable<DataType>, ISharedBlackboardVariable
    {
        [SerializeField] internal RuntimeBlackboardAsset m_SharedVariablesRuntimeAsset;

        public SharedBlackboardVariable(){}
        
        public SharedBlackboardVariable(DataType value) : base(value)
        {
        }
        
        /// <summary>
        /// see <see cref="BlackboardVariable.ObjectValue"/>
        /// </summary>
        public override DataType Value
        {
            get
            {
                m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);
                if (this == variable)
                {
                    return m_Value;
                }
                
                return variable.Value;
            }
            set
            {
                m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);

                if (this == variable)
                {
                    if (!EqualityComparer<DataType>.Default.Equals(m_Value, value))
                    {
                        return;
                    }
                 
                    m_Value = value;
                    InvokeValueChanged();
                }
                else if(!EqualityComparer<DataType>.Default.Equals(variable.Value, value))
                {
                    m_SharedVariablesRuntimeAsset.Blackboard.SetVariableValue(variable.GUID, value);
                    InvokeValueChanged();
                }
            }
        }
        
        internal override BlackboardVariable Duplicate()
        {
            BlackboardVariable blackboardVariableDuplicate = CreateForType(Type, true);
            blackboardVariableDuplicate.Name = Name;
            blackboardVariableDuplicate.GUID = GUID;
            return blackboardVariableDuplicate;
        }
        
        public void SetSharedVariablesRuntimeAsset(RuntimeBlackboardAsset globalVariablesRuntimeAsset)
        {
            m_SharedVariablesRuntimeAsset = globalVariablesRuntimeAsset;
        }
    }

    internal interface ISharedBlackboardVariable
    {
        void SetSharedVariablesRuntimeAsset(RuntimeBlackboardAsset globalVariablesRuntimeAsset);
    }
}