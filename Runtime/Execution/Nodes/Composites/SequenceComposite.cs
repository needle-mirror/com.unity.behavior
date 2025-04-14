using System;
using Unity.Properties;

namespace Unity.Behavior
{
    /// <summary>
    /// Executes branches in order until one fails or all succeed.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Sequence",
        description: "Executes branches in order until one fails or all succeed.", 
        icon: "Icons/Sequence",
        category: "Flow",
        id: "dfd2a5f53dc54b8dad31dc3f7a794079")]
    internal partial class SequenceComposite : Composite
    {
        [CreateProperty] int m_CurrentChild;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            m_CurrentChild = 0;
            return StartChild(m_CurrentChild);
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            var currentChild = Children[m_CurrentChild];
            Status childStatus = currentChild.CurrentStatus;
            if (childStatus == Status.Success)
            {
                return StartChild(++m_CurrentChild);
            }
            return childStatus == Status.Running ? Status.Waiting : childStatus;
        }

        protected Status StartChild(int childIndex)
        {
            if (m_CurrentChild >= Children.Count)
            {
                return Status.Success;
            }
            var childStatus = StartNode(Children[childIndex]);
            return childStatus switch
            {
                Status.Success => childIndex + 1 >= Children.Count ? Status.Success : Status.Running,
                Status.Running => Status.Waiting,
                _ => childStatus
            };
        }
    }
}
