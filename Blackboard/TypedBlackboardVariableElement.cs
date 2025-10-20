using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using DoubleField = Unity.AppUI.UI.DoubleField;
using FloatField = Unity.AppUI.UI.FloatField;
using IntegerField = Unity.AppUI.UI.IntField;
using TextField = Unity.AppUI.UI.TextField;
using Toggle = Unity.AppUI.UI.Toggle;
using Vector2Field = Unity.AppUI.UI.Vector2Field;
using Vector2IntField = Unity.AppUI.UI.Vector2IntField;
using Vector3Field = Unity.AppUI.UI.Vector3Field;
using Vector3IntField = Unity.AppUI.UI.Vector3IntField;
using Vector4Field = Unity.AppUI.UI.Vector4Field;

namespace Unity.Behavior.GraphFramework
{
    internal class TypedVariableElement<T, FieldType> : BlackboardVariableElement where FieldType : VisualElement, new()
    {
        protected FieldType m_Field;

        public TypedVariableElement(BlackboardView view, VariableModel variableModel, bool IsEditable) : base(view, variableModel, IsEditable)
        {
            m_Field = new FieldType();
            m_Field.SetEnabled(IsEditable);

            if (variableModel.ID == k_ReservedID)
            {
                m_Field.SetEnabled(false);
                LocalizedTextElement selfTextElement = new LocalizedTextElement("The GameObject running the graph.");
                tooltip = selfTextElement.tooltip = m_Field.tooltip = "This variable will be assigned with the GameObject the graph is running on.\n"
                    + "It can be assigned to a different GameObject via the BehaviorAgent component.";
                Add(selfTextElement);
            }
            else
            {
                Add(m_Field);
            }

            if (m_Field is INotifyValueChanged<T> element)
            {
                element.RegisterValueChangedCallback(OnValueChanged);
                element.SetValueWithoutNotify((T)variableModel.ObjectValue);
            }
            else if (m_Field is INotifyValueChanged<IEnumerable<int>> enumerableElement)
            {
                enumerableElement.RegisterValueChangedCallback(OnValueChanged);
            }
        }

        private void OnValueChanged(ChangeEvent<IEnumerable<int>> evt)
        {
            m_View.Dispatcher.DispatchImmediate(new SetBlackboardVariableValueCommand(VariableModel, evt.newValue.FirstOrDefault()));
        }

        private void OnValueChanged(ChangeEvent<T> evt)
        {
            m_View.Dispatcher.DispatchImmediate(new SetBlackboardVariableValueCommand(VariableModel, evt.newValue));
        }
    }

    internal class TypedListVariableElement<ValueType, BaseValueType> : TypedVariableElement<ValueType, ListView> where ValueType : BaseValueType
    {
        protected TypedListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable, Type fieldType) : base(view, variableModel, isEditable)
        {
            CreateListView(m_Field, new List<ValueType>(), fieldType);
        }

        private void CreateListView(ListView listView, List<ValueType> elements, Type fieldType)
        {
            if (VariableModel.ObjectValue != null)
            {
                elements = (List<ValueType>)VariableModel.ObjectValue;
            }
            else
            {
                VariableModel.ObjectValue = elements;
            }

            listView.itemsSource = elements;
            listView.showAddRemoveFooter = true;
            listView.makeItem = () =>
            {
                ListItemElement listItem = new ListItemElement(fieldType);

                listItem.Field.RegisterValueChangedCallback(delegate (ChangeEvent<BaseValueType> evt)
                {
                    if (elements[listItem.Index] is not null && elements[listItem.Index].Equals((ValueType)evt.newValue))
                    {
                        return;
                    }

                    elements[listItem.Index] = (ValueType)evt.newValue;
                    VariableModel.ObjectValue = elements;
                    m_View.Dispatcher.DispatchImmediate(new SetBlackboardVariableValueCommand(VariableModel, VariableModel.ObjectValue));
                });
                return listItem;
            };
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            listView.showBorder = true;
            listView.bindItem = (element, i) =>
            {
                ListItemElement listItem = element as ListItemElement;
                if (listItem == null)
                {
                    return;
                }
                listItem.Index = i;
                listItem.Init(i, elements[i]);
            };
            listView.Q<UnityEngine.UIElements.Button>("unity-list-view__add-button").clicked += () =>
            {
                m_View.Dispatcher.DispatchImmediate(new SetBlackboardVariableValueCommand(VariableModel, VariableModel.ObjectValue));
            };
            listView.Q<UnityEngine.UIElements.Button>("unity-list-view__remove-button").clicked += () =>
            {
                m_View.Dispatcher.DispatchImmediate(new SetBlackboardVariableValueCommand(VariableModel, VariableModel.ObjectValue));
            };
        }

