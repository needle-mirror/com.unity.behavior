using System;
using Unity.Properties;

namespace Unity.Behavior
{
    /// <summary>
    /// Executes branches in order until one succeeds.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Try In Order",
        description: "Executes branches in order until one succeeds.", 
        icon: "Icons/selector",
        category: "Flow",
        id: "2bdfd1f8aaec469f8df1fd3190d7466b")]
    internal partial class SelectorComposite : Composite
    {
        [CreateProperty] private int m_CurrentChild;

        protected override Status OnStart()
        {
            m_CurrentChild = 0;
            return StartChild(m_CurrentChild);
        }

        protected override Status OnUpdate()
        {
            var currentChild = Children[m_CurrentChild];
            Status childStatus = currentChild.CurrentStatus;
            if (childStatus == Status.Failure)
            {
                return ++m_CurrentChild >= Children.Count ? Status.Failure : StartChild(m_CurrentChild);
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
                Status.Failure => childIndex + 1 >= Children.Count ? Status.Failure : Status.Running,
                Status.Running => Status.Waiting,
                _ => childStatus
            };
        }
    }
}
