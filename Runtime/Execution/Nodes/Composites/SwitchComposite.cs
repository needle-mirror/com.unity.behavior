using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Switch",
        description: "Branches off based on enum value. When used with a flag enum, only the first active flag will be considered.", 
        icon: "Icons/Sequence",
        category: "Flow/Conditional",
        id: "ef072beedcccd16ac0cd3cb5295fe4cd")]
    internal partial class SwitchComposite : Composite
    {
        private static readonly Dictionary<Type, bool> s_FlagEnumCache = new();

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
                EnumVariable.OnValueChanged += SetEnumValueChanged;
                UpdateCaches();
            }
        }

        // Cache in a static lookup table as the attribute won't change for a given type.
        private static bool IsFlagEnum(Type enumType)
        {
            if (s_FlagEnumCache.TryGetValue(enumType, out bool isFlagEnum))
                return isFlagEnum;

            isFlagEnum = enumType.GetCustomAttribute<FlagsAttribute>() != null;
            s_FlagEnumCache[enumType] = isFlagEnum;
            return isFlagEnum;
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

            if (UpdateAsEnumFlag(enumType, intValue))
            {
                return;
            }

            // Standard enum handling
            m_CachedIntValue = intValue;
            m_HasEnumChanged = false;
            EnsureEnumCacheIsValid(enumType);
        }

        private bool UpdateAsEnumFlag(Type enumType, int intValue)
        {
            if (!IsFlagEnum(enumType))
            {
                return false;
            }

#if UNITY_EDITOR
            // Check if multiple flags are set
            if (intValue > 0 && (intValue & (intValue - 1)) != 0)
            {
                Debug.LogWarning($"Switch node is using a flag enum '{enumType.Name}' with multiple flags set ({intValue}). " +
                               "Only the first matching flag will be executed. Consider using a Switch Flag node instead for parallel execution.");
            }
#endif
            // Find the first set flag (lowest bit)
            var enumValues = Enum.GetValues(enumType);
            for (int i = 0; i < enumValues.Length; i++)
            {
                int flagValue = Convert.ToInt32(enumValues.GetValue(i));
                if (flagValue > 0 && (intValue & flagValue) != 0)
                {
                    // Use this flag's index as the child index
                    m_CachedIntValue = flagValue;
                    m_HasEnumChanged = false;
                    EnsureEnumCacheIsValid(enumType);
                    return true;
                }
            }

            return false;
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
