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
        
        // Initialize enum values properly
        if (variable.Type.IsEnum && !variable.Type.IsDefined(typeof(FlagsAttribute), false))
        {
            // Get the first value of the enum (not necessarily 0)
            Array enumValues = Enum.GetValues(variable.Type);
            if (enumValues.Length > 0)
            {
                variable.ObjectValue = enumValues.GetValue(0);
            }
        }
        
        DispatcherContext.BlackboardAsset.Variables.Add(variable);
        BlackboardAsset.InvokeBlackboardChanged(BlackboardAsset.BlackboardChangedType.VariableAdded);
        BlackboardView.FocusOnVariableNameField(variable);
    }
}
