using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Properties;

namespace Unity.Behavior
{
    /// <summary>
    /// Restarts child node execution on given conditions.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Restart",
        description: "Restarts branch when assigned conditions are true.",
        category: "Flow/Abort",
        id: "4d0888f06af04abd987e4b7d61f72e36")]
    internal partial class RestartModifier : Modifier, IObserverAbort
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
        /// </summary>
        /// <returns>True if observer conditions are met and lower-priority siblings should be interrupted.</returns>
        public bool EvaluateObserver()
        {
            return ConditionUtils.CheckConditions(Conditions, RequiresAllConditions);
        }

        protected override Status OnStart()
        {
            base.OnStart();

            if (Child == null)
            {
                return Status.Failure;
            }

            foreach (Condition condition in Conditions)
            {
                condition.OnStart();
            }

            Status status = StartNode(Child);
            if (status == Status.Success)
                return Status.Success;
            if (status == Status.Failure)
                return Status.Failure;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (ConditionUtils.CheckConditions(Conditions, RequiresAllConditions))
            {
                EndNodesAndRestart();
                return Status.Running;
            }
            // Check the child status
            Status status = Child.CurrentStatus;
            if (status == Status.Success)
                return Status.Success;
            if (status == Status.Failure)
                return Status.Failure;

            return Status.Running;
        }

        private void EndNodesAndRestart()
        {
            Graph.EndNode(Child);
            Graph.StartNode(Child);

            // Reset the conditions
            foreach (Condition condition in Conditions)
            {
                condition.OnEnd();
                condition.OnStart();
            }
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
