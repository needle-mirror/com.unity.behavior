using System;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    internal class ComponentToComponentBlackboardVariable<SourceType, TargetType> : BlackboardVariableCaster<SourceType, TargetType>
        where SourceType : Component where TargetType : Component
    {
        protected override SourceType GetSourceObjectFromTarget(TargetType value)
        {
            if (value is SourceType sourceType)
            {
                return sourceType;
            }
            else
            {
                return value.GetComponent<SourceType>();
            }
        }

        protected override TargetType GetTargetObjectFromSource(SourceType variable)
        {
            if (variable is TargetType targetType)
            {
                return targetType;
            }
            else
            {
                return variable.gameObject.GetComponent<TargetType>();
            }
        }

        // Required for serialization
        public ComponentToComponentBlackboardVariable() { }

        public ComponentToComponentBlackboardVariable(BlackboardVariable<SourceType> linkedVariable)
            : base(linkedVariable)
        { }
    }
}
