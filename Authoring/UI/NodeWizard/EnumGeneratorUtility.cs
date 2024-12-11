#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            using (var outfile = new StreamWriter(path))
            {
                outfile.WriteLine("using System;");
                outfile.WriteLine("using Unity.Behavior;");
                outfile.WriteLine("");
                outfile.WriteLine("[BlackboardEnum]");
                outfile.WriteLine($"public enum {name}");
                outfile.WriteLine("{");
                outfile.WriteLine($"    {string.Join(",\n\t", members.Select(m => m.Replace(" ", string.Empty)).Where(m => !string.IsNullOrEmpty(m)))}");
                outfile.WriteLine("}");
            }
            AssetDatabase.Refresh();
            return true;
        }
    }
}
#endif