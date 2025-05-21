using System;

namespace Unity.Behavior.GraphFramework
{
    [Serializable]
    internal abstract class Command
    {
        public bool MarkUndo { get; }
        public bool SetAssetDirty { get; protected set; } = true;

        protected Command(bool markUndo)
        {
            MarkUndo = markUndo;
        }
    }

    /// <summary>
    /// Only Commands implementing this interface will dirty blackboard asset.
    /// </summary>
    internal interface IBlackboardAssetCommand { }
}