using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Set Variable Value",
        description: "Sets the value of a given variable.",
        story: "Set [Variable] value to [Value]",
        category: "Action/Blackboard",
        id: "3cf856343a2c414c2cfd1083d30c24fa")]
    internal partial class SetVariableValueAction : Action
    {
        [SerializeReference] public BlackboardVariable Variable;
        [SerializeReference] public BlackboardVariable Value;

#if BEHAVIOR_TEST_PRIMITIVE_HOTPATH
        public static bool s_HasUsedPrimitiveHotpath { get; private set; } = false;
#endif

        protected override Status OnStart()
        {
            if (Variable == null || Value == null)
            {
                return Status.Failure;
            }

#if BEHAVIOR_TEST_PRIMITIVE_HOTPATH
            s_HasUsedPrimitiveHotpath = true;
#endif
            // Hotpath for primitives to avoid boxing
            if (Variable is BlackboardVariable<int> varInt && Value is BlackboardVariable<int> valInt)
            {
                varInt.Value = valInt.Value;
            }
            else if (Variable is BlackboardVariable<float> varFloat && Value is BlackboardVariable<float> valFloat)
            {
                varFloat.Value = valFloat.Value;
            }
            else if (Variable is BlackboardVariable<double> varDouble && Value is BlackboardVariable<double> valDouble)
            {
                varDouble.Value = valDouble.Value;
            }
            else if (Variable is BlackboardVariable<string> varString && Value is BlackboardVariable<string> valString)
            {
                varString.Value = valString.Value;
            }
            else if (Variable is BlackboardVariable<bool> varBool && Value is BlackboardVariable<bool> valBool)
            {
                varBool.Value = valBool.Value;
            }
            else
            {
#if BEHAVIOR_TEST_PRIMITIVE_HOTPATH
                s_HasUsedPrimitiveHotpath = false;
#endif
                Variable.ObjectValue = Value.ObjectValue;
            }

            return Status.Success;
        }
    }
}
