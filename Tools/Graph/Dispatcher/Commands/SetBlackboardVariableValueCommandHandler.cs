using System;
using System.Reflection;

namespace Unity.Behavior.GraphFramework
{
    internal class SetBlackboardVariableValueCommandHandler : CommandHandler<SetBlackboardVariableValueCommand>
    {
        public override bool Process(SetBlackboardVariableValueCommand command)
        {
            Asset?.MarkUndo("Set blackboard variable value");
            BlackboardAsset?.MarkUndo("Set blackboard asset variable value");
            command.Variable.ObjectValue = command.Value;
            
            Type variableType = command.Variable.GetType();
            if (variableType.IsGenericType)
            {
                Type[] genericTypeArgs = variableType.GenericTypeArguments;

                if (genericTypeArgs.Length == 1 && genericTypeArgs[0].IsEnum)
                {
                    FieldInfo[] fields = genericTypeArgs[0].GetFields(BindingFlags.Public | BindingFlags.Static);

                    command.Variable.ObjectValue = fields[(int)command.Value].GetValue(null);
                }
            }
            
            // Have we processed the command and wish to block further processing?
            return true;
        }
    }
}