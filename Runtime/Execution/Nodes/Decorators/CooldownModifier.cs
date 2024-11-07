using System;
using UnityEngine;
using Unity.Behavior;
using Unity.Properties;
using Modifier = Unity.Behavior.Modifier;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Cooldown",
        description: "Imposes a mandatory wait time between executions to regulate action frequency.",
        story: "Cooldowns for [duration] seconds after execution",
        category: "Flow",
        id: "a9ff45a058927aa68b4328a5daf34161")]
    internal partial class CooldownModifier : Modifier
    {
        [SerializeReference] public BlackboardVariable<float> Duration;
        [CreateProperty] private float m_CooldownRemainingTime;
        private float m_CooldownEndTime;

        protected override Status OnStart()
        {
            if (Time.time < m_CooldownEndTime)
            {
                return Status.Failure;
            }

            m_CooldownEndTime = Time.time + Duration.Value;

            if (Child == null)
            {
                return Status.Success;
            }

            var status = StartNode(Child);
            if (status == Status.Running)
            {
                return Status.Waiting;
            }
            return status;
        }

        protected override Status OnUpdate()
        {
            var status = Child.CurrentStatus;
            if (status == Status.Running)
            {
                return Status.Waiting;
            }
            // Set the cooldown again because the child was still running and we only want to set it after it finished.
            m_CooldownEndTime = Time.time + Duration.Value;
            return status;
        }

        protected override void OnSerialize()
        {
            m_CooldownRemainingTime = m_CooldownEndTime - Time.time;
        }

        protected override void OnDeserialize()
        {
            m_CooldownEndTime = Time.time + m_CooldownRemainingTime;
        }
    }
}