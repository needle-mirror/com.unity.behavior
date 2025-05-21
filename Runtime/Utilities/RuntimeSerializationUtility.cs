using System.Collections.Generic;
using Unity.Behavior.GraphFramework;
using Unity.Behavior.Serialization.Json;
using UnityEngine;

namespace Unity.Behavior
{
    /// <summary>
    /// Contains all interfaces and helper objects for runtime serialization of behavior graphs.
    /// </summary>
    public class RuntimeSerializationUtility
    {
        /// <summary>
        /// Interface for all object resolvers. During runtime serialization, an object resolver is required to convert
        /// an object to and from a unique ID.
        /// </summary>
        /// <typeparam name="TSerializedFormat">Serialized output type.</typeparam>
        public interface IUnityObjectResolver<TSerializedFormat>
        {
            /// <summary>
            /// Converts an object to a serializable id.
            /// </summary>
            /// <param name="obj">Object to convert.</param>
            /// <returns>Serializable ID.</returns>
            TSerializedFormat Map(UnityEngine.Object obj);

            /// <summary>
            /// Used to convert a serializable ID to an object.
            /// </summary>
            /// <param name="mappedValue">Serializable ID.</param>
            /// <typeparam name="TSerializedType">Serialized ID type.</typeparam>
            /// <returns>The object created for the serialized ID.</returns>
            TSerializedType Resolve<TSerializedType>(TSerializedFormat mappedValue) where TSerializedType : UnityEngine.Object;
        }

        /// <summary>
        /// Interface for behavior serializor implementations.
        /// </summary>
        /// <typeparam name="TSerializedFormat">Serialized output type.</typeparam>
        public interface IBehaviorSerializer<TSerializedFormat>
        {
            /// <summary>
            /// Serializes a BehaviorGraph into.
            /// </summary>
            /// <param name="graph">The Graph to serialize.</param>
            /// <param name="resolver">The object resolver implementation to use.</param>
            /// <returns>The serialized output</returns>
            TSerializedFormat Serialize(BehaviorGraph graph, IUnityObjectResolver<string> resolver);

            /// <summary>
            /// Deserializes a BehaviorGraph on to a graph object.
            /// </summary>
            /// <param name="serialized">Serialized data to be deserialized.</param>
            /// <param name="graph">BehaviorGraph to be updated.</param>
            /// <param name="resolver">The object resolver implementation to use.</param>
            void Deserialize(TSerializedFormat serialized, BehaviorGraph graph, IUnityObjectResolver<string> resolver);
        }

        private class NodeJsonAdapter : IJsonAdapter<Node>
        {
            private const string GuidKey = "ID";
            private const string IsRunningKey = "IsRunning";
            private const string ChildrenKey = "Children";
            private const string ChildKey = "Child";
            private const string ParentsKey = "Parents";
            private const string ParentKey = "Parent";

