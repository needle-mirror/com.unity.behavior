using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Monitors conditions and aborts lower-priority siblings when conditions become true,
    /// causing the parent composite to restart and re-evaluate from the beginning.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Priority Abort",
        description: "Monitors conditions and aborts execution when triggered, forcing the parent composite to re-evaluate from the beginning.",
        category: "Flow/Abort",
        id: "7e2a9f5c8b3d4a1e6f9c2d8a4b7e3f1a")]
    internal partial class ObserverAbortModifier : Modifier, IObserverAbort
    {
        [SerializeReference]
        protected List<Condition> m_Conditions = new List<Condition>();
        public List<Condition> Conditions { get => m_Conditions; set => m_Conditions = value; }

        [SerializeField]
        protected bool m_RequiresAllConditions;
        public bool RequiresAllConditions { get => m_RequiresAllConditions; set => m_RequiresAllConditions = value; }

        /// <summary>
        /// The observer behavior type for this node.
        /// PriorityGuard supports LowerPriority and Both (not None or Self).
        /// </summary>
        [SerializeField]
        protected ObserverAbortTarget m_AbortTarget = ObserverAbortTarget.LowerPriority;

        public ObserverAbortTarget AbortTarget
        {
            get => m_AbortTarget;
            set => m_AbortTarget = value;
        }

        /// <summary>
        /// Checks if this observer should trigger interruption of lower-priority siblings.
        /// PriorityGuard is designed for LowerPriority observation.
        /// </summary>
        /// <returns>True if observer conditions are met and lower-priority siblings should be interrupted.</returns>
        public bool EvaluateObserver()
        {
            return ConditionUtils.CheckConditions(Conditions, RequiresAllConditions);
        }
        
        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (Child == null)
            {
                LogFailure("No child to run.");
                return Status.Failure;
            }

            foreach (Condition condition in Conditions)
            {
                condition.OnStart();
            }

            // Note: The node itself doesn't handle the abort.
            // Instead, the IObserverAbort condition will be evaluated at the next graph tick.
            // However, if observing Self/Both and conditions are met, we early out to prevent child from starting.
            if ((m_AbortTarget == ObserverAbortTarget.Self || m_AbortTarget == ObserverAbortTarget.Both)
                && !ConditionUtils.CheckConditions(Conditions, RequiresAllConditions))
            {
                return Status.Running;
            }

            Status status = StartNode(Child);
            if (status == Status.Success)
                return Status.Success;
            if (status == Status.Failure)
                return Status.Failure;
            return Status.Running;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            // Monitor child status
            Status status = Child.CurrentStatus;
            if (status == Status.Success)
                return Status.Success;
            if (status == Status.Failure)
                return Status.Failure;

            return Status.Running;
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

