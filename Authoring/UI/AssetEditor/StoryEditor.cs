using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.Behavior.GraphFramework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIExtras;

namespace Unity.Behavior
{
#if ENABLE_UXML_UI_SERIALIZATION
    [UxmlElement]
#endif
    internal partial class StoryEditor : VisualElement
    {
#if !ENABLE_UXML_UI_SERIALIZATION
        internal new class UxmlFactory : UxmlFactory<StoryEditor, UxmlTraits> { }
#endif
        private const string k_VisualTreeAssetPath = "Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/StoryEditorLayout.uxml";
        private const string k_StyleSheetAssetPath = "Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/StoryEditorStylesheet.uss";

        private const string k_StoryFieldName = "StoryField";
        private const string k_PropertiesContainerName = "PropertiesContainer";

        protected readonly StoryFieldWithValidation StoryField;
        private List<BlackboardOption> m_SupportedTypes;
        public List<BlackboardOption> SupportedTypes
        {
            get => m_SupportedTypes;
            set
            {
                m_SupportedTypes = value;
#if UNITY_EDITOR
                CreateVariablesSearchOptions();
#endif
            }
        }
        internal List<SearchView.Item> m_SupportedTypesSearchItems;

        internal WordTypeSentence Sentence { get; private set; }

        internal event System.Action OnPropertyValueChanged;

        public StoryEditor()
        {
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>(k_StyleSheetAssetPath));
            ResourceLoadAPI.Load<VisualTreeAsset>(k_VisualTreeAssetPath).CloneTree(this);

            Sentence = new WordTypeSentence();

            StoryField = this.Q<StoryFieldWithValidation>(k_StoryFieldName);
            StoryField.Field.RegisterValueChangingCallback(OnStoryChanged);
            RefreshPropertiesUI();
            SupportedTypes = BlackboardRegistry.GetStoryVariableTypes();
        }

        internal void UpdateWordTypeList()
        {
            Sentence.UpdateWordTypeList(StoryField.Q<UnityEngine.UIElements.TextField>().cursorIndex, StoryField.Value);
        }

        internal void OnStoryChanged(ChangingEvent<string> evt)
        {
            // Update Sentence and UI.
            UpdateWordTypeList();
            RefreshPropertiesUI();
        }

        internal void RefreshPropertiesUI()
        {
            var propertiesContainer = this.Q<VisualElement>(k_PropertiesContainerName);
            propertiesContainer.Clear();

            if (!Sentence.WordTypePairs.Any())
            {
                return;
            }

            propertiesContainer.style.display = DisplayStyle.Flex;
            for (int i = 0; i < Sentence.WordTypePairs.Count; ++i)
            {
                propertiesContainer.Add(CreatePropertyUI(i));
            }

            // using a local function here to make sure the captured variable i in the value changed callback is correct
            VisualElement CreatePropertyUI(int i)
            {
                var wordTypePair = Sentence.WordTypePairs[i];
                VisualElement propertyUI = new VisualElement();
                propertyUI.AddToClassList("PropertyField");
                propertyUI.Add(new Label(wordTypePair.Word));

                ActionButton actionButton = new ActionButton();
                actionButton.AddToClassList("StoryDropdown");
                actionButton.userData = i;
                actionButton.size = Size.M;
                actionButton.label = SupportedTypes[0].Name;
                actionButton.trailingIcon = "caret-down";
                actionButton.trailingIconVariant = IconVariant.Fill;
                actionButton.Q<Icon>("appui-actionbutton__trailing-icon").AddToClassList("appui-picker__caret");
                actionButton.clicked += () =>
                {
                    void OnSearchItemSelected(SearchView.Item item)
                    {
                        OnPropertyDropdownValueChanged(actionButton, item);
                        actionButton.tooltip = item.Name;
                    }
                    SearchWindow.ShowInPopover("Variable Type", m_SupportedTypesSearchItems, OnSearchItemSelected, 260, 400, actionButton, true, sortSearchItems: false);
                };

                if (wordTypePair.Type != typeof(RegularText))
                {
                    var dropdownIndex = SupportedTypes.FindIndex(a => a.Type.Type == wordTypePair.Type);
                    actionButton.label = SupportedTypes[dropdownIndex].Name;
                }

                propertyUI.Add(actionButton);
                return propertyUI;
            }
        }