        private class ListItemElement : VisualElement
        {
            private readonly Label m_Label;
            public readonly INotifyValueChanged<BaseValueType> Field;

            public int Index;

            public ListItemElement(Type fieldElementType)
            {
                name = "ListVariableListViewItem";
                m_Label = new Label();
#if UNITY_EDITOR
                if (fieldElementType == typeof(UnityEditor.UIElements.ObjectField))
                {
                    UnityEditor.UIElements.ObjectField objectField = new UnityEditor.UIElements.ObjectField();
                    objectField.allowSceneObjects = false;
                    objectField.objectType = typeof(ValueType);
                    Field = (INotifyValueChanged<BaseValueType>)objectField;
                }
                else
#endif
                {
                    Field = (INotifyValueChanged<BaseValueType>)Activator.CreateInstance(fieldElementType);
                }

                Add(m_Label);
                Add(Field as VisualElement);
            }

            public void Init(int index, ValueType value)
            {
                m_Label.text = $"Element {index}";
                m_Label.AddToClassList("ListVariableItemLabel");
                Field.value = value;
            }
        }
    }

    [VariableUI(typeof(TypedVariableModel<int>))]
    internal class IntVariableElement : TypedVariableElement<int, IntegerField>
    {
        public IntVariableElement(BlackboardView view, VariableModel variableModel, bool IsEditable) : base(view, variableModel, IsEditable)
        {
            IntegerField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<int>>))]
    internal class IntListVariableElement : TypedListVariableElement<int, int>
    {
        public IntListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(IntegerField))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<float>))]
    internal class FloatVariableElement : TypedVariableElement<float, FloatField>
    {
        public FloatVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            FloatField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<float>>))]
    internal class FloatListVariableElement : TypedListVariableElement<float, float>
    {
        public FloatListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(FloatField))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<double>))]
    internal class DoubleVariableElement : TypedVariableElement<double, DoubleField>
    {
        public DoubleVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            DoubleField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<double>>))]
    internal class DoubleListVariableElement : TypedListVariableElement<double, double>
    {
        public DoubleListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(DoubleField))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<bool>))]
    internal class BoolVariableElement : TypedVariableElement<bool, Toggle>
    {
        public BoolVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            Toggle toggle = m_Field;
            toggle.label = toggle.value.ToString();
            m_Field.RegisterValueChangedCallback(evt =>
            {
                m_Field.label = evt.newValue.ToString();
            });
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<bool>>))]
    internal class BoolListVariableElement : TypedListVariableElement<bool, bool>
    {
        public BoolListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(Toggle))
        {
        }
    }

#if UNITY_EDITOR

    [VariableUI(typeof(TypedVariableModel<Color>))]
    internal class ColorVariableElement : TypedVariableElement<Color, ColorField>
    {
        public ColorVariableElement(BlackboardView view, VariableModel variableModel, bool IsEditable) : base(view, variableModel, IsEditable)
        {
            ColorField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<Color>>))]
    internal class ColorListVariableElement : TypedListVariableElement<Color, Color>
    {
        public ColorListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(ColorField))
        {
        }
    }

