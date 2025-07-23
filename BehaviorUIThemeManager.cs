using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Behavior.GraphFramework
{

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class BehaviorUIThemeManager
    {
#if UNITY_EDITOR
        private static bool s_LastKnownThemeIsDark;
#endif
        private static string lastKnownThemeKey = "lastKnownThemeKey";
        private const string DarkThemeStylesheetPath = "Packages/com.unity.behavior/Elements/Assets/BehaviorUIStylesheet_Dark.uss";
        private const string LightThemeStylesheetPath = "Packages/com.unity.behavior/Elements/Assets/BehaviorUIStylesheet_Light.uss";

        // Cache the stylesheets for better performance
        private static StyleSheet s_DarkThemeStylesheet;
        private static StyleSheet s_LightThemeStylesheet;

        // Track registered elements with weak references to avoid memory leaks
        private static readonly List<WeakReference<VisualElement>> s_RegisteredElements = new List<WeakReference<VisualElement>>();

#if UNITY_EDITOR
        public static bool IsDarkTheme => EditorGUIUtility.isProSkin;
#else
        public static bool IsDarkTheme => true;
#endif
        public static event System.Action ThemeChanged;

        static BehaviorUIThemeManager()
        {
#if UNITY_EDITOR
            s_LastKnownThemeIsDark = SessionState.GetBool(lastKnownThemeKey, IsDarkTheme);
            if (s_LastKnownThemeIsDark == IsDarkTheme)
            {
                SessionState.SetBool(lastKnownThemeKey, IsDarkTheme);
            }
 
            EditorApplication.update += CheckThemeChange;
#endif
            // Preload the stylesheets
            LoadStyleSheets();
        }

        private static void LoadStyleSheets()
        {
            if (!s_DarkThemeStylesheet)
            {
                s_DarkThemeStylesheet = ResourceLoadAPI.Load<StyleSheet>(DarkThemeStylesheetPath);
            }

            if (!s_LightThemeStylesheet)
            {
                s_LightThemeStylesheet = ResourceLoadAPI.Load<StyleSheet>(LightThemeStylesheetPath);
            }
        }

        /// <summary>
        /// Register a visual element to automatically receive theme updates
        /// </summary>
        public static void RegisterElement(VisualElement element)
        {
            if (element == null)
                return;

            ApplyThemeToElement(element);

            // Check if the element is already registered
            for (int i = 0; i < s_RegisteredElements.Count; i++)
            {
                if (s_RegisteredElements[i].TryGetTarget(out var target) && target == element)
                {
                    return;
                }
            }

            s_RegisteredElements.Add(new WeakReference<VisualElement>(element));
        }

        /// <summary>
        /// Unregister a visual element from automatic theme updates
        /// </summary>
        public static void UnregisterElement(VisualElement element)
        {
            if (element == null)
                return;

            for (int i = s_RegisteredElements.Count - 1; i >= 0; i--)
            {
                if (s_RegisteredElements[i].TryGetTarget(out var target) && target == element)
                {
                    s_RegisteredElements.RemoveAt(i);
                }
            }
        }

        public static StyleSheet GetThemeStyleSheet()
        {
            return IsDarkTheme ? s_DarkThemeStylesheet : s_LightThemeStylesheet;
        }

        public static void ApplyThemeToElement(VisualElement element)
        {
            if (s_DarkThemeStylesheet == null || s_LightThemeStylesheet == null)
            {
                LoadStyleSheets();
            }

            if (element == null || element.styleSheets == null || s_DarkThemeStylesheet == null || s_LightThemeStylesheet == null)
            {
                return;
            }

            if (element.styleSheets.Contains(s_DarkThemeStylesheet))
            {
                element.styleSheets.Remove(s_DarkThemeStylesheet);
            }
            if (element.styleSheets.Contains(s_LightThemeStylesheet))
            {
                element.styleSheets.Remove(s_LightThemeStylesheet);
            }

            element.styleSheets.Add(GetThemeStyleSheet());
        }

#if UNITY_EDITOR
        private static void CheckThemeChange()
        {
            if (s_LastKnownThemeIsDark != IsDarkTheme)
            {
                s_LastKnownThemeIsDark = IsDarkTheme;
                SessionState.SetBool(lastKnownThemeKey, IsDarkTheme);

                // Update all registered elements
                UpdateAllRegisteredElements();

                // Still fire the event for any legacy code
                ThemeChanged?.Invoke();
            }
        }
#endif

        private static void UpdateAllRegisteredElements()
        {
            for (int i = s_RegisteredElements.Count - 1; i >= 0; i--)
            {
                if (s_RegisteredElements[i].TryGetTarget(out var element))
                {
                    ApplyThemeToElement(element);
                }
                else
                {
                    // Remove dead references
                    s_RegisteredElements.RemoveAt(i);
                }
            }
        }
    }
}
