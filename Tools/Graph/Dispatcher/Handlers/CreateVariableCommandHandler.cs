using System;
using Unity.Behavior.GraphFramework;

internal class CreateVariableCommandHandler : CommandHandler<CreateVariableCommand>
{
    public override bool Process(CreateVariableCommand command)
    {
        CreateBlackboardVariable(command.VariableType, command.Name, command.ExactName, command.Args);
        return true;
    }
    
    private void CreateBlackboardVariable(Type type, string name, bool exactName, params object[] args)
    {
        VariableModel variable = Activator.CreateInstance(type, args) as VariableModel;
        variable.Name = exactName ? name : BlackboardUtils.GetNewVariableName(name, BlackboardAsset);
        DispatcherContext.BlackboardAsset.Variables.Add(variable);
        BlackboardAsset.InvokeBlackboardChanged();
        BlackboardView.FocusOnVariableNameField(variable);
    }
}