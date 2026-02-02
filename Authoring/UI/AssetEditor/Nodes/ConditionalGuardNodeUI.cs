using Unity.Behavior.GraphFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeUI(typeof(ConditionalGuardNodeModel))]
    internal class ConditionalGuardNodeUI : ConditionalNodeUI
    {
        private ConditionalGuardNodeModel m_ConditionNodeModel => Model as ConditionalGuardNodeModel;
        private IObserverAbortNodeModel m_ObserverAbortNodeModel;
        private ObserverAbortTarget m_lastObserverType = (ObserverAbortTarget)(-1);
        bool m_ModifierMode = false;
        public ConditionalGuardNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            AddToClassList("ConditionalAction");
            AddToClassList("Condition");
            AddToClassList("ShowNodeColor");
            AddToClassList("Composite");
            AddToClassList("TwoLineNode");

            EnableInClassList("Composite", false);
            EnableInClassList("TwoLineNode", false);
            m_ModifierMode = false;

            UpdateGuardConditionElement();
            CreateNodeConditionElements();

            NodeValueContainer.style.display = DisplayStyle.Flex;
            m_ObserverAbortNodeModel = Model as IObserverAbortNodeModel;
        }

        public override void Refresh(bool isDragging)
        {
            if (m_ConditionNodeModel.CanUseObserverAbort() == false)
            {
                if (m_ModifierMode)
                {
                    m_ConditionNodeModel.Mode = ConditionalGuardNodeModel.GuardMode.Action;
                    EnableInClassList("Composite", false);
                    EnableInClassList("TwoLineNode", false);
                    EnableInClassList("ShowNodeColor", true);
                    EnableInClassList("ConditionalAction", true);
                    m_ModifierMode = false;
                    UpdateLinkFields();
                }
            }
            else if (!m_ModifierMode || m_lastObserverType != m_ObserverAbortNodeModel.ObserverType)
            {
                m_ConditionNodeModel.Mode = ConditionalGuardNodeModel.GuardMode.Modifier;
                EnableInClassList("Composite", true);
                EnableInClassList("TwoLineNode", true);
                EnableInClassList("ShowNodeColor", false);
                EnableInClassList("ConditionalAction", false);
                m_lastObserverType = m_ObserverAbortNodeModel.ObserverType;
                m_ModifierMode = true;
                UpdateLinkFields();
            }

            if (m_ModifierMode)
            {
                Title = "Pass";
                if (ConditionalNodeModel.ConditionModels.Count > 1)
                {
                    Title += ConditionalNodeModel.RequiresAllConditionsTrue ? " If All Are True" : " If Any Are True";
                }
                else
                {
                    Title += " If";
                }

                Title += m_ObserverAbortNodeModel.GetObserverTypeUITitle();
            }
            else
            {
                Title = string.Empty;
            }

            base.Refresh(isDragging);
        }

        internal override void UpdateLinkFields()
        {
            UpdateGuardConditionElement();
            base.UpdateLinkFields();
        }

        private void UpdateGuardConditionElement()
        {
            if (m_ModifierMode)
            {
                ConditionElementPrefix = string.Empty;
            }
            else
            {
                if (ConditionalNodeModel.ConditionModels.Count > 1)
                {
                    if (m_ConditionNodeModel.ShouldTruncateNodeUI)
                    {
                        ConditionElementPrefix = ConditionalNodeModel.RequiresAllConditionsTrue ? "If all of" : "If any of";
                    }
                    else
                    {
                        ConditionElementPrefix = ConditionalNodeModel.RequiresAllConditionsTrue ? "<b>AND</b> if" : "<b>OR</b> if";
                    }
                }
                else
                {
                    ConditionElementPrefix = "If";
                }
            }
        }
    }
}
