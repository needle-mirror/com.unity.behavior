using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Set Animator Integer",
        description: "Sets an integer parameter on an animator to a specific value.",
        story: "Set [Parameter] in [Animator] to [Value]",
        category: "Action/Animation",
        id: "33f8bb6a3f9bc4606c6613be45ad704f")]
    internal partial class SetAnimatorIntAction : Action
    {
        [SerializeReference] public BlackboardVariable<string> Parameter;
        [SerializeReference] public BlackboardVariable<Animator> Animator;
        [SerializeReference] public BlackboardVariable<int> Value;

        protected override Status OnStart()
        {
            if (Animator.Value == null)
            {
                LogFailure("No Animator set.");
                return Status.Failure;
            }

            Animator.Value.SetInteger(Parameter.Value, Value.Value);
            return Status.Success;
        }
    }
}
