using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Instantiate Object",
        story: "Instantiate [Object]",
        description: "Instantiates a GameObject to the graph owner scene." +
        "\nThe instanciated object is stored in [InstantitedObject].",
        category: "Action/GameObject",
        id: "54f091ab6bb57615eae84c1e44203a04")]
    internal partial class InstantiateObjectAction : Action
    {
        public enum TargetMode
        {
            Position = 0,
            PositionAndRotation,
            Parent
        }

        [SerializeReference] public BlackboardVariable<GameObject> Object;
        [Tooltip("[Out Value] This field is assigned with the instantiated object.")]
        [SerializeReference] public BlackboardVariable<GameObject> InstantiatedObject;
        [SerializeReference] public BlackboardVariable<Transform> Target;
        [Tooltip("Only used when a Target transform is provided." +
            "\n<b>Position</b>: Applies only the position of the target to the instantiated object." +
            "\n<b>Position And Rotation</b>: Applies both position and rotation to the instantiated object." +
            "\n<b>Parent</b>: Sets the transform of the instantiated object as a child of the target. Its local position and rotation are reset.")]
        [SerializeReference] public BlackboardVariable<TargetMode> TargetUsage;
        [Tooltip("<b>True</b>: The node returns success after creating the InstantiateAsync request." +
            "\n<b>False</b>: The node wait for the object to be instanciated before succeeding. Supports [Runtime Serialization]." +
            "\n\n[Runtime Serialization] If the node status is waiting for the request to finish and the node is serialized, " +
            "the request will be recreated during deserialization.")]
        [SerializeReference] public BlackboardVariable<bool> RunInBackground = new BlackboardVariable<bool>(true);

        // Allows to recreate the instantiate request in case the node is deserialized (i.e. after a loading).
        [CreateProperty] private bool m_HasPendingRequest = false;
        
        private AsyncInstantiateOperation m_AsyncOperation;

        protected override Status OnStart()
        {
            if (Object.Value == null)
            {
                LogFailure("No valid object to instantiate provided.");
                return Status.Failure;
            }

            if (InstantiatedObject is IBlackboardVariableCast)
            {
                var caster = InstantiatedObject as IBlackboardVariableCast;
                LogFailure($"Invalid InstantiatedObject variable: Expecting \"GameObject\" but is \"{caster.SourceTypeName}\". Please provide a valid GameObject variable.");
                return Status.Failure;
            }

            if (Target.Value == null)
            {
                m_AsyncOperation = GameObject.InstantiateAsync(Object.Value);
            }
            else
            {
                switch (TargetUsage.Value)
                {
                    case TargetMode.Position:
                        m_AsyncOperation = GameObject.InstantiateAsync(Object.Value, Target.Value.position, Quaternion.identity);
                        break;

                    case TargetMode.PositionAndRotation:
                        m_AsyncOperation = GameObject.InstantiateAsync(Object.Value, Target.Value.position, Target.Value.rotation);
                        break;

                    case TargetMode.Parent:
                        m_AsyncOperation = GameObject.InstantiateAsync(Object.Value, Target.Value, Vector3.zero, Quaternion.identity);
                        break;

                    default:
                        m_AsyncOperation = GameObject.InstantiateAsync(Object.Value);
                        break;
                }
            }

            if (m_AsyncOperation == null)
            {
                LogFailure($"Failed to instantiate {Object.Value.name}.", true);
                return Status.Failure;
            }

            m_AsyncOperation.completed += operation =>
            {                
                if (m_AsyncOperation.isDone)
                {
                    InstantiatedObject.Value = m_AsyncOperation.Result[0] as GameObject;
                    m_AsyncOperation = null;
                }

                // Awake the node only if the node is not running in background.
                if (CurrentStatus == Status.Waiting)
                {
                    AwakeNode(this);
                }
            };

            return RunInBackground.Value ? Status.Success : Status.Waiting;
        }

        protected override Status OnUpdate()
        {
            if (m_AsyncOperation != null && !m_AsyncOperation.isDone)
            {
                LogFailure($"Failed to instantiate {Object.Value.name}.", true);
                m_AsyncOperation = null;
                return Status.Failure;
            }

            return Status.Success;
        }

        protected override void OnSerialize()
        {
            if (m_AsyncOperation == null || m_AsyncOperation.isDone)
            {
                return;
            }
            
            m_HasPendingRequest = true;
        }

        protected override void OnDeserialize()
        {
            if (!m_HasPendingRequest)
            {
                return;
            }

            m_HasPendingRequest = false;
            if (OnStart() == Status.Success)
            {
                CurrentStatus = Status.Running;
            }
        }
    }
}
