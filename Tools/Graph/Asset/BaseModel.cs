using System;

namespace Unity.Behavior.GraphFramework
{
    [Serializable]
    internal class BaseModel
    {
        public GraphAsset Asset { get; set; }
        public virtual IVariableLink GetVariableLink(string variableName, Type type) => null;
    }
}
