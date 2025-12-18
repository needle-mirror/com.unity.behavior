using System;
using System.Linq;
using Unity.Behavior.GraphFramework;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace Unity.Behavior
{
    internal class CreateVariableFromSerializedTypeCommandHandler : CommandHandler<CreateVariableFromSerializedTypeCommand>
    {
        public override bool Process(CreateVariableFromSerializedTypeCommand command)
        {
#if UNITY_6000_5_OR_NEWER
            Type type = CurrentAssemblies.GetLoadedAssemblies()
#else
            Type type = AppDomain.CurrentDomain.GetAssemblies()
#endif
            .SelectMany(x => x.GetTypes())
            // Note: This command is currently only called for newly generated Enums and EventChannels, which means the type should definitely either have EventChannelBase or BlackboardEnumAttribute.
            // If this is to change, we'll need a different check here.
            .FirstOrDefault(t => t.Name == command.VariableTypeName && (typeof(EventChannelBase).IsAssignableFrom(t) || t.IsDefined(typeof(BlackboardEnumAttribute), false)));
            if (type == null)
            {
                Debug.LogError($"Type {command.VariableTypeName} not found");
                return false;
            }

            BlackboardView.Dispatcher.DispatchImmediate(new CreateVariableCommand($"{command.VariableTypeName}", BlackboardUtils.GetVariableModelTypeForType(type)),
                setHasOutstandingChanges: false);
            return true;
        }
    }
}
