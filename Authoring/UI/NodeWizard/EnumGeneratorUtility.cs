using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace Unity.Behavior
{
    internal static class EnumGeneratorUtility
    {
        internal static bool CreateEnumAsset(string name, List<string> members)
        {
            string suggestedSavePath = Util.GetAbsolutePathToProjectAssets(BehaviorProjectSettings.instance.SaveFolderEnum);
            var path = EditorUtility.SaveFilePanel($"Create Enum \"{name}\"", suggestedSavePath, name, "cs");

            if (path.Length == 0)
            {
                return false;
            }
            if (BehaviorProjectSettings.instance.AutoSaveLastSaveLocation)
            {
                BehaviorProjectSettings.instance.SaveFolderEnum = Path.GetDirectoryName(path);
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine("using System;");
            output.AppendLine("using Unity.Behavior;");
            output.AppendLine();
            output.AppendLine("[BlackboardEnum]");
            output.AppendLine($"public enum {name}");
            output.AppendLine("{");
            output.AppendLine($"\t{string.Join($",{Environment.NewLine}\t", members.Select(m => m.Replace(" ", string.Empty)).Where(m => !string.IsNullOrEmpty(m)))}");
            output.AppendLine("}");

            File.WriteAllText(path, output.ToString(), new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            return true;
        }

        internal static bool CreateEnumFlagAsset(string name, List<string> members)
        {
            string suggestedSavePath = Util.GetAbsolutePathToProjectAssets(BehaviorProjectSettings.instance.SaveFolderEnum);
            var path = EditorUtility.SaveFilePanel($"Create Flag Enum \"{name}\"", suggestedSavePath, name, "cs");

            if (path.Length == 0)
            {
                return false;
            }
            
            if (BehaviorProjectSettings.instance.AutoSaveLastSaveLocation)
            {
                BehaviorProjectSettings.instance.SaveFolderEnum = Path.GetDirectoryName(path);
            }

            List<string> flagMembers = new List<string>();
            int index = 0;

            foreach (string member in members)
            {
                string cleanMember = member.Replace(" ", string.Empty);
                if (string.IsNullOrEmpty(cleanMember))
                {
                    continue;
                }

                flagMembers.Add(cleanMember + $" = 1 << {index++}");
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine("using System;");
            output.AppendLine("using Unity.Behavior;");
            output.AppendLine();
            output.AppendLine("[Flags]");
            output.AppendLine("[BlackboardEnum]");
            output.AppendLine($"public enum {name}");
            output.AppendLine("{");
            output.AppendLine($"\t{string.Join($",{Environment.NewLine}\t", flagMembers)}");
            output.AppendLine("}");

            File.WriteAllText(path, output.ToString(), new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            return true;
        }
    }
}
