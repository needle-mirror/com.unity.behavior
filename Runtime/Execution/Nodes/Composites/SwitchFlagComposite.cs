using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Switch Flag",
        description: "Executes all branches that match the set flags in the enum value in parallel. Works with regular enums as a fallback.",
        icon: "Icons/parallel_all",
        category: "Flow/Conditional",
        id: "af072beedcccd16ac0cd3cb5295fe4ce")]
    internal partial class SwitchFlagComposite : Composite
    {
        private static readonly Dictionary<Type, bool> s_FlagEnumCache = new();

        // The returned status when no flags are set or no matching children exist.
        internal Status DefaultStatus = Status.Success;

        [SerializeReference] public BlackboardVariable EnumVariable;

        [CreateProperty] private List<int> m_ActiveChildIndices = new();

        private Type m_CachedEnumType;
        private Array m_CachedEnumValues;
        private Dictionary<int, int> m_CachedValueToChildIndexMap = new();
        private int m_CachedIntValue = -1;
        
        private bool m_IsInitialized = false;
        private bool m_HasEnumChanged = false;
        private bool m_IsCurrentEnumFlags = false;

        /// <inheritdoc cref="OnStart" />
        protected override Status OnStart()
        {
            if (Children.Count == 0 || m_IsInitialized == false)
            {
                return DefaultStatus;
            }

            if (m_HasEnumChanged)
            {
                UpdateCaches();
            }

            // Find active children
            m_ActiveChildIndices.Clear();

            if (m_IsCurrentEnumFlags)
            {
                // Flag enum: find all matching flags
                foreach (var kvp in m_CachedValueToChildIndexMap)
                {
                    int flagValue = kvp.Key;
                    int childIndex = kvp.Value;

                    if (flagValue > 0 && (m_CachedIntValue & flagValue) != 0 && childIndex < Children.Count)
                    {
                        m_ActiveChildIndices.Add(childIndex);
                    }
                }
            }
            else
            {
                // Regular enum: find single matching child
                if (m_CachedValueToChildIndexMap.TryGetValue(m_CachedIntValue, out int childIndex) &&
                    childIndex < Children.Count)
                {
                    m_ActiveChildIndices.Add(childIndex);
                }
            }

            if (m_ActiveChildIndices.Count == 0)
            {
                return DefaultStatus;
            }

            // Start all active children
            bool shouldWait = false;
            int successCount = 0;

            for (int i = 0; i < m_ActiveChildIndices.Count; i++)
            {
                int childIndex = m_ActiveChildIndices[i];
                Node child = Children[childIndex];

                if (child == null)
                    continue;

                var childStatus = StartNode(child);
                if (childStatus is Status.Running or Status.Waiting)
                {
                    shouldWait = true;
                }
                else if (childStatus is Status.Success)
                {
                    successCount++;
                }
            }

            if (shouldWait)
            {
                return Status.Waiting;
            }

            return successCount == m_ActiveChildIndices.Count ? Status.Success : Status.Failure;
        }

        /// <inheritdoc cref="OnUpdate" />
        protected override Status OnUpdate()
        {
            if (m_ActiveChildIndices.Count == 0)
            {
                return DefaultStatus;
            }

            int successCount = 0;

            for (int i = 0; i < m_ActiveChildIndices.Count; i++)
            {
                int childIndex = m_ActiveChildIndices[i];
                Node child = Children[childIndex];

                if (child == null)
                    continue;

                var childStatus = child.CurrentStatus;
                if (childStatus is Status.Running or Status.Waiting)
                {
                    return Status.Waiting;
                }
                else if (childStatus is Status.Success)
                {
                    successCount++;
                }
            }

            // All active children completed
            return successCount == m_ActiveChildIndices.Count ? Status.Success : Status.Failure;
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

            // Listen to changes in the enum variable only once
            if (!m_IsInitialized && EnumVariable?.ObjectValue != null)
            {
                m_IsInitialized = true;
                EnumVariable.OnValueChanged += SetEnumValueChanged;
                UpdateCaches();
            }
        }

        private void SetEnumValueChanged()
        {
            m_HasEnumChanged = true;
        }

        private void UpdateCaches()
        {
            var enumValue = EnumVariable.ObjectValue;
            var enumType = enumValue.GetType();
            int intValue = Convert.ToInt32(enumValue);

            m_IsCurrentEnumFlags = IsFlagEnum(enumType);
            m_CachedIntValue = intValue;
            m_HasEnumChanged = false;
            EnsureEnumCacheIsValid(enumType);
        }

        private static bool IsFlagEnum(Type enumType)
        {
            if (s_FlagEnumCache.TryGetValue(enumType, out bool isFlagEnum))
                return isFlagEnum;

            isFlagEnum = enumType.GetCustomAttribute<FlagsAttribute>() != null;
            s_FlagEnumCache[enumType] = isFlagEnum;
            return isFlagEnum;
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

            // Map each enum value to its child index
            for (int i = 0; i < m_CachedEnumValues.Length; i++)
            {
                int flagValue = Convert.ToInt32(m_CachedEnumValues.GetValue(i));
                m_CachedValueToChildIndexMap[flagValue] = i;
            }
        }
    }
}