namespace Unity.Behavior
{
    /// <summary>
    /// Defines when a node should monitor and react to conditions.
    /// </summary>
    internal enum ObserverAbortTarget
    {
        /// <summary>
        /// No observer behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Aborts own child when conditions are met.
        /// </summary>
        Self = 1,

        /// <summary>
        /// Monitors conditions as long as parent composite is active.
        /// Interrupts lower-priority siblings when conditions are met
        /// </summary>
        LowerPriority = 2,

        /// <summary>
        /// Combines Self and LowerPriority behaviors.
        /// </summary>
        Both = 3
    }

    /// <summary>
    /// Interface for nodes that can observe conditions and trigger interruption of siblings.
    /// </summary>
    internal interface IObserverAbort : IConditional
    {
        /// <summary>
        /// The observer behavior for this node.
        /// </summary>
        ObserverAbortTarget AbortTarget { get; set; }

        /// <summary>
        /// Checks if this observer should trigger interruption of lower-priority siblings.
        /// Only returns true when ObserverType is LowerPriority or Both and conditions are met.
        /// </summary>
        /// <returns>True if observer conditions are met and lower-priority siblings should be interrupted.</returns>
        bool EvaluateObserver();
    }
}