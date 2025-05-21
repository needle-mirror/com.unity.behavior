using Unity.Behavior.GraphFramework;

internal class SetVariableIsSharedCommandHandler : CommandHandler<SetVariableIsSharedCommand>
{
    public override bool Process(SetVariableIsSharedCommand command)
    {
        command.Variable.IsShared = command.NewValue;
        DispatcherContext.Root.SendEvent(VariableRenamedEvent.GetPooled(DispatcherContext.Root, command.Variable));
        BlackboardAsset.InvokeBlackboardChanged(BlackboardAsset.BlackboardChangedType.VariableSetGlobal);
        return true;
    }
}
