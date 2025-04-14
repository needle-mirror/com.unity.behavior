using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Repeats operation of the node.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Repeat",
        description: "Repeats operation of the node.", 
        category: "Flow",
        icon: "Icons/repeater",
        id: "ae70eb7a112b4b339e1699ebc246f1c4")]
    internal partial class RepeaterModifier : Modifier, IRepeater
    {
        [SerializeField, CreateProperty]
        private bool m_AllowMultipleRepeatsPerTick = false;
        public bool AllowMultipleRepeatsPerTick
        {
            get => m_AllowMultipleRepeatsPerTick;
            set => m_AllowMultipleRepeatsPerTick = value;
        }
        internal int m_Repeats;
        internal int m_CompletedRuns;
        private int m_CurrentFrame;
        [CreateProperty] private int m_FrameDelta;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            m_CurrentFrame = Time.frameCount;
            m_CompletedRuns = 0;
            if (Child == null)
            {
                return Status.Failure;
            }

            var status = StartNode(Child);
            if (status == Status.Failure || status == Status.Success)
                return Status.Running;

            return Status.Waiting;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            if (!AllowMultipleRepeatsPerTick && m_CurrentFrame == Time.frameCount)
            {
                return Status.Running;
            }
            m_CurrentFrame = Time.frameCount;
            Status status = Child.CurrentStatus;
            if (status == Status.Failure || status == Status.Success)
            {
                if (m_Repeats != 0 && ++m_CompletedRuns >= m_Repeats)
                    return status;

                var newStatus = StartNode(Child);
                if (newStatus == Status.Failure || newStatus == Status.Success)
                    return Status.Running;
            }
            return Status.Waiting;
        }
        
        protected override void OnDeserialize()
        {
            m_CurrentFrame = Time.frameCount + m_FrameDelta;
        }

        protected override void OnSerialize()
        {
            m_FrameDelta = Time.frameCount - m_CurrentFrame; 
        }
    }
}