            public void Serialize(in JsonSerializationContext<Node> context, Node node)
            {
                if (node == null)
                {
                    context.Writer.WriteNull();
                    return;
                }
                using (context.Writer.WriteObjectScope())
                {
                    if (context.SerializeCurrentAsReference())
                    {
                        return;
                    }

                    // To do: enable the code below and get rid of BehaviorGraphModule.Serialize
                    //if (node.IsRunning)
                    //{
                    //    try
                    //    {
                    //        node.Serialize();
                    //    }
                    //    catch (System.Exception e)
                    //    {
                    //        Debug.LogError($"Failed to serialize {node.GetType()} (ID: {node.ID}): {e.Message}");
                    //    }
                    //}

                    context.SerializeValue(GuidKey, node.ID);
                    context.SerializeValue(IsRunningKey, node.IsRunning);
                    if (node is Composite composite)
                    {
                        if (composite.m_Parent != null)
                        {
                            context.SerializeValue(ParentKey, composite.m_Parent);
                        }

                        if (composite.m_Children != null && composite.m_Children.Count > 0)
                        {
                            using var _ = context.Writer.WriteArrayScope(ChildrenKey);
                            foreach (var cChild in composite.m_Children)
                            {
                                context.SerializeValue(cChild);
                            }
                        }

                        if (node is BranchingConditionComposite branchingConditionComposite)
                        {
                            if (branchingConditionComposite.True != null)
                            {
                                context.SerializeValue("True", branchingConditionComposite.True);
                            }

                            if (branchingConditionComposite.False != null)
                            {
                                context.SerializeValue("False", branchingConditionComposite.False);
                            }

                            if (branchingConditionComposite.CurrentChild != null)
                            {
                                context.SerializeValue("CurrentChild", branchingConditionComposite.CurrentChild);
                            }
                        }
                    }
                    else if (node is Action action)
                    {
                        if (action.m_Parent != null)
                        {
                            context.SerializeValue(ParentKey, action.m_Parent);
                        }
                    }
                    else if (node is Modifier modifier)
                    {
                        if (modifier.m_Parent != null)
                        {
                            context.SerializeValue(ParentKey, modifier.m_Parent);
                        }
                        if (modifier.m_Child != null)
                        {
                            context.SerializeValue(ChildKey, modifier.m_Child);
                        }
                    }
                    else if (node is Join join)
                    {
                        if (join.m_Parents != null && join.m_Parents.Count > 0)
                        {
                            using var _ = context.Writer.WriteArrayScope(ParentsKey);
                            foreach (var parent in join.m_Parents)
                            {
                                context.SerializeValue(parent);
                            }
                        }

                        if (join.m_Child != null)
                        {
                            context.SerializeValue(ChildKey, join.m_Child);
                        }
                    }

                    if (node is IConditional conditional)
                    {
                        using var _ = context.Writer.WriteArrayScope("Conditions");
                        foreach (var condition in conditional.Conditions)
                        {
                            if (node.IsRunning)
                            {
                                if (condition is IConditionSerializationCallbackReceiver receiver)
                                {
                                    receiver.OnSerialize();
                                }
                            }

                            context.SerializeValue(condition);
                        }
                    }

                    // the skipBeginEndObject is here to avoid another {} pair
                    context.ContinueVisitation(true);
                }
            }

