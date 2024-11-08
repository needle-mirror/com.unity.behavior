using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Repeats the branch while the condition is true.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Repeat While", 
        description: "Repeats the flow underneath as long as the specified condition(s) are true.",
        category: "Flow",
        hideInSearch: true,
        icon: "Icons/repeat_until_change",
        id: "bcd62844ac1b14f074e31df34956441a")]
    internal partial class RepeatWhileConditionModifier : Modifier, IConditional
    {
        [SerializeReference]
        protected List<Condition> m_Conditions = new List<Condition>();
        public List<Condition> Conditions { get => m_Conditions; set => m_Conditions = value; }

        [SerializeField]
        protected bool m_RequiresAllConditions;
        public bool RequiresAllConditions { get => m_RequiresAllConditions; set => m_RequiresAllConditions = value; }

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (Child == null || Conditions.Count == 0)
            {
                return Status.Failure;
            }

            // Early out in case the condition is already filled and prevent DoWhile condition. 
            foreach (Condition condition in Conditions)
            {
                condition.OnStart();
            }

            bool conditionIsTrue = ConditionUtils.CheckConditions(Conditions, RequiresAllConditions);
            if (!conditionIsTrue)
            {
                return Status.Success;
            }

            Status childStatus = StartNode(Child);
            return (childStatus == Status.Running || childStatus == Status.Waiting) ? Status.Waiting : Status.Running;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            bool conditionIsTrue = ConditionUtils.CheckConditions(Conditions, RequiresAllConditions);
            // If condition is true, we need to restart the child
            if (conditionIsTrue)
            {
                Status childStatus = RestartChild();
                return (childStatus == Status.Running || childStatus == Status.Waiting) ? Status.Waiting : Status.Running;
            }

            return Status.Success;
        }

        private Status RestartChild()
        {
            EndNode(Child);
            return StartNode(Child);
        }
        
        protected override void OnEnd()
        {
            base.OnEnd();

            foreach (Condition condition in Conditions)
            {
                condition.OnEnd();
            }
        }
    }
}