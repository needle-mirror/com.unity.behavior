using System.Collections.Generic;
using System.Linq;

namespace Unity.Behavior
{
    internal static class BlackboardUtility
    {
        internal static bool ContainsReferenceTo(this BehaviorAuthoringGraph graph, BehaviorBlackboardAuthoringAsset blackboard)
        {
            // Null assets can't reference the parent asset.
            if (!graph)
            {
                return false;
            }

            // Detect if the subgraph has any references to this node's graph asset.
            HashSet<BehaviorAuthoringGraph> visitedSubgraphs = new() { graph };
            List<BehaviorAuthoringGraph> subgraphsToCheck = new() { graph };
            while (subgraphsToCheck.Count != 0)
            {
                var subgraph = subgraphsToCheck[0];
                subgraphsToCheck.Remove(subgraph);

                if (subgraph.m_Blackboards.Any(foundBlackboardAsset => foundBlackboardAsset == blackboard))
                {
                    return true;
                }
                
                // Queue subgraphs for checking
                foreach (var subgraphNode in subgraph.Nodes.OfType<SubgraphNodeModel>())
                {
                    if (subgraphNode.RequiredBlackboard == blackboard)
                    {
                        return true;
                    }
                    if (subgraphNode.RuntimeSubgraph && visitedSubgraphs.Add(subgraphNode.SubgraphAuthoringAsset))
                    {
                        subgraphsToCheck.Add(subgraphNode.SubgraphAuthoringAsset);
                    }
                }
            }
            return false;
        }
    }
}