#endif

    [VariableUI(typeof(TypedVariableModel<Vector2>))]
    internal class Vector2VariableElement : TypedVariableElement<Vector2, Vector2Field>
    {
        public Vector2VariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            Vector2Field field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<Vector2>>))]
    internal class Vector2ListVariableElement : TypedListVariableElement<Vector2, Vector2>
    {
        public Vector2ListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(Vector2Field))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<Vector3>))]
    internal class Vector3VariableElement : TypedVariableElement<Vector3, Vector3Field>
    {
        public Vector3VariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            Vector3Field field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<Vector3>>))]
    internal class Vector3ListVariableElement : TypedListVariableElement<Vector3, Vector3>
    {
        public Vector3ListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(Vector3Field))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<Vector4>))]
    internal class Vector4VariableElement : TypedVariableElement<Vector4, Vector4Field>
    {
        public Vector4VariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            Vector4Field field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<Vector4>>))]
    internal class Vector4ListVariableElement : TypedListVariableElement<Vector4, Vector4>
    {
        public Vector4ListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(Vector4Field))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<Vector2Int>))]
    internal class Vector2IntVariableElement : TypedVariableElement<Vector2Int, Vector2IntField>
    {
        public Vector2IntVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            Vector2IntField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<Vector2Int>>))]
    internal class Vector2IntListVariableElement : TypedListVariableElement<Vector2Int, Vector2Int>
    {
        public Vector2IntListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(Vector2IntField))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<Vector3Int>))]
    internal class Vector3IntVariableElement : TypedVariableElement<Vector3Int, Vector3IntField>
    {
        public Vector3IntVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            Vector3IntField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<Vector3Int>>))]
    internal class Vector3IntListVariableElement : TypedListVariableElement<Vector3Int, Vector3Int>
    {
        public Vector3IntListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(Vector3IntField))
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<string>))]
    internal class StringVariableElement : TypedVariableElement<string, TextField>
    {
        public StringVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            TextField field = m_Field;
            field.size = Size.M;
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<string>>))]
    internal class StringListVariableElement : TypedListVariableElement<string, string>
    {
        public StringListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(TextField))
        {
        }
    }

#if UNITY_EDITOR

    internal class ObjectVariableElement : TypedVariableElement<UnityEngine.Object, UnityEditor.UIElements.ObjectField>
#else
    internal class ObjectVariableElement : TypedVariableElement<UnityEngine.Object, RuntimeObjectField>
#endif
    {
        public ObjectVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable)
            : base(view, variableModel, isEditable)
        {
#if UNITY_EDITOR
            m_Field.objectType = variableModel.Type;
            m_Field.allowSceneObjects = false;
#endif
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<ScriptableObject>>))]
    internal class ScriptableObjectListVariableElement : TypedListVariableElement<ScriptableObject, UnityEngine.Object>
    {
#if UNITY_EDITOR

        public ScriptableObjectListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(UnityEditor.UIElements.ObjectField))
#else
        public ScriptableObjectListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(RuntimeObjectField))
#endif
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<List<GameObject>>))]
    internal class GameObjectListVariableElement : TypedListVariableElement<GameObject, UnityEngine.Object>
    {
#if UNITY_EDITOR

        public GameObjectListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(UnityEditor.UIElements.ObjectField))
#else
        public GameObjectListVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable, typeof(RuntimeObjectField))
#endif
        {
        }
    }

    [VariableUI(typeof(TypedVariableModel<Enum>))]
    internal class EnumVariableElement : TypedVariableElement<Enum, Dropdown>
    {
        public EnumVariableElement(BlackboardView view, VariableModel variableModel, bool isEditable) : base(view, variableModel, isEditable)
        {
            m_Field.size = Size.M;
            Type variableEnumType = variableModel.ObjectValue.GetType();
            Array enumValues = Enum.GetValues(variableEnumType);
            m_Field.bindItem = (item, i) => item.label = Enum.GetName(variableEnumType, enumValues.GetValue(i));
            m_Field.sourceItems = enumValues;
            m_Field.SetValueWithoutNotify(GetEnumValueIndex((int)variableModel.ObjectValue, enumValues));
        }

        // Returns the index of the enum value in the enumValues array.
        public static IEnumerable<int> GetEnumValueIndex(int variableValue, Array enumValues)
        {
            var i = 0;
            foreach (var value in enumValues)
            {
                if ((int)value == variableValue) return new[] { i };
                ++i;
            }

            return null;
        }
    }
}
