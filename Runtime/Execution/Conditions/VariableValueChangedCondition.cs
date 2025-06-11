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
            RegisterListener();
        }

        public override void OnEnd()
        {
            Variable.OnValueChanged -= OnVariableValueChange;
        }

        public void OnSerialize() 
        { }

        public void OnDeserialize()
        {
            RegisterListener();
        }

        private void OnVariableValueChange()
        {
            m_HasVariableChanged = true;
        }

        private void RegisterListener()
        {
            if (Variable == null)
            {
                return;
            }
            
            m_HasVariableChanged = false;
            Variable.OnValueChanged -= OnVariableValueChange;
            Variable.OnValueChanged += OnVariableValueChange;
        }
    }
}
