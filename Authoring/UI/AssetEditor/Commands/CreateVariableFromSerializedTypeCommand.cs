using System;
using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    [Serializable]
    internal class CreateVariableFromSerializedTypeCommand : Command, IBlackboardAssetCommand
    {
        public string VariableTypeName;
        
        public CreateVariableFromSerializedTypeCommand(string variableTypeName, bool markUndo) : base(markUndo)
        {
            VariableTypeName = variableTypeName;
        }
    }
}