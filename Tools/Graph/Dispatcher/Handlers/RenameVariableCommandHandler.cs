using Unity.Behavior.GraphFramework;

internal class RenameVariableCommandHandler : CommandHandler<RenameVariableCommand>
{
    public override bool Process(RenameVariableCommand command)
    {
        if (BlackboardHasVariableWithName(command.NewName))
        {
            // If a variable with the given name already exists, refresh the variable element to reset the field and return.
            BlackboardView.RefreshVariableItem(command.Variable);
            return true;
        }

        command.Variable.Name = command.NewName;
        DispatcherContext.Root.SendEvent(VariableRenamedEvent.GetPooled(DispatcherContext.Root, command.Variable));
        BlackboardAsset.InvokeBlackboardChanged();

        return true;
    }

    private bool BlackboardHasVariableWithName(string newName)
    {
        foreach (VariableModel variable in BlackboardAsset.Variables)
        {
            if (variable.Name == newName)
            {
                return true;
            }
        }

        return false;
    }
}
