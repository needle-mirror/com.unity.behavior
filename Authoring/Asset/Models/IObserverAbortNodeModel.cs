using System;

namespace Unity.Behavior
{
    /// <summary>
    /// Interface for node model of IObserverAbort runtime node.
    /// </summary>
    internal interface IObserverAbortNodeModel : IConditionalNodeModel
    {
        /// <summary>
        /// The observer behavior type for this node.
        /// </summary>
        ObserverAbortTarget ObserverType { get; set; }
    }

    internal static class IObserverAbortNodeModelExtension
    {
        public static string GetObserverTypeUITitle(this IObserverAbortNodeModel target)
        {
            return target.ObserverType switch
            {
                ObserverAbortTarget.Self => " & Abort On Fail",
                ObserverAbortTarget.LowerPriority => " & Abort Lower Priority",
                ObserverAbortTarget.Both => " & Abort On Fail Or Lower Priority",
                _ => string.Empty
            };
        }
    }
}

