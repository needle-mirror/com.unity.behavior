using System;
using System.Reflection;

namespace Unity.Behavior.GraphFramework
{
    internal class SetBlackboardVariableValueCommandHandler : CommandHandler<SetBlackboardVariableValueCommand>
    {
        public override bool Process(SetBlackboardVariableValueCommand command)
        {
            Type variableType = command.Variable.GetType();
            if (variableType.IsGenericType && variableType.GenericTypeArguments.Length == 1 
                && variableType.GenericTypeArguments[0].IsEnum)
            {
                command.Variable.ObjectValue = Enum.ToObject(variableType.GenericTypeArguments[0], command.Value);
            }
            else
            {
                command.Variable.ObjectValue = command.Value;
            }

            BlackboardAsset?.InvokeBlackboardChanged(BlackboardAsset.BlackboardChangedType.VariableValueChanged);
            // Have we processed the command and wish to block further processing?
            return true;
        }
    }
}