using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if !UNITY_EDITOR
// Don't remove this using (even if Rider claims it is unused), it is used in a runtime only part in GetEnumVariableTypes() 
using System.Reflection;
#endif
using UnityEngine;

namespace Unity.Behavior.GraphFramework
{
    internal static class BlackboardUtils
    {
        private static Dictionary<Type, Texture2D> m_VariableTypeIcons = new ()
        {
            { typeof(bool), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/boolean_icon.png") },
            { typeof(double), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/float_icon.png") },
            { typeof(string), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/string_icon.png") },
            { typeof(Color), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/color_icon.png") },
            { typeof(float), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/float_icon.png") },
            { typeof(int), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/integer_icon.png") },
            { typeof(Transform), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/position_icon.png") },
            { typeof(Vector2), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/vector2_icon.png") },
            { typeof(Vector3), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/vector3_icon.png") },
            { typeof(Vector4), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/vector4_icon.png") },
            { typeof(GameObject), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/object_icon.png") },
            { typeof(UnityEngine.Object), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/object_icon.png") },
            { typeof(Enum), ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/enum_icon.png") }
        };
        
        public static Texture2D GetIcon(this Type type)
        {
            if (type == null)
            {
                return ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/variable_icon.png");
            }

            if (type.IsEnum)
                return m_VariableTypeIcons[typeof(Enum)];

            if (m_VariableTypeIcons.TryGetValue(type, out var texture2D))
            {
                return texture2D;
            }

            return GetIcon(type.BaseType);
        }

        public static void AddCustomIcon(Type typeKey, Texture2D texture)
        {
            if (!m_VariableTypeIcons.ContainsKey(typeKey))
            {
                m_VariableTypeIcons.Add(typeKey, texture);
            }
        }
        
        public static Texture2D GetScriptableObjectIcon(ScriptableObject obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                // Get the icon of the ScriptableObject
                Texture2D iconTexture = EditorGUIUtility.ObjectContent(obj, obj.GetType()).image as Texture2D;
                return iconTexture;
#endif
            }

            return null;
        }

        public static string GetArrowUnicode()
        {
            return "\u2192";
        }

        public static string GetNewVariableName(string typeName, BlackboardAsset asset)
        {
            string variableName = $"New {typeName}";
            
            string pattern = @"^" + Regex.Escape(variableName) + @"(?: \((\d+)\))?$";

            if (asset == null)
            {
                return variableName;
            }

            int nextPostfix = 0;
            bool variableNameWithNoPostfixFound = false;
            foreach (VariableModel variable in asset.Variables)
            {
                if (variable.Name == variableName)
                {
                    variableNameWithNoPostfixFound = true;
                }
                Match match = Regex.Match(variable.Name, pattern);
                if (match.Success)
                {
                    if (match.Groups[1].Success)
                    {
                        int currentPostfix = int.Parse(match.Groups[1].Value);
                        if (currentPostfix > nextPostfix)
                        {
                            nextPostfix = currentPostfix;
                        }
                    }
                }
            }

            if (!variableNameWithNoPostfixFound)
            {
                return variableName;
            }

            return nextPostfix == 0 ? variableName + " (1)" : variableName + " (" + (nextPostfix + 1) + ")";
        }

        public static string GetNameForType(Type type)
        {
            if (type == typeof(float))
            {
                return "Float";
            }
            if (typeof(IList).IsAssignableFrom(type))
            {
                Type elementType = type.GetGenericArguments()[0];
                return $"{elementType.Name} List";
            }
            return type.Name;
        }

        public static Type GetVariableModelTypeForType(Type type)
        {
            return typeof(TypedVariableModel<>).MakeGenericType(type);
        }
    }
}