using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Properties;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Switch",
        description: "Branches off based on enum value.", 
        icon: "Icons/Sequence",
        category: "Flow/Conditional",
        id: "ef072beedcccd16ac0cd3cb5295fe4cd")]
    internal partial class SwitchComposite : Composite
    {
        // The returned status when no node is attached to the desired port.
        internal Status DefaultStatus = Status.Success;

        [SerializeReference] public BlackboardVariable EnumVariable;
        [CreateProperty]
        private int m_CurrentChild = -1;

        // Cache for enum type information
        private int m_CachedIntValue = -1;
        private Type m_CachedEnumType;
        private Array m_CachedEnumValues;
        private Dictionary<int, int> m_CachedValueToChildIndexMap = new();

        // Used to know when the enum value has changed
        private bool m_IsInitialized = false;
        private bool m_HasEnumChanged = false;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            // Not initizalized means no BBV is assigned.
            if (Children.Count == 0 || m_IsInitialized == false)
            {
                return Status.Success;
            }

            if (m_HasEnumChanged)
            {
                UpdateCaches();
            }

            // Reset child index
            m_CurrentChild = -1;

            if (!m_CachedValueToChildIndexMap.TryGetValue(m_CachedIntValue, out m_CurrentChild) || m_CurrentChild >= Children.Count)
            {
                // If the value is not found in the cache, return the default status
                return DefaultStatus;
            }

            Node child = Children[m_CurrentChild];
            if (child == null)
            {
                return DefaultStatus;
            }

            Status status = StartNode(child);
            return status switch
            {
                Status.Success => Status.Success,
                Status.Failure => Status.Failure,
                _ => Status.Waiting
            };
        }


        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            if (m_CurrentChild >= 0 && m_CurrentChild < Children.Count)
            {
                var child = Children[m_CurrentChild];
                return child != null ? child.CurrentStatus : DefaultStatus;
            }

            return DefaultStatus;
        }

        /// <inheritdoc cref="ResetStatus" />
        protected internal override void ResetStatus()
        {
            CurrentStatus = Status.Uninitialized;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child != null)
                {
                    child.ResetStatus();
                }
            }

            // Listen to changes in the enum variable only once, this is use to avoid boxing.
            if (m_IsInitialized == false && EnumVariable?.ObjectValue != null) // boxing cost ONLY when not initialized.
            {
                m_IsInitialized = true;
                EnumVariable.OnValueChanged += () => m_HasEnumChanged = true;
                UpdateCaches();
            }
        }

        private void UpdateCaches()
        {
            // Boxing value is unavoidable because enum type is unknown at compile time.
            // If you need a GC free solution, consider creating a new Composite that uses the explicit enum type.
            var enumValue = EnumVariable.ObjectValue;
            m_CachedIntValue = (int)enumValue;
            m_HasEnumChanged = false;

            EnsureEnumCacheIsValid(enumValue.GetType());
        }

        private void EnsureEnumCacheIsValid(Type enumType)
        {
            if (m_CachedEnumValues != null && m_CachedEnumType == enumType)
            {
                return;
            }

            m_CachedValueToChildIndexMap.Clear();
            m_CachedEnumType = enumType;
            m_CachedEnumValues = Enum.GetValues(enumType);

            for (int i = 0; i < m_CachedEnumValues.Length; i++)
            {
                m_CachedValueToChildIndexMap[Convert.ToInt32(m_CachedEnumValues.GetValue(i))] = i;
            }
        }
    }
}