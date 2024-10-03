using System;
using Unity.Behavior.GraphFramework;

internal class CreateVariableCommandHandler : CommandHandler<CreateVariableCommand>
{
    public override bool Process(CreateVariableCommand command)
    {
        CreateBlackboardVariable(command.VariableType, command.Name, command.Args);
        return true;
    }
    
    private void CreateBlackboardVariable(Type type, string name, params object[] args)
    {
        VariableModel variable = Activator.CreateInstance(type, args) as VariableModel;
        variable.Name = BlackboardUtils.GetNewVariableName(name, BlackboardAsset);
        DispatcherContext.BlackboardAsset.Variables.Add(variable);
        BlackboardAsset.InvokeBlackboardChanged();
        BlackboardView.FocusOnVariableNameField(variable);
    }
}