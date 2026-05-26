using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Navigate To Location",
        description: "Navigates a GameObject to a specified position using NavMeshAgent." +
        "\nIf NavMeshAgent is not available on the [Agent] or its children, moves the Agent using its transform.",
        story: "[Agent] navigates to [Location]",
        category: "Action/Navigation",
        id: "c67c5c55de9fe94897cf61976250cc83")]
    internal partial class NavigateToLocationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> Location;
        [SerializeReference] public BlackboardVariable<float> Speed = new BlackboardVariable<float>(1.0f);
        [SerializeReference] public BlackboardVariable<float> DistanceThreshold = new BlackboardVariable<float>(0.2f);
        [SerializeReference] public BlackboardVariable<string> AnimatorSpeedParam = new BlackboardVariable<string>("SpeedMagnitude");

        // This will only be used in movement without a navigation agent.
        [SerializeReference] public BlackboardVariable<float> SlowDownDistance = new BlackboardVariable<float>(1.0f);
        [Tooltip("(NavMeshAgent only) If true, the node returns Failure when the agent cannot reach the destination (e.g. unreachable position or outside navmesh bounds). If false, returns Success.")]
        [SerializeReference] public BlackboardVariable<bool> FailIfUnreachable = new(true);

        private NavMeshAgent m_NavMeshAgent;
        private Animator m_Animator;
        private Vector3 m_LastLocationPosition;
        [CreateProperty] private float m_OriginalStoppingDistance = -1f;
        [CreateProperty] private float m_OriginalSpeed = -1f;
        private float m_CurrentSpeed;

        protected override Status OnStart()
        {
            if (Agent.Value == null || Location.Value == null)
            {
                return Status.Failure;
            }

            return Initialize();
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null || Location.Value == null)
            {
                return Status.Failure;
            }

            Vector3 agentPosition, locationPosition;
            float distance = GetDistanceToLocation(out agentPosition, out locationPosition);

            // Check if the location has changed.
            bool locationChanged = m_LastLocationPosition != locationPosition;

            if (locationChanged)
            {
                m_LastLocationPosition = locationPosition;
            }

            bool destinationReached = distance <= DistanceThreshold;

            if (destinationReached && (m_NavMeshAgent == null || !m_NavMeshAgent.pathPending))
            {
                return Status.Success;
            }
            else if (m_NavMeshAgent == null) // transform-based movement
            {
                m_CurrentSpeed = NavigationUtility.SimpleMoveTowardsLocation(Agent.Value.transform, locationPosition, Speed, distance, SlowDownDistance);
            }
            else
            {
                if (locationChanged)
                {
                    m_NavMeshAgent.SetDestination(locationPosition);
                }
                else if (!m_NavMeshAgent.pathPending && !m_NavMeshAgent.hasPath && m_NavMeshAgent.pathStatus == NavMeshPathStatus.PathPartial)
                {
                    // Agent cannot come closer to the target (out of navmesh bound)
                    return FailIfUnreachable.Value ? Status.Failure : Status.Success;
                }
            }

            UpdateAnimatorSpeed();

            return Status.Running;
        }

        protected override void OnEnd()
        {
            UpdateAnimatorSpeed(0f);

            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.ResetPath();
                }
                m_NavMeshAgent.speed = m_OriginalSpeed;
                m_NavMeshAgent.stoppingDistance = m_OriginalStoppingDistance;
            }

            m_NavMeshAgent = null;
            m_Animator = null;
        }

        protected override void OnDeserialize()
        {
            // If using a navigation mesh, we need to reset default value before Initialize.
            m_NavMeshAgent = Agent.Value.GetComponentInChildren<NavMeshAgent>();
            if (m_NavMeshAgent != null)
            {
                if (m_OriginalSpeed >= 0f)
                    m_NavMeshAgent.speed = m_OriginalSpeed;
                if (m_OriginalStoppingDistance >= 0f)
                    m_NavMeshAgent.stoppingDistance = m_OriginalStoppingDistance;

                m_NavMeshAgent.Warp(Agent.Value.transform.position);
            }

            Initialize();
        }

        private Status Initialize()
        {
            float distance = GetDistanceToLocation(out Vector3 agentPosition, out Vector3 locationPosition);
            m_LastLocationPosition = locationPosition;

            if (distance <= DistanceThreshold)
            {
                return Status.Success;
            }

            // If using a navigation mesh, set target position for navigation mesh agent.
            m_NavMeshAgent = Agent.Value.GetComponentInChildren<NavMeshAgent>();
            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.ResetPath();
                }

                m_OriginalSpeed = m_NavMeshAgent.speed;
                m_NavMeshAgent.speed = Speed;
                m_OriginalStoppingDistance = m_NavMeshAgent.stoppingDistance;
                m_NavMeshAgent.stoppingDistance = DistanceThreshold;
                m_NavMeshAgent.SetDestination(locationPosition);
            }

            m_Animator = Agent.Value.GetComponentInChildren<Animator>();
            UpdateAnimatorSpeed(0f);

            return Status.Running;
        }

        private float GetDistanceToLocation(out Vector3 agentPosition, out Vector3 locationPosition)
        {
            agentPosition = Agent.Value.transform.position;
            locationPosition = Location.Value;
            return Vector3.Distance(new Vector3(agentPosition.x, locationPosition.y, agentPosition.z), locationPosition);
        }

        private void UpdateAnimatorSpeed(float explicitSpeed = -1)
        {
            NavigationUtility.UpdateAnimatorSpeed(m_Animator, AnimatorSpeedParam, m_NavMeshAgent, m_CurrentSpeed, explicitSpeed: explicitSpeed);
        }
    }
}
