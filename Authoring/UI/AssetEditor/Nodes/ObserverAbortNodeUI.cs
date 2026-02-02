using Unity.Behavior.GraphFramework;
using UnityEngine.UIElements;

namespace Unity.Behavior
{
    [NodeUI(typeof(ObserverAbortNodeModel))]
    internal class ObserverAbortNodeUI : ConditionalNodeUI
    {
        public ObserverAbortNodeUI(NodeModel nodeModel) : base(nodeModel)
        {
            AddToClassList("Modifier");
            AddToClassList("TwoLineNode");
        }

        public override void Refresh(bool isDragging)
        {
            var observerAbortNodeModel = Model as IObserverAbortNodeModel;

            if (Model is BehaviorGraphNodeModel graphNodeModel && !graphNodeModel.CanUseObserverAbort())
            {
                EnableInClassList("TwoLineNode", false);

                Title = "Priority Abort (Inactive)";
                tooltip = BehaviorGraphNodeInspectorUI.k_ObserverAbortDisabledTooltip + 
                    "\nWhile inactive, the node will be replaced by an implicit sequence at runtime.";
            }
            else
            {
                EnableInClassList("TwoLineNode", true);
                tooltip = string.Empty;

                Title = observerAbortNodeModel.GetObserverTypeUITitle().Remove(0, 2); // get rid of the beginning
                if (ConditionalNodeModel.ConditionModels.Count > 1)
                {
                    Title += ConditionalNodeModel.RequiresAllConditionsTrue ? " (All Conditions)" : " (Any Condition)";
                }
            }


            base.Refresh(isDragging);
        }
    }
}

