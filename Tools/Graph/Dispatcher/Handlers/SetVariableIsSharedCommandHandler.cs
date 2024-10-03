using Unity.Behavior.GraphFramework;

internal class SetVariableIsSharedCommandHandler : CommandHandler<SetVariableIsSharedCommand>
{
    public override bool Process(SetVariableIsSharedCommand command)
    {
        command.Variable.IsShared = command.NewValue;
        DispatcherContext.Root.SendEvent(VariableRenamedEvent.GetPooled(DispatcherContext.Root, command.Variable));
        return true;
    }
}
