using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Wait (Frames)",
    description: "Waits for a specified number of frames.",
    story: "Wait for [NumFrames] Frames",
    category: "Action",
    id: "2292810d494a4c941a71cbe358e46920")]
internal partial class WaitFramesAction : Action
{
    [Tooltip("The number of frames to wait.")]
    [SerializeReference] public BlackboardVariable<int> NumFrames = new(1);
    private int m_FrameTarget;
    [CreateProperty] private int m_FramesRemaining;

    protected override Status OnStart()
    {
        int currentFrame = Time.frameCount; 
        m_FrameTarget = currentFrame + Mathf.Max(0, NumFrames.Value);
        if (currentFrame >= m_FrameTarget)
        {
            return Status.Success;
        }
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        int currentFrame = Time.frameCount;
        if (currentFrame >= m_FrameTarget)
        {
            return Status.Success;
        }
        return Status.Running;
    }
    
    protected override void OnSerialize()
    {
        m_FramesRemaining = m_FrameTarget - Time.frameCount;
    }

    protected override void OnDeserialize()
    {
        m_FrameTarget = Time.frameCount + m_FramesRemaining;
    }
}

