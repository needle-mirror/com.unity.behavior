using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Set Animator Trigger",
        description: "Sets a trigger on an animator.",
        story: "Set animation trigger [Trigger] in [Animator] to: [TriggerState]",
        category: "Action/Animation",
        id: "554af2e866043e86bebd186cdf350589")]
    internal partial class SetAnimatorTriggerAction : Action
    {
        [SerializeReference] public BlackboardVariable<string> Trigger;
        [SerializeReference] public BlackboardVariable<Animator> Animator;
        [SerializeReference] public BlackboardVariable<bool> TriggerState;

        protected override Status OnStart()
        {
            if (Animator.Value == null)
            {
                LogFailure("No Animator set.");
                return Status.Failure;
            }

            if (TriggerState.Value)
            {
                Animator.Value.SetTrigger(Trigger.Value);
            }
            else
            {
                Animator.Value.ResetTrigger(Trigger.Value);
            }

            return Status.Success;
        }
    }
}
