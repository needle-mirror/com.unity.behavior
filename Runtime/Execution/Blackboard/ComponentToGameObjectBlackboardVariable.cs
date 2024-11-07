using System;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    internal class ComponentToGameObjectBlackboardVariable<SourceType> : BlackboardVariableCaster<SourceType, GameObject>
        where SourceType : Component
    {
        protected override SourceType GetSourceObjectFromTarget(GameObject value) => value != null ? value.GetComponent<SourceType>() : null;
        protected override GameObject GetTargetObjectFromSource(SourceType variable) => variable != null ? variable.gameObject : null;

        // Required for serialization
        public ComponentToGameObjectBlackboardVariable() { }

        public ComponentToGameObjectBlackboardVariable(BlackboardVariable<SourceType> linkedVariable)
            : base(linkedVariable)
        { }
    }
}