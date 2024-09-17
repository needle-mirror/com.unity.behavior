using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Play Particle System",
        story: "Play [ParticleSystem] on [Target]",
        category: "Action/Resource",
        id: "45b0abd81036c6ba1089944eb166dc54",
        description: "Plays a ParticleSystem at the Target location. " +
        "\nThe ParticleSystems are internally pooled and reused when possible.")]
    internal partial class PlayParticleSystemAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> ParticleSystem;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [Tooltip("Should the spawned system copy the target rotation?")]
        [SerializeReference] public BlackboardVariable<bool> CopyRotation = new BlackboardVariable<bool>(true);
        [SerializeReference] public BlackboardVariable<Vector3> PositionOffset = new BlackboardVariable<Vector3>(Vector3.zero);
        [Tooltip("Time between each recycling attempt of a spawmed particle system. " +
            "Lower values can be usefull to recycle short bursts of particles more often.")]
        [SerializeReference] public BlackboardVariable<float> RecyclingFrequency = new BlackboardVariable<float>(1);
        [Tooltip("[Out Value] This field is assigned with the spawned particle system's gameObject.")]
        [SerializeReference] public BlackboardVariable<GameObject> InstantiatedObject;

        private Stack<ParticleSystem> m_PrivatePool = null;
        private List<ParticleSystem> m_ChildSystemsBuffer = new List<ParticleSystem>(4);

        private ParticleSystem ParticleSystemComponent => ParticleSystem.Value.GetComponent<ParticleSystem>();

        protected override Status OnStart()
        {
            if (Target.Value == null)
            {
                LogFailure("No target assigned to play the ParticleSystem.");
                return Status.Failure;
            }
            if (ParticleSystem.Value == null || ParticleSystemComponent == null)
            {
                LogFailure("No valid ParticleSystem assigned.");
                return Status.Failure;
            }

            ParticleSystem vfx = null;
            if (m_PrivatePool != null && m_PrivatePool.Count > 0)
            {
                do
                {
                    vfx = m_PrivatePool.Pop();
                } while (vfx == null && m_PrivatePool.Count > 0);

                if (vfx)
                {
                    vfx.gameObject.SetActive(true);
                }
            }

            if (vfx == null)
            {
                vfx = GameObject.Instantiate(ParticleSystemComponent);
                vfx.gameObject.name = "VFX: " + ParticleSystem.Value.name;
            }

            var targetTransform = Target.Value.transform;
            vfx.transform.SetPositionAndRotation(targetTransform.position + PositionOffset,
                    CopyRotation.Value ? targetTransform.rotation : Quaternion.identity);
#if UNITY_EDITOR
            vfx.gameObject.name = "VFX: " + ParticleSystem.Value.name;
#endif

            if (!vfx.isPlaying)
            {
                vfx.Play();
            }

            InstantiatedObject.Value = vfx.gameObject;

            // If the particle system loop, we don't try to recycle it.
            if (vfx.main.loop)
            {
                return Status.Success;
            }

            vfx.GetComponentsInChildren(false, m_ChildSystemsBuffer);
            foreach (ParticleSystem child in m_ChildSystemsBuffer)
            {
                if (child.main.loop)
                {
                    return Status.Success;
                }
            }

            // If no looping system, try to recycle every second.
            Awaitable_ReleaseParticleSystem(vfx);

            return Status.Success;
        }

        private async void Awaitable_ReleaseParticleSystem(ParticleSystem system)
        {
            // System is invalid or has been destroyed.
            if (system == null)
            {
                return;
            }

            var childSystems = system.GetComponentsInChildren<ParticleSystem>(false);

            do
            {
                await Awaitable.WaitForSecondsAsync(RecyclingFrequency.Value);
            }
            while (AnySystemRunning());

            // System is invalid or has been destroyed.
            if (system == null)
            {
                return;
            }

            system.gameObject.SetActive(false);

            if (m_PrivatePool == null)
            {
                m_PrivatePool = new Stack<ParticleSystem>();
            }

            m_PrivatePool.Push(system);

            bool AnySystemRunning()
            {
                // System is invalid or has been destroyed.
                if (system == null)
                {
                    return false;
                }

                foreach (var child in childSystems)
                {
                    if (child.isPlaying)
                    {
                        return true;
                    }
                }

                return system.isPlaying;
            }
        }
    }
}
