using UnityEngine;
using UnityEngine.AI;

namespace Unity.Behavior
{
    /// <summary>
    /// Utility class for common navigation operations that can be used across behavior action nodes
    /// </summary>
    internal static class NavigationUtility
    {
        /// <summary>
        /// Updates an animator parameter based on agent movement speed
        /// </summary>
        /// <param name="animator">The animator component to update</param>
        /// <param name="speedParameterName">Name of the speed parameter in the animator</param>
        /// <param name="navMeshAgent">Optional NavMeshAgent component</param>
        /// <param name="currentSpeed">Current calculated speed (used when navMeshAgent is null)</param>
        /// <param name="minSpeedThreshold">The minimum speed threshold - any calculated speed at or below this value will be set to zero.
        /// This helps eliminate animator jitter when the agent is nearly stationary or making very minor adjustments.</param>
        /// <param name="explicitSpeed">Optional explicit speed value to set (-1 means use movement speed)</param>
        /// <returns>True if animator was updated, false otherwise</returns>
        public static bool UpdateAnimatorSpeed(Animator animator, string speedParameterName, NavMeshAgent navMeshAgent, float currentSpeed, float minSpeedThreshold = 0.1f, 
            float explicitSpeed = -1f)
        {
            if (animator == null || string.IsNullOrEmpty(speedParameterName))
            {
                return false;
            }

            float speedValue = 0;
            if (explicitSpeed >= 0)
            {
                speedValue = explicitSpeed;
            }
            else if (navMeshAgent != null)
            {
                speedValue = navMeshAgent.velocity.magnitude;
            }
            else
            {
                speedValue = currentSpeed;
            }

            if (speedValue <= minSpeedThreshold)
            {
                speedValue = 0;
            }

            animator.SetFloat(speedParameterName, speedValue);
            return true;
        }

        /// <summary>
        /// Moves a transform towards a target position with optional slowdown near destination
        /// </summary>
        /// <param name="agentTransform">The transform to move</param>
        /// <param name="targetLocation">The target position to move towards</param>
        /// <param name="speed">Maximum movement speed</param>
        /// <param name="distance">Current distance to target</param>
        /// <param name="slowDownDistance">Distance at which to begin slowing down (0 for no slowdown)</param>
        /// <param name="minSpeedRatio">Minimum speed ratio when slowing down (0.1 = 10% of max speed)</param>
        /// <returns>Actual speed used for movement</returns>
        public static float SimpleMoveTowardsLocation(Transform agentTransform, Vector3 targetLocation, float speed, float distance, float slowDownDistance = 0.0f,
            float minSpeedRatio = 0.1f)
        {
            if (agentTransform == null)
            {
                return 0f;
            }

            Vector3 agentPosition = agentTransform.position;
            float movementSpeed = speed;

            // Slowdown
            if (slowDownDistance > 0.0f && distance < slowDownDistance)
            {
                float ratio = distance / slowDownDistance;
                movementSpeed = Mathf.Max(speed * minSpeedRatio, speed * ratio);
            }

            Vector3 toDestination = targetLocation - agentPosition;
            toDestination.y = 0.0f;

            if (toDestination.sqrMagnitude > 0.0001f)
            {
                toDestination.Normalize();

                // Apply movement
                agentPosition += toDestination * (movementSpeed * Time.deltaTime);
                agentTransform.position = agentPosition;

                // Look at the target
                agentTransform.forward = toDestination;
            }

            return movementSpeed;
        }


    }
}
