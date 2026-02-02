using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Returns Success if the condition evaluates to true and Failure otherwise.
    /// Modifier variant that can have children and support observer abort.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Conditional Guard (Modifier)",
        description: "Allows flow to pass only if the specified condition(s) are met. Can observe and interrupt lower-priority siblings." +
        "\nReturns Status.Failure if no child node is assigned.",
        category: "Flow/Conditional",
        hideInSearch: true,
        id: "a8f3c7e9d6b84f2a9c1e5d7b3f6a8c2d")]
    internal partial class ConditionalGuardModifier : Modifier, IObserverAbort
    {
        [SerializeReference]
        protected List<Condition> m_Conditions = new List<Condition>();
        public List<Condition> Conditions { get => m_Conditions; set => m_Conditions = value; }

        [SerializeField]
        protected bool m_RequiresAllConditions;
        public bool RequiresAllConditions { get => m_RequiresAllConditions; set => m_RequiresAllConditions = value; }

        /// <summary>
        /// The observer behavior type for this node.
        /// </summary>
        [SerializeField]
        protected ObserverAbortTarget m_ObserverType = ObserverAbortTarget.None;

        public ObserverAbortTarget AbortTarget
        {
            get => m_ObserverType;
            set => m_ObserverType = value;
        }

        /// <summary>
        /// Checks if this observer should trigger interruption of lower-priority siblings.
        /// ConditionalGuard only supports LowerPriority observation (not Self or Both).
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
                return Status.Failure;
            }

            foreach (Condition condition in Conditions)
            {
                condition.OnStart();
            }

            // Check conditions first
            if (!ConditionUtils.CheckConditions(Conditions, RequiresAllConditions))
            {
                return Status.Failure;
            }

            // Conditions passed, start the child
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

