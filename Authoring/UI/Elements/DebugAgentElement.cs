using Unity.Behavior.GraphFramework;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.UIElements;
using Toggle = Unity.AppUI.UI.Toggle;

namespace Unity.Behavior
{
    internal class DebugAgentElement : VisualElement
    {
        internal bool CanAutoConnect => m_AutoConnectToggle.value;
        internal Toggle DebugToggle => m_DebugToggle;
        private readonly Toggle m_DebugToggle;
        private readonly Toggle m_AutoConnectToggle;

        private const string k_NoAgentSelectedText = "No agent selected";
        private const string k_AutoConnectText = "Auto-Connect Debugger";
        private const string k_AutoConnectTooltip = "When enabled, automatically connect debugger when selecting an agent in the scene.";

        public DebugAgentElement()
        {
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/UI/Elements/Assets/DebugAgentElementStylesheet.uss"));
            AddToClassList("DebugAgentElement");

            m_DebugToggle = new Toggle();
            m_DebugToggle.name = "DebugAgentToggle";
            m_DebugToggle.label = k_NoAgentSelectedText;
            m_DebugToggle.SetEnabled(false);

            HelpText debugLabel = new HelpText("Select which GameObject you want to target for debugging");
            debugLabel.name = "DebugInfoText";
            debugLabel.style.whiteSpace = WhiteSpace.Normal;

            m_AutoConnectToggle = new Toggle();
            m_AutoConnectToggle.name = "AutoConnectToggle";
            m_AutoConnectToggle.label = k_AutoConnectText;
            m_AutoConnectToggle.tooltip = k_AutoConnectTooltip;
            m_AutoConnectToggle.value = true;
            m_AutoConnectToggle.SetEnabled(true);

            Divider divider = new Divider();
            divider.size = Size.S;
            
            Add(m_DebugToggle);
            Add(divider);
            Add(m_AutoConnectToggle);
            Add(debugLabel);
        }

        internal void SetAgentToToggle(string agentName, bool isDisplayed)
        {
            m_DebugToggle.style.display = DisplayStyle.Flex;
            m_DebugToggle.SetEnabled(isDisplayed);
            m_DebugToggle.label = "Debugging " + agentName;
        }

        internal void ResetToggle()
        {
            m_DebugToggle.label = k_NoAgentSelectedText;
            m_DebugToggle.value = false;
            m_DebugToggle.SetEnabled(false);
        }
    }
}
