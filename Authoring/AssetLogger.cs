using System.Text;
using Unity.Behavior.GraphFramework;
using UnityEngine;

namespace Unity.Behavior
{
    internal class AssetLogger
    {
        // Provides a mechanism to get rid of redundant error messages that can arise when loading asset in memory.
        public static bool CanLogMissingTypeInManagedRefErrorMessage { get; set; } = true;

        private static AssetLogger s_Instance;
        private static AssetLogger Instance => s_Instance ??= new AssetLogger();
        private readonly StringBuilder m_Messages = new StringBuilder();
        private int m_AlteredNodeCount = 0;
        private LogType m_LogType;

        public static void Reset()
        {
            Instance.m_AlteredNodeCount = 0;
            Instance.m_Messages.Clear();
            Instance.m_LogType = LogType.Log;
        }

        public static void RecordNodeMigration(string nodeName, string oldType, string newType)
        {
            Instance.m_AlteredNodeCount++;
            Instance.m_Messages.AppendLine($"\t- Node '{nodeName}': migrated from '{oldType}' to '{newType}'");
            Instance.m_LogType = LogType.Warning;
        }

        public static void RecordNodeResolution(string nodeName, string newType)
        {
            Instance.m_AlteredNodeCount++;
            Instance.m_Messages.AppendLine($"\t- Node '{nodeName}': resolved placeholder, '{newType}' found");
        }

        public static void RecordNodeAsPlaceholder(string nodeName, string oldType)
        {
            Instance.m_AlteredNodeCount++;
            Instance.m_Messages.AppendLine($"\t- Node '{nodeName}': converted to placeholder because '{oldType}' is missing");
        }

        public static void RecordAssetResolution(ScriptableObject asset)
        {
            Instance.m_Messages.AppendLine($"\t- '{asset.name} ({asset.GetType().Name})'");
        }

        public static void RecordPlaceholderStripping(BehaviorAuthoringGraph graph, NodeModel nodeModel)
        {
            Instance.m_AlteredNodeCount++;

            string nodeName = "Unknown";
            string type = "Unknown";
            if (nodeModel is BehaviorGraphNodeModel behaviorNodeModel
                && graph.RuntimeNodeTypeIDToNodeModelInfo.TryGetValue(behaviorNodeModel.NodeTypeID, out var nodeModelInfo))
            {
                nodeName = nodeModelInfo.Name;
                type = nodeModelInfo.RuntimeTypeString;
            }

            Instance.m_Messages.AppendLine($"\t- Node '{nodeName}' ({type})");
            Instance.m_LogType = LogType.Warning;
        }

        public static void LogAssetManagedReferenceError(ScriptableObject asset)
        {
            if (CanLogMissingTypeInManagedRefErrorMessage)
            {
                ErrorMessages.LogSerializedReferenceWindowError(asset);
            }
        }

        public static void LogResults(Object context, string operationType)
        {
            if (Instance.m_Messages.Length == 0 && Instance.m_AlteredNodeCount == 0)
                return;

            string header = GenerateHeader(context.name, Instance.m_AlteredNodeCount, operationType);
            string message = $"{header}\n{Instance.m_Messages}";

            switch (Instance.m_LogType)
            {
                case LogType.Log:
                    Debug.Log(message, context);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(message + "\nInspect the asset for more information.", context);
                    break;
                case LogType.Error:
                    Debug.LogError(message + "\nInspect the asset for more information.", context);
                    break;
            }
        }

        private static string GenerateHeader(string assetName, int count, string description)
        {
            return count switch
            {
                0 => $"'{assetName}' {description}",
                1 => $"'{assetName}' {description}: 1 node affected",
                _ => $"'{assetName}' {description}: {count} nodes affected"
            };
        }
    }
}