            public Node Deserialize(in JsonDeserializationContext<Node> context)
            {
                if (context.SerializedValue.IsNull())
                {
                    return null;
                }

                Node node = context.ContinueVisitation();
                if (context.SerializedValue.TryGetValue(GuidKey, out SerializedValueView guidSerializedView))
                {
                    var guid = guidSerializedView.AsStringView().ToString();
                    node.ID = new SerializableGUID(guid);
                }

                if (context.SerializedValue.TryGetValue(IsRunningKey, out SerializedValueView isRunningSerializedView))
                {
                    var isRunning = isRunningSerializedView.AsBoolean();
                    node.IsRunning = isRunning;
                }

                if (node is Composite composite)
                {
                    if (context.SerializedValue.TryGetValue(ParentKey, out SerializedValueView parentSerializedView))
                    {
                        var parentNode = context.DeserializeValue<Node>(parentSerializedView);
                        composite.m_Parent = parentNode;
                    }

                    if (context.SerializedValue.TryGetValue(ChildrenKey, out SerializedValueView childrenView))
                    {
                        var childrenNodes = context.DeserializeValue<List<Node>>(childrenView);
                        composite.m_Children = childrenNodes;
                    }

                    if (node is BranchingConditionComposite branchingConditionComposite)
                    {
                        if (context.SerializedValue.TryGetValue("True",
                                out SerializedValueView trueSerializedValueView))
                        {
                            var trueNode = context.DeserializeValue<Node>(trueSerializedValueView);
                            branchingConditionComposite.True = trueNode;
                        }

                        if (context.SerializedValue.TryGetValue("False",
                                out SerializedValueView falseSerializedValueView))
                        {
                            var falseNode = context.DeserializeValue<Node>(falseSerializedValueView);
                            branchingConditionComposite.False = falseNode;
                        }

                        if (context.SerializedValue.TryGetValue("CurrentChild",
                                out SerializedValueView childSerializedValueView))
                        {
                            var childNode = context.DeserializeValue<Node>(childSerializedValueView);
                            branchingConditionComposite.CurrentChild = childNode;
                        }
                    }
                }
                else if (node is Action action)
                {
                    if (context.SerializedValue.TryGetValue(ParentKey, out SerializedValueView parentSerializedView))
                    {
                        var parentNode = context.DeserializeValue<Node>(parentSerializedView);
                        action.m_Parent = parentNode;
                    }
                }
                else if (node is Modifier modifier)
                {
                    if (context.SerializedValue.TryGetValue(ParentKey, out SerializedValueView parentSerializedView))
                    {
                        var parentNode = context.DeserializeValue<Node>(parentSerializedView);
                        modifier.m_Parent = parentNode;
                    }

                    if (context.SerializedValue.TryGetValue(ChildKey, out SerializedValueView childSerializedView))
                    {
                        var childNode = context.DeserializeValue<Node>(childSerializedView);
                        modifier.m_Child = childNode;
                    }
                }
                else if (node is Join join)
                {
                    if (context.SerializedValue.TryGetValue(ParentsKey, out SerializedValueView parentsView))
                    {
                        var parentNodes = context.DeserializeValue<List<Node>>(parentsView);
                        join.m_Parents = parentNodes;
                    }

                    if (context.SerializedValue.TryGetValue(ChildKey, out SerializedValueView childSerializedView))
                    {
                        var childNode = context.DeserializeValue<Node>(childSerializedView);
                        join.m_Child = childNode;
                    }
                }

                // To do: enable the code below and get rid of BehaviorGraphModule.Deserialize
                //if (node.IsRunning)
                //{
                //    try
                //    {
                //        node.Deserialize();
                //    }
                //    catch (System.Exception e)
                //    {
                //        Debug.LogError($"Failed to deserialize {node.GetType()} (ID: {node.ID}): {e.Message}");
                //    }
                //}

                if (node is IConditional conditional)
                {
                    if (context.SerializedValue.TryGetValue("Conditions", out SerializedValueView conditionView))
                    {
                        var conditions = context.DeserializeValue<List<Condition>>(conditionView);
                        conditional.Conditions = conditions;

                        if (node.IsRunning)
                        {
                            foreach (var condition in conditions)
                            {
                                if (condition is IConditionSerializationCallbackReceiver receiver)
                                {
                                    receiver.OnDeserialize();
                                }
                            }
                        }
                    }
                }

                return node;
            }
        }

        /// <summary>
        /// Implementation for JSON serializer.
        /// </summary>
        public class JsonBehaviorSerializer : IBehaviorSerializer<string>
        {
            private static UnityMonoBehaviourAdapter s_UnityMonoBehaviourAdapter;
            private static GameObjectAdapter s_GameObjectAdapter;
            private static ComponentAdapter s_ComponentAdapter;

            private static JsonSerializationParameters s_JsonPackageSerializationParameters =
                new JsonSerializationParameters
                {
                    Indent = 4,
                    DisableRootAdapters = true,
                    UserDefinedAdapters = new List<IJsonAdapter>
                    {
                        (s_UnityMonoBehaviourAdapter = new UnityMonoBehaviourAdapter()),
                        new NodeJsonAdapter(),
                        (s_GameObjectAdapter = new GameObjectAdapter()),
                        (s_ComponentAdapter = new ComponentAdapter()),
                        new SerializableGUIDAdapter(),
                    }
                };

            private class SerializableGUIDAdapter : IJsonAdapter<SerializableGUID>
            {
                public void Serialize(in JsonSerializationContext<SerializableGUID> context, SerializableGUID value) =>
                    context.SerializeValue(value.Valid ? value.ToString() : string.Empty);