        internal void OnPropertyDropdownValueChanged(ActionButton dropdown, SearchView.Item item)
        {
            int i = dropdown.userData is int dropdownUserData ? dropdownUserData : 0;
            dropdown.label = item.Name;
            Sentence.SetWordType(i, Sentence.WordTypePairs[i].Word, item.Data as Type);
            StoryField.Validate();
            OnPropertyValueChanged?.Invoke();
        }

#if UNITY_EDITOR
        private void CreateVariablesSearchOptions()
        {
            m_SupportedTypesSearchItems = new();

            bool basicTypesIconIsSetup = false;
            bool vectorTypesIconIsSetup = false;
            bool resourcesIconIsSetup = false;
            bool listIconIsSetup = false;
            bool otherIconIsSetup = false;
            bool enumIconIsSetup = false;

            foreach (var item in SupportedTypes)
            {
                SetupIconsCategories(item, ref basicTypesIconIsSetup, ref vectorTypesIconIsSetup,
                    ref resourcesIconIsSetup, ref listIconIsSetup, ref otherIconIsSetup, ref enumIconIsSetup);

                if (item.IconImage != null)
                {
                    m_SupportedTypesSearchItems.Add(new SearchView.Item(item.Path, item.IconImage, data: item.Type?.Type));
                }
                else
                {
                    m_SupportedTypesSearchItems.Add(new SearchView.Item(item.Path, item.Icon, data: item.Type?.Type));
                }
            }
        }

        private void SetupIconsCategories(BlackboardOption item, ref bool basicTypesIconIsSetup, ref bool vectorTypesIconIsSetup,
            ref bool resourcesIconIsSetup, ref bool listIconIsSetup, ref bool otherIconIsSetup, ref bool enumIconIsSetup)
        {
            if (!basicTypesIconIsSetup && item.Path.StartsWith("Basic Types"))
            {
                var iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/float.png";
                if (EditorGUIUtility.isProSkin)
                {
                    iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/d_float.png";
                }

                m_SupportedTypesSearchItems.Add(new SearchView.Item("Basic Types", ResourceLoadAPI.Load<Texture2D>(iconPath), data: null));
                basicTypesIconIsSetup = true;
            }
            else if (!vectorTypesIconIsSetup && item.Path.StartsWith("Vector Types"))
            {
                var iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/Vector3.png";
                if (EditorGUIUtility.isProSkin)
                {
                    iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/d_Vector3.png";
                }

                m_SupportedTypesSearchItems.Add(new SearchView.Item("Vector Types", ResourceLoadAPI.Load<Texture2D>(iconPath), data: null));
                vectorTypesIconIsSetup = true;
            }
            else if (!resourcesIconIsSetup && item.Path.StartsWith("Resources"))
            {
                var iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/source.png";
                if (EditorGUIUtility.isProSkin)
                {
                    iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/d_source.png";
                }

                m_SupportedTypesSearchItems.Add(new SearchView.Item("Resources", ResourceLoadAPI.Load<Texture2D>(iconPath), data: null));
                resourcesIconIsSetup = true;
            }
            else if (!listIconIsSetup && item.Path.StartsWith("List"))
            {
                var iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/list.png";
                if (EditorGUIUtility.isProSkin)
                {
                    iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/d_list.png";
                }

                m_SupportedTypesSearchItems.Add(new SearchView.Item("List", ResourceLoadAPI.Load<Texture2D>(iconPath), data: null));
                listIconIsSetup = true;
            }
            else if (!otherIconIsSetup && item.Path.StartsWith("Other"))
            {
                var iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/others.png";
                if (EditorGUIUtility.isProSkin)
                {
                    iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/d_others.png";
                }

                m_SupportedTypesSearchItems.Add(new SearchView.Item("Other", ResourceLoadAPI.Load<Texture2D>(iconPath), data: null));
                otherIconIsSetup = true;
            }
            else if (!enumIconIsSetup && item.Path.StartsWith("Enumeration"))
            {
                var iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/enum.png";
                if (EditorGUIUtility.isProSkin)
                {
                    iconPath = "Packages/com.unity.behavior/Blackboard/Assets/Icons/Variables/d_enum.png";
                }

                m_SupportedTypesSearchItems.Add(new SearchView.Item("Enumeration", ResourceLoadAPI.Load<Texture2D>(iconPath), data: null));
                enumIconIsSetup = true;
            }
        }
#endif
    }
}
