using System;
using System.Collections.Generic;
using System.Linq;
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

        private BehaviorGraphNodeModel BehaviorGraphNode => Model as BehaviorGraphNodeModel;

        /// <inheritdoc cref="SetValueWithoutNotify"/>
        public void SetValueWithoutNotify(TValueType newValue)
        {
            // Convert enum value to index for the dropdown.
            m_Field.SetValueWithoutNotify(EnumVariableElement.GetEnumValueIndex(Convert.ToInt32(newValue), Enum.GetValues(typeof(TValueType))));
        }

        internal Dropdown Field => m_Field;

        /// <inheritdoc cref="value"/>
        public TValueType value
        {
            get => (TValueType)Enum.ToObject(typeof(TValueType), m_Field.value.FirstOrDefault());
            set
            {
                m_Field.SetValueWithoutNotify(EnumVariableElement.GetEnumValueIndex(Convert.ToInt32(value), Enum.GetValues(typeof(TValueType))));
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
            LinkVariableType = runtimeType;

            m_Field = new Dropdown { name = "InputField" };
            FieldContainer.Clear();
            FieldContainer.Add(m_Field);

            m_Field.size = Size.S;
            Array enumValues = Enum.GetValues(runtimeType);
            m_Field.bindItem = (item, i) => item.label = Enum.GetName(runtimeType, enumValues.GetValue(i));
            m_Field.sourceItems = enumValues;

            SetFieldIcon(runtimeType);

            m_Field.RegisterValueChangedCallback(OnValueChanged);

            VisualElement linkFieldSpacer = new VisualElement();
            linkFieldSpacer.AddToClassList("LinkButtonSpacer");
            linkFieldSpacer.style.position = Position.Relative;
            linkFieldSpacer.style.visibility = Visibility.Hidden;
            m_Field.Q<VisualElement>("appui-picker__trailingcontainer").Add(linkFieldSpacer);
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
                // Need to make sure we convert the enum value into index value for the dropdown.
                SetValueWithoutNotify((TValueType)field.Value);
            }
            base.UpdateValue(field);
        }
    }
}