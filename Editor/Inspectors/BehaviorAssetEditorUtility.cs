using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    internal static class BehaviorAssetEditorUtility
    {
        public static void AddSeparator(VisualElement parent)
        {
            var separator = new VisualElement();
            separator.AddToClassList("behavior-separator");
            parent.Add(separator);
        }

        public static void AddSpace(VisualElement parent)
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("behavior-spacer");
            parent.Add(spacer);
        }

        public static void DrawBlackboardVariable(VisualElement container, BlackboardAsset asset)
        {
            if (asset == null || asset.Variables == null || asset.Variables.Count == 0)
            {
                return;
            }

            var variablesLabel = new Label("Variables");
            variablesLabel.AddToClassList("behavior-section-title");
            container.Add(variablesLabel);

            foreach (var bbv in asset.Variables)
            {
                Label variableLabel = null;
                if (bbv == null)
                {
                    variableLabel = new Label("Unknown Variable (Missing Type)");
                    continue;
                }
                else
                {
                    // Variable label with context menu
                    variableLabel = new Label($"{bbv.Name} ({GraphFramework.BlackboardUtils.GetNameForType(bbv.Type)})");
                }
                variableLabel.AddToClassList("behavior-variable-label");

                // Add context menu manipulator
                var contextMenuManipulator = new ContextualMenuManipulator((evt) =>
                {
                    evt.menu.AppendAction("Copy GUID", (action) =>
                    {
                        EditorGUIUtility.systemCopyBuffer = bbv.ID.ToString();
                    });

                    evt.menu.AppendAction("Copy Variable Name", (action) =>
                    {
                        EditorGUIUtility.systemCopyBuffer = bbv.Name;
                    });
                });

                variableLabel.AddManipulator(contextMenuManipulator);
                container.Add(variableLabel);
            }
        }

        public static void DrawCustomHelpbox(VisualElement parent,
            bool useErrorIcon,
            string helpboxMessage,
            System.Action printMessageToConsoleAction)
        {
            var customHelpBox = new Box();
            customHelpBox.AddToClassList("behavior-helpbox");

            var icon = new VisualElement();
            icon.AddToClassList(useErrorIcon ? "behavior-error-icon" : "behavior-warning-icon");

            var contentContainer = new VisualElement();
            contentContainer.AddToClassList("behavior-helpbox-content-container");

            var messageText = new Label(helpboxMessage);
            messageText.AddToClassList("behavior-helpbox-message-text");

            var troubleshootingContainer = new VisualElement();
            troubleshootingContainer.AddToClassList("behavior-helpbox-troubleshooting-container");

            var printButton = new Button(printMessageToConsoleAction)
            {
                text = "Print to console"
            };
            printButton.AddToClassList("behavior-helpbox-button");

            var hyperlink = new Label("View documentation");
            hyperlink.AddToClassList("behavior-helpbox-hyperlink");
            hyperlink.RegisterCallback<ClickEvent>(_ =>
            {
                Application.OpenURL(ErrorMessages.k_TroubleshootingLink);
            });

            troubleshootingContainer.Add(printButton);
            troubleshootingContainer.Add(hyperlink);

            contentContainer.Add(messageText);
            contentContainer.Add(troubleshootingContainer);

            customHelpBox.Add(icon);
            customHelpBox.Add(contentContainer);

            parent.Add(customHelpBox);
        }

        public static VisualElement CreateBehaviorAssetField(ScriptableObject asset)
        {
            var row = new VisualElement();
            row.AddToClassList("behavior-asset-field");

            var icon = new VisualElement();
            icon.AddToClassList("behavior-asset-field__icon");
            icon.AddToClassList(GetAssetIconClass(asset));
            row.Add(icon);

            var assetName = asset != null ? asset.name : "(Missing)";
            var label = new Label(assetName);
            label.AddToClassList("behavior-asset-field__label");
            row.Add(label);

            var menu = new ToolbarMenu();
            menu.AddToClassList("behavior-asset-field__menu");
            menu.menu.AppendAction("Ping Asset",
                _ =>
                {
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                },
                _ => asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            row.Add(menu);

            return row;
        }

        public static Button CreateEditButton(ScriptableObject asset, string buttonText)
        {
            var button = new Button(() =>
            {
                if (asset != null)
                {
                    AssetDatabase.OpenAsset(asset);
                }
            })
            {
                text = buttonText
            };
            button.AddToClassList("behavior-edit-button");
            return button;
        }

        private static string GetAssetIconClass(ScriptableObject asset)
        {
            if (asset is BlackboardAsset || asset is RuntimeBlackboardAsset)
            {
                return "behavior-icon--blackboard-asset";
            }
            if (asset is GraphAsset || asset is BehaviorGraph)
            {
                return "behavior-icon--graph-asset";
            }
            return "behavior-icon--graph-asset";
        }
    }
}
