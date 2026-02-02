using System;
using System.Collections.Generic;

namespace Unity.Behavior
{
    /// <summary>
    /// A utility class for conditions used in Conditional nodes.
    /// </summary>
    public static class ConditionUtils
    {
#if BEHAVIOR_TEST_PRIMITIVE_HOTPATH
        public static bool s_HasUsedPrimitiveHotpath { get; private set; } = false;
#endif

        /// <summary>
        /// Evaluates the operation from a condition.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name="CompareType">The type of value being compared. This must be unmanaged IComparable.</typeparam>
        /// <param name="leftOperand">The left side value that is being compared.</param>
        /// <param name="conditionOperatorVariable">The condition operator.</param>
        /// <param name="rightOperand">The right side value that is being compared.</param>
        public static bool Evaluate<CompareType>(CompareType leftOperand,
            BlackboardVariable<ConditionOperator> conditionOperatorVariable,
            CompareType rightOperand) where CompareType : unmanaged, IComparable
        {
            return CompareValues(leftOperand, rightOperand, conditionOperatorVariable);
        }

        /// <summary>
        /// Evaluates the operation from a condition.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        /// <param name="leftOperand">The left side value that is being compared.</param>
        /// <param name="conditionOperatorVariable">The condition operator.</param>
        /// <param name="rightOperand">The right side value that is being compared.</param>
        public static bool Evaluate(object leftOperand,
            BlackboardVariable<ConditionOperator> conditionOperatorVariable,
            object rightOperand)
        {
            if (leftOperand == null || rightOperand == null)
            {
                return CompareReferences(leftOperand, rightOperand, conditionOperatorVariable.Value);
            }

            ConditionOperator conditionOperator = conditionOperatorVariable.Value;

            // If both operands are Blackboard variables.
            if (leftOperand is BlackboardVariable variable && rightOperand is BlackboardVariable comparisonValue)
            {
                return CompareBlackboardVariables(variable, comparisonValue, conditionOperator);
            }

            // If left operand is a Blackboard variable but the right operand is not.
            if (leftOperand is BlackboardVariable leftVar)
            {
                return CompareBlackboardWithValue(leftVar, rightOperand, conditionOperator);
            }

            // If right operand is a Blackboard variable but the left operand is not.
            if (rightOperand is BlackboardVariable rightVar)
            {
                return CompareValueWithBlackboard(leftOperand, rightVar, conditionOperator);
            }

            // If neither of comparison values are Blackboard variables.
            if (leftOperand is IComparable && rightOperand is IComparable)
            {
                return CompareValues((IComparable)leftOperand, (IComparable)rightOperand,
                    conditionOperator);
            }

            return CompareReferences(leftOperand, rightOperand, conditionOperator);
        }

        private static bool CompareReferences(object left, object right, ConditionOperator conditionOperator)
        {
            return conditionOperator switch
            {
                ConditionOperator.Equal => left == right,
                ConditionOperator.NotEqual => left != right,
                _ => false
            };
        }

        private static bool CompareBlackboardVariables(BlackboardVariable left, BlackboardVariable right, ConditionOperator conditionOperator)
        {
            // Try primitive hotpaths first to avoid boxing
            if (TryComparePrimitives(left, right, conditionOperator, out bool result))
            {
                return result;
            }

            return CompareObjects(left.ObjectValue, right.ObjectValue, conditionOperator);
        }

        private static bool CompareBlackboardWithValue(BlackboardVariable left, object right, ConditionOperator conditionOperator)
        {
            // Try primitive hotpaths first to avoid boxing
            if (right is IComparable && TryComparePrimitives(left, (IComparable)right, conditionOperator, out bool result))
            {
                return result;
            }

            return CompareObjects(left.ObjectValue, right, conditionOperator);
        }

        private static bool CompareValueWithBlackboard(object left, BlackboardVariable right, ConditionOperator conditionOperator)
        {
            // Try primitive hotpaths first to avoid boxing
            if (left is IComparable && TryComparePrimitives((IComparable)left, right, conditionOperator, out bool result))
            {
                return result;
            }

            return CompareObjects(left, right.ObjectValue, conditionOperator);
        }

        private static bool CompareObjects(object left, object right, ConditionOperator conditionOperator)
        {
            // Handle nulls first
            if (left == null || right == null)
            {
                return conditionOperator switch
                {
                    ConditionOperator.Equal => left == right,
                    ConditionOperator.NotEqual => left != right,
                    _ => false
                };
            }

            // Try IComparable for ordering operations.
            if (left is IComparable && right is IComparable)
            {
                return CompareValues((IComparable)left, (IComparable)right, conditionOperator);
            }

            // Try value type equality (for structs like Vector3 that implement IEquatable but not IComparable)
            if (left.GetType().IsValueType)
            {
                bool areEqual = left.Equals(right);
                return conditionOperator switch
                {
                    ConditionOperator.Equal => areEqual,
                    ConditionOperator.NotEqual => !areEqual,
                    _ => false
                };
            }

            // Fall back to reference comparison
            return CompareReferences(left, right, conditionOperator);
        }

