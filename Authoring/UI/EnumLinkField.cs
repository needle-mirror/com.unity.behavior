using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    /// <summary>
    /// A link field class specialized for enum types.
    /// </summary>
    /// <typeparam name="TValueType">The enum type represented by the link field.</typeparam>
    public class EnumLinkField<TValueType> : BaseLinkField, INotifyValueChanged<TValueType> where TValueType : Enum
    {
        private readonly Dropdown m_Field;
        private readonly bool m_IsFlagsEnum;
        private readonly Array m_EnumValues;
        private readonly Type m_EnumType;

        private BehaviorGraphNodeModel BehaviorGraphNode => Model as BehaviorGraphNodeModel;

        /// <inheritdoc cref="SetValueWithoutNotify"/>
        public void SetValueWithoutNotify(TValueType newValue)
        {
            if (m_IsFlagsEnum)
            {
                var newValueAsInt = Convert.ToInt32(newValue);
                var selectedIndices = new List<int>();

                // Handle special case where value is 0 (None/Default)
                if (newValueAsInt == 0)
                {
                    // Find the 0 value index if it exists
                    for (int i = 0; i < m_EnumValues.Length; i++)
                    {
                        if (Convert.ToInt32(m_EnumValues.GetValue(i)) == 0)
                        {
                            selectedIndices.Add(i);
                            break;
                        }
                    }
                }
                else
                {
                    // For non-zero flag values, find all matching flags
                    for (int i = 0; i < m_EnumValues.Length; i++)
                    {
                        var enumValue = Convert.ToInt32(m_EnumValues.GetValue(i));
                        // Skip zero value (None) when we have other flags
                        if (enumValue == 0)
                            continue;

                        // Add matching flags to selection
                        if ((newValueAsInt & enumValue) == enumValue && enumValue != 0)
                            selectedIndices.Add(i);
                    }
                }

                m_Field.SetValueWithoutNotify(selectedIndices);
            }
            else
            {
                // Find the index of the enum value in the enumValues array
                m_Field.SetValueWithoutNotify(GetEnumValueIndex(newValue, m_EnumValues));
            }
        }

        internal Dropdown Field => m_Field;

        /// <inheritdoc cref="value"/>
        public TValueType value
        {
            get
            {
                if (m_IsFlagsEnum)
                {
                    var indices = m_Field.value;

                    if (!indices.Any())
                        return default;

                    int combinedValue = 0;
                    foreach (var index in indices)
                    {
                        combinedValue |= Convert.ToInt32(m_EnumValues.GetValue(index));
                    }

                    return (TValueType)Enum.ToObject(m_EnumType, combinedValue);
                }
                else
                {
                    // Get the actual enum value at the selected index
                    int selectedIndex = m_Field.value.FirstOrDefault();
                    if (selectedIndex >= 0 && selectedIndex < m_EnumValues.Length)
                    {
                        return (TValueType)m_EnumValues.GetValue(selectedIndex);
                    }
                    return default;
                }
            }
            set
            {
                SetValueWithoutNotify(value);
                using LinkFieldValueChangeEvent changeEvent = LinkFieldValueChangeEvent.GetPooled(this, value);
                SendEvent(changeEvent);
            }
        }

        /// <summary>
        /// The default constructor for EnumLinkField, using its generic type for initialization.
        /// </summary>
        public EnumLinkField() : this(typeof(TValueType))
        {
        }

        /// <summary>
        /// A custom constructor taking any type for initialization.
        /// </summary>
        /// <param name="runtimeType">The enum type represented by the link field.</param>
        public EnumLinkField(Type runtimeType)
        {
            m_EnumType = runtimeType;
            m_IsFlagsEnum = runtimeType.GetCustomAttribute<FlagsAttribute>() != null;
            m_EnumValues = Enum.GetValues(runtimeType);

            LinkVariableType = runtimeType;

            m_Field = new Dropdown { name = "InputField" };
            FieldContainer.Clear();
            FieldContainer.Add(m_Field);

            // Set multiple selection for flag enums
            if (m_IsFlagsEnum)
                m_Field.selectionType = PickerSelectionType.Multiple;

            m_Field.size = Size.S;
            m_Field.bindItem = (item, i) => item.label = Enum.GetName(runtimeType, m_EnumValues.GetValue(i));
            m_Field.sourceItems = m_EnumValues;

            SetFieldIcon(runtimeType);

            m_Field.RegisterValueChangedCallback(OnValueChanged);
        }

        private void OnValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            using LinkFieldValueChangeEvent changeEvent = LinkFieldValueChangeEvent.GetPooled(this, value);
            SendEvent(changeEvent);
        }

        internal override void UpdateValue(IVariableLink field)
        {
            if (field.Value == null)
            {
                SetValueWithoutNotify(default);
            }
            else
            {
                SetValueWithoutNotify((TValueType)field.Value);
            }
            base.UpdateValue(field);
        }

        // Returns the index of the enum value in the enumValues array.
        private static IEnumerable<int> GetEnumValueIndex<T>(T enumValue, Array enumValues) where T : Enum
        {
            var i = 0;
            foreach (var value in enumValues)
            {
                if (EqualityComparer<T>.Default.Equals((T)value, enumValue)) return new[] { i };
                ++i;
            }

            return new[] { 0 }; // Return first value as default
        }
    }
}
