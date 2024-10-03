namespace Unity.Behavior.GraphFramework
{
    internal class SetVariableIsSharedCommand : Command
    {
        public VariableModel Variable { get; }
        public bool NewValue { get; set; }

        public SetVariableIsSharedCommand(VariableModel variable, bool newValue, bool markUndo = true) : base(markUndo)
        {
            Variable = variable;
            NewValue = newValue;
        }
    }
}