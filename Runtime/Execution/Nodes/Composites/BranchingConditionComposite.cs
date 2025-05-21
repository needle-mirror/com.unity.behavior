using System;
using System.Collections.Generic;
using Unity.Behavior.Serialization;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Chooses one branch depending if the condition is true or false.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Conditional Branch",
        description: "Redirects the flow to the appropriate branch based on whether the condition is true or false.",
        category: "Flow/Conditional",
        id: "15fd229322992eabc0f7186e51eabcca")]
    internal partial class BranchingConditionComposite : Composite, IConditional
    {
        [SerializeReference]
        protected List<Condition> m_Conditions = new List<Condition>();
        public List<Condition> Conditions { get => m_Conditions; set => m_Conditions = value; }

        [SerializeField]
        protected bool m_RequiresAllConditions;
        public bool RequiresAllConditions { get => m_RequiresAllConditions; set => m_RequiresAllConditions = value; }

        /// <summary>
        /// The node that is executed if the condition is true.
        /// </summary>
        [SerializeReference] public Node True;

        /// <summary>
        /// The node that is executed if the condition is false.
        /// </summary>
        [SerializeReference] public Node False;

        [SerializeReference] private Node m_CurrentChild;
        [CreateProperty, DontSerialize]
        public Node CurrentChild { get => m_CurrentChild; internal set => m_CurrentChild = value; }

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (Conditions.Count == 0)
            {
                return Status.Failure;
            }

            Status status;

            foreach (Condition condition in Conditions)
            {
                condition.OnStart();
            }

            if (ConditionUtils.CheckConditions(Conditions, RequiresAllConditions))
            {
                if (True == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("Branching condition evaluated as true, but there is no child to run. Ignore this warning if this is intentional.");
#endif
                    return Status.Success;
                }
                m_CurrentChild = True;
                status = StartNode(True);
            }
            else
            {
                if (False == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("Branching condition evaluated as false, but there is no child to run. Ignore this warning if this is intentional.");
#endif
                    return Status.Success;
                }
                m_CurrentChild = False;
                status = StartNode(False);
            }

            return status switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Waiting
            };
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            return m_CurrentChild.CurrentStatus;
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