using System;
using Unity.Behavior.Serialization;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Modify the behavior of its child nodes.
    /// </summary>
    [Serializable]
    public abstract class Modifier : Node, IParent
    {
        /// <summary>
        /// the parent of the node.
        /// </summary>
        [CreateProperty, DontSerialize]
        public Node Parent { get => m_Parent; internal set { m_Parent = value; } }
        [SerializeReference]
        internal Node m_Parent;

        /// <summary>
        /// the child of the node.
        /// </summary>
        [CreateProperty, DontSerialize]
        public Node Child { get => m_Child; internal set => m_Child = value; }
        [SerializeReference]
        internal Node m_Child;

        /// <inheritdoc cref="AwakeParents" />
        protected internal override void AwakeParents()
        {
            AwakeNode(Parent);
        }

        /// <inheritdoc cref="Add" />
        public void Add(Node child)
        {
            Child = child;
            child.AddParent(this);
        }

        /// <inheritdoc cref="AddParent" />
        internal override void AddParent(Node parent)
        {
            this.Parent = parent;
        }

        /// <inheritdoc cref="ResetStatus" />
        protected internal override void ResetStatus()
        {
            CurrentStatus = Status.Uninitialized;
            Child?.ResetStatus();
        }
    }
}