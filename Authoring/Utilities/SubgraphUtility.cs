using System.Collections.Generic;
using System.Linq;

namespace Unity.Behavior
{
    internal static class SubgraphUtility
    {
        internal static bool ContainsCyclicReferenceTo(this BehaviorAuthoringGraph subgraphAsset, BehaviorAuthoringGraph parentAsset)
        {
            // Null assets can't reference the parent asset.
            if (!subgraphAsset)
            {
                return false;
            }

            // Trivial check: see if the two assets are the same.
            if (subgraphAsset == parentAsset)
            {
                return true;
            }

#if UNITY_EDITOR
            // If the parent asset don't have a runtime asset, we have nothing to compare against.
            if (!parentAsset.HasRuntimeGraph)
            {
                return false;
            }
#endif

            // Detect if the subgraph has any references to this node's graph asset.
            bool cycleDetected = false;
            HashSet<BehaviorAuthoringGraph> visitedSubgraphs = new() { subgraphAsset };
            List<BehaviorAuthoringGraph> subgraphsToCheck = new() { subgraphAsset };
            
            BehaviorGraph runtimeGraphToCheck = BehaviorAuthoringGraph.GetOrCreateGraph(parentAsset);            
            while (subgraphsToCheck.Count != 0)
            {
                var subgraph = subgraphsToCheck[0];
                subgraphsToCheck.Remove(subgraph);

                if (subgraph.Nodes.OfType<SubgraphNodeModel>().Any(node => node.RuntimeSubgraph == runtimeGraphToCheck))
                {
                    cycleDetected = true;
                    break;
                }

                // Queue subgraphs for checking
                foreach (var subgraphNode in subgraph.Nodes.OfType<SubgraphNodeModel>())
                {
                    if (subgraphNode.RuntimeSubgraph && subgraphNode.SubgraphAuthoringAsset && visitedSubgraphs.Add(subgraphNode.SubgraphAuthoringAsset))
                    {
                        subgraphsToCheck.Add(subgraphNode.SubgraphAuthoringAsset);
                    }
                }
            }

            return cycleDetected;
        }

        internal static bool ContainsStaticSubgraphReferenceTo(this BehaviorAuthoringGraph parentAsset, BehaviorAuthoringGraph subgraphAsset)
        {
            if (!subgraphAsset || subgraphAsset == parentAsset)
            {
                return false;
            }

            // If the parent already have a dependency, early out.
            if (parentAsset.HasSubgraphDependency(subgraphAsset))
            {
                return true;
            }

#if UNITY_EDITOR
            // If the target asset don't have a runtime asset, we won't find a reference.
            if (!subgraphAsset.HasRuntimeGraph)
            {
                return false;
            }
#endif
            // Detect if the subgraph has any references to this node's graph asset.
            BehaviorGraph runtimeSubgraphToCheck = BehaviorAuthoringGraph.GetOrCreateGraph(subgraphAsset);
            HashSet<BehaviorAuthoringGraph> visitedSubgraphs = new() { parentAsset };
            Queue<BehaviorAuthoringGraph> graphsToCheck = new();
            graphsToCheck.Enqueue(parentAsset);

            while (graphsToCheck.TryDequeue(out var workingGraph))
            {
                if (workingGraph.Nodes.OfType<SubgraphNodeModel>().Any(node => !node.IsDynamic && node.RuntimeSubgraph == runtimeSubgraphToCheck))
                {
                    return true;
                }

                // Queue subgraphs for checking
                foreach (var subgraphNode in workingGraph.Nodes.OfType<SubgraphNodeModel>())
                {
                    if (workingGraph != parentAsset && subgraphNode.RuntimeSubgraph && subgraphNode.SubgraphAuthoringAsset 
                        && visitedSubgraphs.Add(subgraphNode.SubgraphAuthoringAsset))
                    {
                        graphsToCheck.Enqueue(subgraphNode.SubgraphAuthoringAsset);
                    }
                }
            }

            return false;
        }

    }
}