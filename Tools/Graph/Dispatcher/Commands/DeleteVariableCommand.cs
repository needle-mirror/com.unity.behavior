namespace Unity.Behavior.GraphFramework
{
    internal class DeleteVariableCommand : Command, IBlackboardAssetCommand
    {
        public VariableModel Variable { get; }
        
        public DeleteVariableCommand(VariableModel variable, bool markUndo = true) : base(markUndo)
        {
            Variable = variable;
        }
    }
}