using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Patrol",
        description: "Moves a GameObject along way points (transform children of a GameObject) using NavMeshAgent." +
        "\nIf NavMeshAgent is not available on the [Agent] or its children, moves the Agent using its transform.",
        category: "Action/Navigation",
        story: "[Agent] patrols along [Waypoints]",
        id: "f0cd1414cf8e67c47214e54fc922c793")]
    internal partial class PatrolAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<List<GameObject>> Waypoints;
        [SerializeReference] public BlackboardVariable<float> Speed;
        [SerializeReference] public BlackboardVariable<float> WaypointWaitTime = new BlackboardVariable<float>(1.0f);
        [SerializeReference] public BlackboardVariable<float> DistanceThreshold = new BlackboardVariable<float>(0.2f);
        [SerializeReference] public BlackboardVariable<string> AnimatorSpeedParam = new BlackboardVariable<string>("SpeedMagnitude");
        [Tooltip("Should patrol restart from the latest point?")]
        [SerializeReference] public BlackboardVariable<bool> PreserveLatestPatrolPoint = new (false);

        private NavMeshAgent m_NavMeshAgent;
        private Animator m_Animator;
        private float m_PreviousStoppingDistance;
        
        [CreateProperty]
        private Vector3 m_CurrentTarget;
        [CreateProperty]
        private int m_CurrentPatrolPoint = 0;
        [CreateProperty]
        private bool m_Waiting;
        [CreateProperty]
        private float m_WaypointWaitTimer;

        protected override Status OnStart()
        {
            if (Agent.Value == null)
            {
                LogFailure("No agent assigned.");
                return Status.Failure;
            }

            if (Waypoints.Value == null || Waypoints.Value.Count == 0)
            {
                LogFailure("No waypoints to patrol assigned.");
                return Status.Failure;
            }

            Initialize();

            m_Waiting = false;
            m_WaypointWaitTimer = 0.0f;

            MoveToNextWaypoint();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null || Waypoints.Value == null)
            {
                return Status.Failure;
            }

            if (m_Waiting)
            {
                if (m_WaypointWaitTimer > 0.0f)
                {
                    m_WaypointWaitTimer -= Time.deltaTime;
                }
                else
                {
                    m_WaypointWaitTimer = 0f;
                    m_Waiting = false;
                    MoveToNextWaypoint();
                }
            }
            else
            {
                float distance = GetDistanceToWaypoint();
                Vector3 agentPosition = Agent.Value.transform.position;
                
                // If we are using navmesh, get the animator speed out of the velocity.
                if (m_Animator != null && m_NavMeshAgent != null)
                {
                    m_Animator.SetFloat(AnimatorSpeedParam, m_NavMeshAgent.velocity.magnitude);
                }

                if (distance <= DistanceThreshold)
                {
                    if (m_Animator != null)
                    {
                        m_Animator.SetFloat(AnimatorSpeedParam, 0);
                    }

                    m_WaypointWaitTimer = WaypointWaitTime.Value;
                    m_Waiting = true;
                }
                else if (m_NavMeshAgent == null)
                {
                    float speed = Mathf.Min(Speed, distance);

                    Vector3 toDestination = m_CurrentTarget - agentPosition;
                    toDestination.y = 0.0f;
                    toDestination.Normalize();
                    agentPosition += toDestination * (speed * Time.deltaTime);
                    Agent.Value.transform.position = agentPosition;
                    Agent.Value.transform.forward = toDestination;
                }
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (m_Animator != null)
            {
                m_Animator.SetFloat(AnimatorSpeedParam, 0);
            }

            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.ResetPath();
                }
                m_NavMeshAgent.stoppingDistance = m_PreviousStoppingDistance;
            }
        }

        protected override void OnDeserialize()
        {
            Initialize();
        }

        private void Initialize()
        {
            m_Animator = Agent.Value.GetComponentInChildren<Animator>();
            if (m_Animator != null)
            {
                m_Animator.SetFloat(AnimatorSpeedParam, 0);
            }

            m_NavMeshAgent = Agent.Value.GetComponentInChildren<NavMeshAgent>();
            if (m_NavMeshAgent != null)
            {
                if (m_NavMeshAgent.isOnNavMesh)
                {
                    m_NavMeshAgent.ResetPath();
                }
                m_NavMeshAgent.speed = Speed.Value;
                m_PreviousStoppingDistance = m_NavMeshAgent.stoppingDistance;
                m_NavMeshAgent.stoppingDistance = DistanceThreshold;
            }

            m_CurrentPatrolPoint = PreserveLatestPatrolPoint.Value ? m_CurrentPatrolPoint - 1 : -1;
        }

        private float GetDistanceToWaypoint()
        {
            if (m_NavMeshAgent != null)
            {
                return m_NavMeshAgent.remainingDistance;
            }

            Vector3 targetPosition = m_CurrentTarget;
            Vector3 agentPosition = Agent.Value.transform.position;
            agentPosition.y = targetPosition.y; // Ignore y for distance check.
            return Vector3.Distance(
                agentPosition,
                targetPosition
            );
        }

        private void MoveToNextWaypoint()
        {
            m_CurrentPatrolPoint = (m_CurrentPatrolPoint + 1) % Waypoints.Value.Count;            

            m_CurrentTarget = Waypoints.Value[m_CurrentPatrolPoint].transform.position;
            if (m_NavMeshAgent != null)
            {
                m_NavMeshAgent.SetDestination(m_CurrentTarget);
            }
            else if (m_Animator != null)
            {
                // We set the animator speed once if we are using transform.
                m_Animator.SetFloat(AnimatorSpeedParam, Speed.Value);
            }
        }
    }
}
