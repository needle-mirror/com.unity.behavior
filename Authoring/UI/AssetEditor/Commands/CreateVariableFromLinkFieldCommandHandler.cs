using System;
using System.Collections.Generic;
using Unity.Behavior.GraphFramework;

namespace Unity.Behavior
{
    internal class CreateVariableFromLinkFieldCommandHandler : CommandHandler<CreateVariableFromLinkFieldCommand>
    {
        public override bool Process(CreateVariableFromLinkFieldCommand command)
        {
            VariableModel variable = Activator.CreateInstance(command.VariableType, command.Args) as VariableModel;
            variable.Name = BlackboardUtils.GetNewVariableName(command.Name, BlackboardAsset);
            DispatcherContext.BlackboardAsset.Variables.Add(variable);
            BlackboardView.FocusOnVariableNameField(variable);

            BaseLinkField field = command.Field;
            field.LinkedVariable = variable;
            using (LinkFieldTypeChangeEvent changeEvent = LinkFieldTypeChangeEvent.GetPooled(field, variable.Type))
            {
                field.SendEvent(changeEvent);
            }
            
            Dictionary<string, VariableModel> recentlyLinkedVariables = (DispatcherContext as BehaviorGraphEditor)?.m_RecentlyLinkedVariables;
            if (recentlyLinkedVariables != null)
            {
                recentlyLinkedVariables[field.FieldName] = variable;
            }
            
            return true;
        }
    }
}