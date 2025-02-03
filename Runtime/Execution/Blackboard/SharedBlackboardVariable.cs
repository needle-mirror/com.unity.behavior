using System;
using System.Collections.Generic;
using UnityEngine;

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
                SetValue(value, notifyChange: true);
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
            OnValueChanged += () =>
            {
                blackboardVariableDuplicate.InvokeValueChanged();
            };
            return blackboardVariableDuplicate;
        }

        /// <inheritdoc cref="SetObjectValueWithoutNotify"/>
        public override void SetObjectValueWithoutNotify(object newValue)
        {
            SetValue(newValue, notifyChange: false);
        }

        public override bool ValueEquals(BlackboardVariable other)
        {
            return ObjectValue.Equals(other.ObjectValue);
        }

        private void SetValue(object value, bool notifyChange)
        {
            m_GlobalVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable variable);

            if (variable == this)
            {
                if (!Equals(this, value))
                {
                    TrySetVariableValue(this, value, notifyChange);
                }
            }
            else if (!Equals(variable, value))
            {
                m_GlobalVariablesRuntimeAsset.Blackboard.SetVariableValue(variable.GUID, value);
                // We don't InvokeValueChanged as it is going to be self propagated - see Duplicate().
            }
        }

        /// <summary>
        /// Attempts to set variable value from an object type.
        /// This method is realistically never called as SharedBBV should always be typed.
        /// </summary>
        /// <returns>false if the value type is not compatible</returns>
        private bool TrySetVariableValue<TValue>(BlackboardVariable variable, TValue value, bool notifyChange)
        {
            if (variable is BlackboardVariable<TValue> typedVar)
            {
                if (notifyChange)
                {
                    typedVar.Value = value;
                }
                else
                {
                    typedVar.SetValueWithoutNotify(value);
                }
                return true;
            }
            else if (variable is BlackboardVariable<GameObject> gameObjectVar && gameObjectVar.Type == typeof(TValue))
            {
                if (notifyChange)
                {
                    gameObjectVar.ObjectValue = value;
                }
                else
                {
                    gameObjectVar.SetObjectValueWithoutNotify(value);
                }
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

        public SharedBlackboardVariable()
        { }

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
                SetValue(value, notifyChange: true);
            }
        }

        internal override BlackboardVariable Duplicate()
        {
            BlackboardVariable blackboardVariableDuplicate = CreateForType(Type, true);
            blackboardVariableDuplicate.Name = Name;
            blackboardVariableDuplicate.GUID = GUID;
            OnValueChanged += () =>
            {
                blackboardVariableDuplicate.InvokeValueChanged();
            };
            return blackboardVariableDuplicate;
        }

        /// <inheritdoc cref="SetObjectValueWithoutNotify"/>
        public override void SetObjectValueWithoutNotify(object newValue)
        {
            SetValue((DataType)newValue, notifyChange: false);
        }

        /// <inheritdoc cref="SetValueWithoutNotify"/>
        public override void SetValueWithoutNotify(DataType newValue)
        {
            SetValue(newValue, notifyChange: false);
        }

        public void SetSharedVariablesRuntimeAsset(RuntimeBlackboardAsset globalVariablesRuntimeAsset)
        {
            m_SharedVariablesRuntimeAsset = globalVariablesRuntimeAsset;
        }

        private void SetValue(DataType newValue, bool notifyChange)
        {
            m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);

            if (this == variable)
            {
                if (!EqualityComparer<DataType>.Default.Equals(m_Value, newValue))
                {
                    return;
                }

                m_Value = newValue;
                if (notifyChange)
                {
                    InvokeValueChanged();
                }
            }
            else if (!EqualityComparer<DataType>.Default.Equals(variable.Value, newValue))
            {
                if (notifyChange)
                {
                    m_SharedVariablesRuntimeAsset.Blackboard.SetVariableValue(variable.GUID, newValue);
                }
                else
                {
                    m_SharedVariablesRuntimeAsset.Blackboard.SetVariableValueWithoutNotify(variable.GUID, newValue);
                }
                // We don't InvokeValueChanged as it is going to be self propagated - see Duplicate().
            }
        }
    }

    internal interface ISharedBlackboardVariable
    {
        void SetSharedVariablesRuntimeAsset(RuntimeBlackboardAsset globalVariablesRuntimeAsset);
    }
}