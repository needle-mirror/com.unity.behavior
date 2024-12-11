using System;
using Unity.Properties;

namespace Unity.Behavior
{
    /// <summary>
    /// Executes all branches at the same time, stopping if one fails or succeeds.
    /// </summary>
    [Serializable, GeneratePropertyBag]  
    [NodeDescription(
        name: "Run In Parallel Until Any Completes",
        category: "Flow/Parallel Execution",
        description: "Execute all branches at the same time, stopping if one fails or succeeds.",
        icon: "Icons/parallel_any",
        hideInSearch: true,
        id: "e49414e2f12d45efbff56d88f5befb1d")]
    internal partial class ParallelAnyComposite : Composite
    {
        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (Children.Count == 0)
                return Status.Success;

            for (int i = 0; i < Children.Count; ++i)
            {
                var childStatus = StartNode(Children[i]);
                if (childStatus is Status.Failure or Status.Success)
                {
                    return childStatus;
                }
            }

            return Status.Waiting;
        }

        /// <inheritdoc cref="OnStart" />
        protected override Status OnUpdate()
        {
            for (int i = 0; i < Children.Count; ++i)
            {
                Status childStatus = Children[i].CurrentStatus;
                if (childStatus is Status.Failure or Status.Success)
                {
                    return childStatus;
                }
            }
            
            return Status.Waiting;
        }
    }
    
    /// <summary>
    /// Executes all branches at the same time, stopping if one succeeds.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Run In Parallel Until Any Succeeds",
        category: "Flow/Parallel Execution",
        description: "Execute all branches at the same time, stopping if one succeeds.",
        icon: "Icons/parallel_any",
        hideInSearch: true,
        id: "2e528604708c452babf9c9ce86ca4313")]
    internal partial class ParallelAnySuccess : Composite
    {
        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnStart()
        {
            if (Children.Count == 0)
                return Status.Success;

            int failCount = 0;
            for (int i = 0; i < Children.Count; ++i)
            {
                var childStatus = StartNode(Children[i]);
                if (childStatus is Status.Success)
                {
                    return Status.Success;
                }
                else if (childStatus is Status.Failure)
                {
                    failCount++;
                }
            }

            return failCount == Children.Count ? Status.Failure : Status.Waiting;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            int failCount = 0;
            for (int i = 0; i < Children.Count; ++i)
            {
                Status childStatus = Children[i].CurrentStatus;
                if (childStatus is Status.Success)
                {
                    return Status.Success;
                }
                else if (childStatus is Status.Failure)
                {
                    failCount++;
                }
            }
            return failCount == Children.Count ? Status.Failure : Status.Waiting;
        }
    }
    
    // Note: ParallelAllSuccess is actually ParallelAnyFailed but we can't rename it without doing a major bump or
    // forcing the user to rebuild their runtime graphs.
    
    /// <summary>
    /// Executes all branches at the same time, stopping if one fails.
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Run In Parallel Until Any Fails",
        category: "Flow/Parallel Execution",
        description: "Execute all branches at the same time, stopping if one fails.",
        hideInSearch: true,
        icon: "Icons/parallel_all",
        id: "14a266d5d02d4c67a7940885be9078e8")]
    internal partial class ParallelAllSuccess : Composite
    {
        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            bool shouldWait = false;
            for (int i = 0; i < Children.Count; ++i)
            {
                var childStatus = StartNode(Children[i]);
                if (childStatus is Status.Running or Status.Waiting)
                {
                    shouldWait = true;
                }
                else if (childStatus is Status.Failure)
                {
                    return Status.Failure; // early termination
                }
            }

            if (shouldWait)
                return Status.Waiting;
            else
                return Status.Success;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            bool shouldWait = false;
            for (int i = 0; i < Children.Count; ++i)
            {
                var childStatus = Children[i].CurrentStatus;
                if (childStatus is Status.Running or Status.Waiting)
                {
                    shouldWait = true;
                }
                else if (childStatus is Status.Failure)
                {
                    return Status.Failure; // early termination
                }
            }

            if (shouldWait)
                return Status.Waiting;
            else
                return Status.Success;
        }
    }
}