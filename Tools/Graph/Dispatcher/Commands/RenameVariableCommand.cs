namespace Unity.Behavior.GraphFramework
{
    internal class RenameVariableCommand : Command, IBlackboardAssetCommand
    {
        public VariableModel Variable { get; }
        public string NewName { get; }

        public RenameVariableCommand(VariableModel variable, string newName, bool markUndo = true) : base(markUndo)
        {
            Variable = variable;
            NewName = newName;
        }
    }
}