        // Hotpath method to compare primitive types without boxing
        private static bool TryComparePrimitives(BlackboardVariable left, BlackboardVariable right, ConditionOperator conditionOperator, out bool result)
        {
#if BEHAVIOR_TEST_PRIMITIVE_HOTPATH
            s_HasUsedPrimitiveHotpath = true;
#endif
            // Try int
            if (left is BlackboardVariable<int> leftInt && right is BlackboardVariable<int> rightInt)
            {
                result = CompareValues(leftInt.Value, rightInt.Value, conditionOperator);
                return true;
            }

            // Try float
            if (left is BlackboardVariable<float> leftFloat && right is BlackboardVariable<float> rightFloat)
            {
                result = CompareValues(leftFloat.Value, rightFloat.Value, conditionOperator);
                return true;
            }

            // Try bool
            if (left is BlackboardVariable<bool> leftBool && right is BlackboardVariable<bool> rightBool)
            {
                result = CompareValues(leftBool.Value, rightBool.Value, conditionOperator);
                return true;
            }

            // Try double
            if (left is BlackboardVariable<double> leftDouble && right is BlackboardVariable<double> rightDouble)
            {
                result = CompareValues(leftDouble.Value, rightDouble.Value, conditionOperator);
                return true;
            }

            // Try string
            if (left is BlackboardVariable<string> leftString && right is BlackboardVariable<string> rightString)
            {
                result = CompareValues(leftString.Value, rightString.Value, conditionOperator);
                return true;
            }

#if BEHAVIOR_TEST_PRIMITIVE_HOTPATH
            s_HasUsedPrimitiveHotpath = false;
#endif
            result = false;
            return false;
        }

        // Hotpath method for when left is BlackboardVariable and right is IComparable
        private static bool TryComparePrimitives(BlackboardVariable left, IComparable right, ConditionOperator conditionOperator, out bool result)
        {
            // Try int
            if (left is BlackboardVariable<int> leftInt && right is int rightInt)
            {
                result = CompareValues(leftInt.Value, rightInt, conditionOperator);
                return true;
            }

            // Try float
            if (left is BlackboardVariable<float> leftFloat && right is float rightFloat)
            {
                result = CompareValues(leftFloat.Value, rightFloat, conditionOperator);
                return true;
            }

            // Try bool
            if (left is BlackboardVariable<bool> leftBool && right is bool rightBool)
            {
                result = CompareValues(leftBool.Value, rightBool, conditionOperator);
                return true;
            }

            // Try double
            if (left is BlackboardVariable<double> leftDouble && right is double rightDouble)
            {
                result = CompareValues(leftDouble.Value, rightDouble, conditionOperator);
                return true;
            }

            // Try string
            if (left is BlackboardVariable<string> leftString && right is string rightString)
            {
                result = CompareValues(leftString.Value, rightString, conditionOperator);
                return true;
            }

            result = false;
            return false;
        }

        // Hotpath method for when left is IComparable and right is BlackboardVariable
        private static bool TryComparePrimitives(IComparable left, BlackboardVariable right, ConditionOperator conditionOperator, out bool result)
        {
            // Try int
            if (left is int leftInt && right is BlackboardVariable<int> rightInt)
            {
                result = CompareValues(leftInt, rightInt.Value, conditionOperator);
                return true;
            }

            // Try float
            if (left is float leftFloat && right is BlackboardVariable<float> rightFloat)
            {
                result = CompareValues(leftFloat, rightFloat.Value, conditionOperator);
                return true;
            }

            // Try bool
            if (left is bool leftBool && right is BlackboardVariable<bool> rightBool)
            {
                result = CompareValues(leftBool, rightBool.Value, conditionOperator);
                return true;
            }

            // Try double
            if (left is double leftDouble && right is BlackboardVariable<double> rightDouble)
            {
                result = CompareValues(leftDouble, rightDouble.Value, conditionOperator);
                return true;
            }

            // Try string
            if (left is string leftString && right is BlackboardVariable<string> rightString)
            {
                result = CompareValues(leftString, rightString.Value, conditionOperator);
                return true;
            }

            result = false;
            return false;
        }

        private static bool CompareValues(IComparable left, IComparable right, ConditionOperator conditionOperator)
        {
            int comparison = left.CompareTo(right);
            return conditionOperator switch
            {
                ConditionOperator.Equal => comparison == 0,
                ConditionOperator.NotEqual => comparison != 0,
                ConditionOperator.Greater => comparison > 0,
                ConditionOperator.Lower => comparison < 0,
                ConditionOperator.GreaterOrEqual => comparison >= 0,
                ConditionOperator.LowerOrEqual => comparison <= 0,
                _ => false
            };
        }

        // Generic overload for primitive types to avoid IComparable boxing
        private static bool CompareValues<T>(T left, T right, ConditionOperator conditionOperator) where T : IComparable<T>
        {
            int comparison = left.CompareTo(right);
            return conditionOperator switch
            {
                ConditionOperator.Equal => comparison == 0,
                ConditionOperator.NotEqual => comparison != 0,
                ConditionOperator.Greater => comparison > 0,
                ConditionOperator.Lower => comparison < 0,
                ConditionOperator.GreaterOrEqual => comparison >= 0,
                ConditionOperator.LowerOrEqual => comparison <= 0,
                _ => false
            };
        }

        internal static bool CheckConditions(List<Condition> conditions, bool allRequired)
        {
            if (conditions.Count == 0) return false;
            if (!allRequired)
            {
                foreach (Condition condition in conditions)
                {
                    if (condition.IsTrue())
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (Condition condition in conditions)
            {
                if (!condition.IsTrue())
                    return false;
            }

            return true;
        }
    }
}
