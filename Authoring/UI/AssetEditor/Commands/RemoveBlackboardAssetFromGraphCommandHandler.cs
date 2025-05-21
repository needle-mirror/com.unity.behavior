using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    internal class RemoveBlackboardAssetFromGraphCommandHandler : CommandHandler<RemoveBlackboardAssetFromGraphCommand>
    {
        public override bool Process(RemoveBlackboardAssetFromGraphCommand command)
        {
            if (BlackboardView is not BehaviorGraphBlackboardView graphBlackboardView)
            {
                return false;
            }

            if (command.GraphAsset == null)
            {
                return false;
            }

            for (int index = 0; index < command.GraphAsset.m_Blackboards.Count; index++)
            {
                BehaviorBlackboardAuthoringAsset blackboardAuthoring = command.GraphAsset.m_Blackboards[index];
                if (blackboardAuthoring.AssetID == command.blackboardAuthoringAsset.AssetID)
                {
                    command.GraphAsset.m_Blackboards.Remove(blackboardAuthoring);
                }
            }

            // It is possible that the graph asset has been deleted at the same time as the blackboard,
            // In this case, the SaveAssets will refresh the database and the target graph asset will become invalid.
            if (command.GraphAsset == null)
            {
                return false;
            }

            foreach (VariableModel variable in command.blackboardAuthoringAsset.Variables)
            {
                DispatcherContext.Root.SendEvent(VariableDeletedEvent.GetPooled(DispatcherContext.Root, variable));    
            }
            
            graphBlackboardView.RequestBlackboardReferenceAssetsViewRefresh();
            graphBlackboardView.InitializeListView();

            // Have we processed the command and wish to block further processing?
            return false;
        }
    }
}
