using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Behavior
{
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
                if (m_SharedVariablesRuntimeAsset == null)
                {
                    Debug.LogError($"Shared variable '{Name}' is missing its source of truth. Returning initial value.");
                    return m_Value;
                }

                m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);
                if (variable == null || this == variable)
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
            ((ISharedBlackboardVariable)blackboardVariableDuplicate).SetSharedVariablesRuntimeAsset(m_SharedVariablesRuntimeAsset);

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
            if (m_SharedVariablesRuntimeAsset == null)
            {
                Debug.LogError($"Failed to set value of shared variable '{Name}'. Variable was not properly initialized and is missing its source of truth.");
                return;
            }

            m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);

            if (this == variable)
            {
                bool valueChanged = !EqualityComparer<DataType>.Default.Equals(m_Value, newValue);
                if (!valueChanged)
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

        public void RegisterValueChangedCallback()
        {
            bool found = m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);
            if (found == false || this == variable)
            {
                return;
            }

            variable.OnValueChanged -= InvokeValueChanged;
            variable.OnValueChanged += InvokeValueChanged;
        }

        public void UnregisterValueChangedCallback()
        {
            bool found = m_SharedVariablesRuntimeAsset.Blackboard.GetVariable(GUID, out BlackboardVariable<DataType> variable);
            if (found == false || this == variable)
            {
                return;
            }

            variable.OnValueChanged -= InvokeValueChanged;
        }
    }

    internal interface ISharedBlackboardVariable
    {
        /// <summary>
        /// Set the shared variables runtime asset.
        /// </summary>
        /// <param name="globalVariablesRuntimeAsset"></param>
        void SetSharedVariablesRuntimeAsset(RuntimeBlackboardAsset globalVariablesRuntimeAsset);
        
        /// <summary>
        /// Register OnValueChanged callback to listen for source variable value changes.
        /// </summary>
        void RegisterValueChangedCallback();

        /// <summary>
        /// Unregister OnValueChanged callback.
        /// </summary>
        void UnregisterValueChangedCallback();
    }
}
