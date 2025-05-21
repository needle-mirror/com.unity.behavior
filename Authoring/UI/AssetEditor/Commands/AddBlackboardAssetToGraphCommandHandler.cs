using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    internal class AddBlackboardAssetToGraphCommandHandler : CommandHandler<AddBlackboardAssetToGraphCommand>
    {
        public override bool Process(AddBlackboardAssetToGraphCommand command)
        {
            if (BlackboardView is not BehaviorGraphBlackboardView graphBlackboardView)
            {
                return false;
            }
            
            if (!command.GraphAsset.m_Blackboards.Contains(command.blackboardAuthoringAsset))
            {
                BehaviorBlackboardAuthoringAsset assetReference = command.blackboardAuthoringAsset;
                command.GraphAsset.m_Blackboards.Add(assetReference);
                graphBlackboardView.RequestBlackboardReferenceAssetsViewRefresh();
                graphBlackboardView.InitializeListView();
            }

            // Have we processed the command and wish to block further processing?
            return false;
        }
    }
}
