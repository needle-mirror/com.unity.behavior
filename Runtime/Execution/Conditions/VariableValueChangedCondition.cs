using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Variable Value Changed",
        category: "Variable Conditions",
        story: "[Variable] has changed",
        id: "81244bae408bf0ba83e9723fe4be4299")]
    internal partial class VariableValueChangedCondition : Condition, IConditionSerializationCallbackReceiver
    {
        [SerializeReference] public BlackboardVariable Variable;
        [CreateProperty] private bool m_HasVariableChanged;
        // Need to store the running state in case the condition is deserialized while running.
        // This is usually not needed for conditions, but this node need to handle registration of the listener.
        [CreateProperty] private bool m_IsRunning = false;
        // Variable reference cannot change at runtime, so we can cache the listener registration state.
        private bool m_HasRegisteredListener = false;

        public override bool IsTrue()
        {
            if (!m_HasVariableChanged)
            {
                return false;
            }

            m_HasVariableChanged = false;
            return true;
        }

        public override void OnStart()
        {
            m_IsRunning = true;
            m_HasVariableChanged = false;
            RegisterListener();
        }

        public override void OnEnd()
        {
            m_IsRunning = false;
        }

        public void OnSerialize()
        { }

        public void OnDeserialize()
        {
            // There is no risk of double registration here as deserialization creates a new instance of the class.
            m_HasRegisteredListener = false;
            RegisterListener();
        }

        private void OnVariableValueChange()
        {
            // Early exit if condition is not running
            // This is done to avoid listener registration GC cost.
            if (!m_IsRunning)
            {
                return;
            }

            m_HasVariableChanged = true;
        }

        private void RegisterListener()
        {
            if (m_HasRegisteredListener || Variable == null)
            {
                return;
            }

            // Note: We don't need to unregister as Condition and Variable have similar lifetimes.
            Variable.OnValueChanged += OnVariableValueChange;
            m_HasRegisteredListener = true;
        }
    }
}
