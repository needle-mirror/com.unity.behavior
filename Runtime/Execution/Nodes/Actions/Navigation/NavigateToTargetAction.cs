using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Navigate To Target",
        description: "Navigates a GameObject towards another GameObject using NavMeshAgent." +
        "\nIf NavMeshAgent is not available on the [Agent] or its children, moves the Agent using its transform.",
        story: "[Agent] navigates to [Target]",
        category: "Action/Navigation",
        id: "3bc19d3122374cc9a985d90351633310")]
    internal partial class NavigateToTargetAction : Action
    {
        public enum TargetPositionMode
        {
            ClosestPointOnAnyCollider,      // Use the closest point on any collider, including child objects
            ClosestPointOnTargetCollider,   // Use the closest point on the target's own collider only
            ExactTargetPosition             // Use the exact position of the target, ignoring colliders
        }

        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> Speed = new BlackboardVariable<float>(1.0f);
        [SerializeReference] public BlackboardVariable<float> DistanceThreshold = new BlackboardVariable<float>(0.2f);
        [SerializeReference] public BlackboardVariable<string> AnimatorSpeedParam = new BlackboardVariable<string>("SpeedMagnitude");

        // This will only be used in movement without a navigation agent.
        [SerializeReference] public BlackboardVariable<float> SlowDownDistance = new BlackboardVariable<float>(1.0f);
        [Tooltip("Defines how the target position is determined for navigation:" +
            "\n- ClosestPointOnAnyCollider: Use the closest point on any collider, including child objects" +
            "\n- ClosestPointOnTargetCollider: Use the closest point on the target's own collider only" +
            "\n- ExactTargetPosition: Use the exact position of the target, ignoring colliders. Default if no collider is found.")]
        [SerializeReference] public BlackboardVariable<TargetPositionMode> m_TargetPositionMode = new(TargetPositionMode.ClosestPointOnAnyCollider);

        private NavMeshAgent m_NavMeshAgent;
        private Animator m_Animator;
        private Vector3 m_LastTargetPosition;
        private Vector3 m_ColliderAdjustedTargetPosition;
        [CreateProperty] private float m_OriginalStoppingDistance = -1f;
        [CreateProperty] private float m_OriginalSpeed = -1f;
        private float m_ColliderOffset;
        private float m_CurrentSpeed;

        protected override Status OnStart()
        {
            if (Agent.Value == null || Target.Value == null)
            {
                return Status.Failure;
            }

            return Initialize();
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null || Target.Value == null)
            {
                return Status.Failure;
            }

            // Check if the target position has changed.
            bool boolUpdateTargetPosition = !Mathf.Approximately(m_LastTargetPosition.x, Target.Value.transform.position.x) 
                || !Mathf.Approximately(m_LastTargetPosition.y, Target.Value.transform.position.y) 
                || !Mathf.Approximately(m_LastTargetPosition.z, Target.Value.transform.position.z);

            if (boolUpdateTargetPosition)
            {
                m_LastTargetPosition = Target.Value.transform.position;
                m_ColliderAdjustedTargetPosition = GetPositionColliderAdjusted();
            }

            float distance = GetDistanceXZ();
            bool destinationReached = distance <= (DistanceThreshold + m_ColliderOffset);
            
            if (destinationReached && (m_NavMeshAgent == null || !m_NavMeshAgent.pathPending))
            {
                return Status.Success;
            }
            else if (m_NavMeshAgent == null) // transform-based movement
            {
                m_CurrentSpeed = NavigationUtility.SimpleMoveTowardsLocation(Agent.Value.transform, m_ColliderAdjustedTargetPosition,
                    Speed, distance, SlowDownDistance);
            }
            else if (boolUpdateTargetPosition) // navmesh-based destination update (if needed)
            {
                m_NavMeshAgent.SetDestination(m_ColliderAdjustedTargetPosition);
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
            m_LastTargetPosition = Target.Value.transform.position;
            m_ColliderAdjustedTargetPosition = GetPositionColliderAdjusted();

            // Add the extents of the colliders to the stopping distance.
            m_ColliderOffset = 0.0f;
            Collider agentCollider = Agent.Value.GetComponentInChildren<Collider>();
            if (agentCollider != null)
            {
                Vector3 colliderExtents = agentCollider.bounds.extents;
                m_ColliderOffset += Mathf.Max(colliderExtents.x, colliderExtents.z);
            }

            if (GetDistanceXZ() <= (DistanceThreshold + m_ColliderOffset))
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
                m_NavMeshAgent.stoppingDistance = DistanceThreshold + m_ColliderOffset;
                m_NavMeshAgent.SetDestination(m_ColliderAdjustedTargetPosition);
            }

            m_Animator = Agent.Value.GetComponentInChildren<Animator>();
            UpdateAnimatorSpeed(0f);

            return Status.Running;
        }

        private Vector3 GetPositionColliderAdjusted()
        {
            switch (m_TargetPositionMode.Value)
            {
                case TargetPositionMode.ClosestPointOnAnyCollider:
                    Collider anyCollider = Target.Value.GetComponentInChildren<Collider>(includeInactive: false);
                    if (anyCollider == null || anyCollider.enabled == false) 
                        break;
                    return anyCollider.ClosestPoint(Agent.Value.transform.position);
                case TargetPositionMode.ClosestPointOnTargetCollider:
                    Collider targetCollider = Target.Value.GetComponent<Collider>();
                    if (targetCollider == null || targetCollider.enabled == false) 
                        break;
                    return targetCollider.ClosestPoint(Agent.Value.transform.position);
            }

            // Default to target position.
            return Target.Value.transform.position;
        }

        private float GetDistanceXZ()
        {
            Vector3 agentPosition = new Vector3(Agent.Value.transform.position.x, m_ColliderAdjustedTargetPosition.y, Agent.Value.transform.position.z);
            return Vector3.Distance(agentPosition, m_ColliderAdjustedTargetPosition);
        }

        private void UpdateAnimatorSpeed(float explicitSpeed = -1)
        {
            NavigationUtility.UpdateAnimatorSpeed(m_Animator, AnimatorSpeedParam, m_NavMeshAgent, m_CurrentSpeed, explicitSpeed: explicitSpeed);
        }
    }
}