                public SerializableGUID Deserialize(in JsonDeserializationContext<SerializableGUID> context) =>
                    new(context.SerializedValue.AsStringView().ToString());
            }

            private class ComponentAdapter : IJsonAdapter<Component>
            {
                public IUnityObjectResolver<string> Resolver;

                public void Serialize(in JsonSerializationContext<Component> context, Component value)
                {
                    context.SerializeValue(Resolver.Map(value));
                }

                public Component Deserialize(in JsonDeserializationContext<Component> context)
                {
                    var name = context.DeserializeValue<string>(context.SerializedValue);
                    return name == null ? null : Resolver.Resolve<Component>(name);
                }
            }

            private class GameObjectAdapter : IJsonAdapter<GameObject>
            {
                public IUnityObjectResolver<string> Resolver;

                public void Serialize(in JsonSerializationContext<GameObject> context, GameObject value)
                {
                    context.SerializeValue(Resolver.Map(value));
                }

                public GameObject Deserialize(in JsonDeserializationContext<GameObject> context)
                {
                    var name = context.DeserializeValue<string>(context.SerializedValue);
                    return name == null ? null : Resolver.Resolve<GameObject>(name);
                }
            }

            private class UnityMonoBehaviourAdapter : IContravariantJsonAdapter<MonoBehaviour>
            {
                public IUnityObjectResolver<string> Resolver;

                public void Serialize(IJsonSerializationContext context, MonoBehaviour value)
                {
                    if (value == null)
                    {
                        context.Writer.WriteNull();
                        return;
                    }

                    using (context.Writer.WriteObjectScope())
                    {
                        SerializableType type = value.GetType();
                        context.SerializeValue("type", type.ToString());
                        context.SerializeValue("value", Resolver.Map(value));
                    }
                }

                public object Deserialize(IJsonDeserializationContext context)
                {
                    if (context.SerializedValue.IsNull())
                    {
                        return null;
                    }

                    var type = context.SerializedValue.GetValue("type").AsStringView().ToString();
                    var value = context.SerializedValue.GetValue("value").AsStringView().ToString();

                    var serializableType = new SerializableType(type);
                    if (serializableType.Type == null)
                    {
                        return null;
                    }

                    if (value == null)
                    {
                        return null;
                    }

                    var resolverInvoke = typeof(IUnityObjectResolver<string>).GetMethod("Resolve").MakeGenericMethod(serializableType.Type).Invoke(Resolver, new object[]
                    {
                        value
                    });

                    return resolverInvoke;
                }
            }

            /// <summary>
            /// Serializes a BehaviorGraph into JSON.
            /// </summary>
            /// <param name="graph">The Graph to serialize.</param>
            /// <param name="resolver">The object resolver implementation to use.</param>
            /// <returns>The serialized output</returns>
            public string Serialize(BehaviorGraph graph, IUnityObjectResolver<string> resolver)
            {
                s_UnityMonoBehaviourAdapter.Resolver = resolver;
                s_GameObjectAdapter.Resolver = resolver;
                s_ComponentAdapter.Resolver = resolver;
                return JsonSerialization.ToJson(graph, s_JsonPackageSerializationParameters);
            }

            /// <summary>
            /// Deserializes JSON to a BehaviorGraph.
            /// </summary>
            /// <param name="graphJson">Serialized data to be deserialized.</param>
            /// <param name="graph">BehaviorGraph to be updated.</param>
            /// <param name="resolver">The object resolver implementation to use.</param>
            public void Deserialize(string graphJson, BehaviorGraph graph, IUnityObjectResolver<string> resolver)
            {
                s_UnityMonoBehaviourAdapter.Resolver = resolver;
                s_GameObjectAdapter.Resolver = resolver;
                s_ComponentAdapter.Resolver = resolver;
                JsonSerialization.FromJsonOverride(graphJson, ref graph, s_JsonPackageSerializationParameters);
            }
        }
    }
}