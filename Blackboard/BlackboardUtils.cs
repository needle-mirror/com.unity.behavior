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
        private static Dictionary<Type, string> m_VariableTypeIconNames = new Dictionary<Type, string>
        {
            { typeof(bool), "boolean" },
            { typeof(double), "double" },
            { typeof(string), "string" },
            { typeof(Color), "color" },
            { typeof(float), "float" },
            { typeof(int), "integer" },
            { typeof(Vector2), "vector2" },
            { typeof(Vector2Int), "vector2" },
            { typeof(Vector3), "vector3" },
            { typeof(Vector3Int), "vector3" },
            { typeof(Vector4), "vector4"},
            { typeof(GameObject), "object" },
            { typeof(UnityEngine.Object), "object" },
            { typeof(Enum), "enum" },
            { typeof(List<GameObject>), "list-object" },
            { typeof(List<string>),"list-string" },
            { typeof(List<float>), "list-float" },
            { typeof(List<int>),"list-integer" },
            { typeof(List<double>), "list-double" },
            { typeof(List<bool>),"list-boolean" },
            { typeof(List<Vector2>),"list-vector2" },
            { typeof(List<Vector3>), "list-vector3" },
            { typeof(List<Vector4>),"list-vector4" },
            { typeof(List<Vector2Int>), "list-vector2" },
            { typeof(List<Vector3Int>), "list-vector3" },
            { typeof(List<Color>), "list-color" }
        };

        public static string GetIconNameForType(Type type)
        {
            if (type.IsEnum)
            {
                return m_VariableTypeIconNames[typeof(Enum)];
            } 
            if (m_VariableTypeIconNames.TryGetValue(type, out string iconName))
            {
                return iconName;
            }

            return string.Empty;
        }
        
        public static Texture2D GetIcon(this Type type)
        {
            if (type == null)
            {
                return default;
                // Todo: Should we get rid of this old default icon? Using the App UI default for now if an icon can't be found.
                // return ResourceLoadAPI.Load<Texture2D>("Packages/com.unity.behavior/Blackboard/Assets/Icons/variable_icon.png");
            }

#if UNITY_EDITOR
            var icon = EditorGUIUtility.ObjectContent(null, type).image as Texture2D;
            if (icon != null)
            {
                return icon;
            }
#endif

            return GetIcon(type.BaseType);
        }

        public static void AddCustomIconName(Type variableType, string iconName)
        {
            if (!m_VariableTypeIconNames.ContainsKey(variableType))
            {
                m_VariableTypeIconNames.Add(variableType, iconName);
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