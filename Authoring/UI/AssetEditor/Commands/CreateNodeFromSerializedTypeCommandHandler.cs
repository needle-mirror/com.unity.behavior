using System;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace Unity.Behavior
{
    internal class CreateNodeFromSerializedTypeCommandHandler : CommandHandler<CreateNodeFromSerializedTypeCommand>
    {
        public override bool Process(CreateNodeFromSerializedTypeCommand command)
        {
#if UNITY_6000_5_OR_NEWER
            Type type = CurrentAssemblies.GetLoadedAssemblies()
#else
            Type type = AppDomain.CurrentDomain.GetAssemblies()
#endif
                .SelectMany(x => x.GetTypes())
                .FirstOrDefault(t => typeof(Node).IsAssignableFrom(t) && t.Name == command.NodeTypeName);
            NodeInfo nodeInfo = type == null ? null : NodeRegistry.GetInfo(type);
            if (type == null || nodeInfo == null)
            {
                Debug.LogError($"Could not find type {command.NodeTypeName}");
                return false;
            }

            // Create node.
            NodeModel newNode = Asset.CreateNode(nodeInfo.ModelType, command.Position, null, new object[] { nodeInfo });

            // Connect default LinkField variables to fields.
            if (DispatcherContext is BehaviorGraphEditor behaviorGraphEditor &&
                newNode is BehaviorGraphNodeModel behaviorGraphNodeModel)
            {
                behaviorGraphEditor.LinkVariablesFromBlackboard(behaviorGraphNodeModel);
                behaviorGraphEditor.LinkRecentlyLinkedFields(behaviorGraphNodeModel);
                behaviorGraphNodeModel.OnValidate();
            }

            return true;
        }
    }
}
