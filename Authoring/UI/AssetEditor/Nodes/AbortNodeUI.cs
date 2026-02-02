using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeUI(typeof(AbortNodeModel))]
    internal class AbortNodeUI : ConditionalNodeUI
    {
        private AbortNodeModel m_AbortNodeModel => Model as AbortNodeModel;
        private IObserverAbortNodeModel m_ObserverAbortNodeModel;

        public AbortNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            styleSheets.Add(ResourceLoadAPI.Load<StyleSheet>("Packages/com.unity.behavior/Authoring/UI/AssetEditor/Assets/ConditionNodeStylesheet.uss"));
            AddToClassList("Modifier");
            AddToClassList("TwoLineNode");
            m_ObserverAbortNodeModel = Model as IObserverAbortNodeModel;
        }

        private void UpdateNodeTitle()
        {
            string titleName = m_AbortNodeModel.ModelAbortType switch
            {
                AbortNodeModel.AbortType.Restart => nameof(AbortNodeModel.AbortType.Restart),
                _ => "Fail",
            };

            if (m_AbortNodeModel.ConditionModels.Count > 1)
            {
                Title = !m_AbortNodeModel.RequiresAllConditionsTrue
                    ? $"{titleName} If Any Are True"
                    : $"{titleName} If All Are True";
            }
            else
            {
                Title = $"{titleName} If";
            }

            // Add observer type to title if applicable
            Title += m_ObserverAbortNodeModel.GetObserverTypeUITitle();
        }

        public override void Refresh(bool isDragging)
        {
            base.Refresh(isDragging);
            UpdateNodeTitle();
        }
    }
}
