﻿using System;
using System.Collections.Generic;
using System.Text;
using Unity.Properties;

namespace Unity.Behavior.Serialization.Json
{
    internal class DictionaryElementProperty<TDictionary, TKey, TValue> : Property<TDictionary, TValue>
        where TDictionary : IDictionary<TKey, TValue>
    {
        public override string Name => Key.ToString();
        public override bool IsReadOnly => false;

        public override TValue GetValue(ref TDictionary container)
        {
            return container[Key];
        }

        public override void SetValue(ref TDictionary container, TValue value)
        {
            container[Key] = value;
        }

        public TKey Key { get; internal set; }
    }

    /// <summary>
    /// A visitor that traverses a property container and outputs a JSON string.
    /// </summary>
    internal class JsonPropertyWriter : JsonPropertyVisitor,
        IPropertyBagVisitor,
        ICollectionPropertyBagVisitor,
        IListPropertyBagVisitor,
        IDictionaryPropertyBagVisitor,
        IPropertyVisitor
    {
        struct SerializedId
        {
            public int Id;
        }

        struct SerializedType
        {
            public Type Type;
        }

        struct SerializedVersion
        {
            public int Version;
        }

        class SerializedIdProperty : Property<SerializedId, int>
        {
            public override string Name => k_SerializedId;
            public override bool IsReadOnly => true;

            public override int GetValue(ref SerializedId container) => container.Id;

            public override void SetValue(ref SerializedId container, int value) => throw new InvalidOperationException("Property is ReadOnly.");
        }

        class SerializedTypeProperty : Property<SerializedType, string>
        {
            public override string Name => k_SerializedTypeKey;
            public override bool IsReadOnly => true;

            public override string GetValue(ref SerializedType container) => FormatFullTypeName(container.Type);

            public override void SetValue(ref SerializedType container, string value) => throw new InvalidOperationException("Property is ReadOnly.");

            /// <summary>
            /// Builds a string containing the type's FullName but accounts for nested generic type argument.
            /// for a type T&lt;G&gt; where both types live in the namespace N of assembly A, will return N.T`1[[N.G, A]], A
            /// whereas dotnet methods would return:
            /// t.Name: N.T`1, lacks the generic parameters
            /// t.AssemblyQualifiedName: N.T`1[[N.G, A, Version =1.0.0.0, Culture=neutral, PublicKeyToken=hash]], A, Version =1.0.0.0, Culture=neutral, PublicKeyToken=hash, too verbose
            /// t.FullName: N.T`1[[N.G, A, Version =1.0.0.0, Culture=neutral, PublicKeyToken=hash]], the outer type has the right format, but the generic type arguments use the full assembly qualified name
            /// Note that nested types are another exception - a type T2 nested in a type T should appear as N.T+T2, A
            /// The assembly is skipped for primitives - no need to specify System.Boolean comes from mscorlib
            /// </summary>
            /// <param name="t">The type to format</param>
            /// <returns>An equivalent to FullName where generic argument types also use the full name representation</returns>
            string FormatFullTypeName(Type t)
            {
                StringBuilder sb = new();

                FormatFullTypeNameRecursive(t, sb, false);

                return sb.ToString();

                void FormatFullTypeNameRecursive(Type t, StringBuilder sb, bool omitAssembly)
                {
                    if (t.IsArray)
                    {
                        FormatFullTypeNameRecursive(t.GetElementType(), sb, true);
                        sb.Append("[]");
                    }
                    else if (t.IsNested)
                    {
                        // omit the assembly as we'll add it below - we want N.T+T2, A and not N.T, A+T2, A
                        FormatFullTypeNameRecursive(t.DeclaringType, sb, true);
                        sb.Append('+');
                        sb.Append(t.Name);
                    }
                    else
                    {
                        if (t.Namespace == null)
                            sb.Append(t.Name);
                        else
                            sb.Append(t.Namespace)
                                .Append(".")
                                .Append(t.Name);
                    }
                    if (t.IsGenericType)
                    {
                        // `1 is already in t.Name
                        sb.Append("[");
                        for (var index = 0; index < t.GenericTypeArguments.Length; index++)
                        {
                            if (index > 0)
                            {
                                sb.Append(", ");
                            }

                            sb.Append("[");
                            var typeArgument = t.GenericTypeArguments[index];
                            FormatFullTypeNameRecursive(typeArgument, sb, false);
                            sb.Append("]");
                        }
                        sb.Append("]");
                    }

                    if (!omitAssembly && !t.IsPrimitive && t.Assembly != typeof(int).Assembly)
                    {
                        sb.Append(", ");
                        sb.Append(t.Assembly.GetName().Name);
                    }
                }
            }
        }

        class SerializedVersionProperty : Property<SerializedVersion, int>
        {
            public override string Name => k_SerializedVersionKey;
            public override bool IsReadOnly => true;

            public override int GetValue(ref SerializedVersion container) => container.Version;

            public override void SetValue(ref SerializedVersion container, int value) => throw new InvalidOperationException("Property is ReadOnly.");
        }

        internal struct SerializedContainerMetadata
        {
            public bool IsSerializedReference;

            public bool HasSerializedId;
            public bool HasSerializedType;
            public bool HasSerializedVersion;

            /// <summary>
            /// Returns true if there is any metadata to write out.
            /// </summary>
            public bool Exists => HasSerializedId || HasSerializedType || HasSerializedVersion;

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public int SerializedId;

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public int SerializedVersion;
        }

        /// <summary>
        /// Shared property used to write the serialized type metadata.
        /// </summary>
        static readonly SerializedIdProperty s_SerializedIdProperty = new SerializedIdProperty();

        /// <summary>
        /// Shared property used to write the serialized type metadata.
        /// </summary>
        static readonly SerializedTypeProperty s_SerializedTypeProperty = new SerializedTypeProperty();

        /// <summary>
        /// Shared property used to write the serialized version metadata.
        /// </summary>
        static readonly SerializedVersionProperty s_SerializedVersionProperty = new SerializedVersionProperty();

        JsonWriter m_Writer;
        Type m_SerializedType;
        bool m_DisableRootAdapters;
        JsonAdapterCollection m_Adapters;
        JsonMigrationCollection m_Migrations;
        SerializedReferences m_SerializedReferences;

        bool m_HasPrimitiveOrStringGlobalAdapters;
        bool m_HasPrimitiveOrStringUserDefinedAdapters;
        internal bool SkipBeginEndObject;

        public JsonWriter Writer => m_Writer;

        public void SetWriter(JsonWriter writer)
            => m_Writer = writer;

        public void SetSerializedType(Type type)
            => m_SerializedType = type;

        public void SetDisableRootAdapters(bool disableRootAdapters)
            => m_DisableRootAdapters = disableRootAdapters;

        public void SetGlobalAdapters(List<IJsonAdapter> adapters)
        {
            m_Adapters.Global = adapters;
            m_HasPrimitiveOrStringGlobalAdapters = JsonAdapterCollection.ContainsPrimitiveOrStringAdapter(adapters);
        }

        public void SetUserDefinedAdapters(List<IJsonAdapter> adapters)
        {
            m_Adapters.UserDefined = adapters;
            m_HasPrimitiveOrStringUserDefinedAdapters = JsonAdapterCollection.ContainsPrimitiveOrStringAdapter(adapters);
        }

        public void SetGlobalMigrations(List<IJsonMigration> migrations)
            => m_Migrations.Global = migrations;

        public void SetUserDefinedMigration(List<IJsonMigration> migrations)
            => m_Migrations.UserDefined = migrations;

        public void SetSerializedReferences(SerializedReferences serializedReferences)
            => m_SerializedReferences = serializedReferences;

        public JsonPropertyWriter()
        {
            m_Adapters.InternalAdapter = new JsonAdapter();
        }

        /// <summary>
        /// We use a custom exposed method here so that keep the allocation free path for the common case.
        /// This also allows the packages tests to run successfully as normal.
        /// </summary>
        internal SerializedContainerMetadata GetSerializedContainerMetadataCustomWithAllocation<TContainer>(ref TContainer container)
        {
            var type = container?.GetType() ?? typeof(TContainer);

            // Never write metadata for special json types.
            if (type == typeof(JsonObject) || type == typeof(JsonArray)) return default;

            var metadata = default(SerializedContainerMetadata);

            if (!(TypeTraits<TContainer>.IsValueType || container.GetType().IsValueType))
            {
                var reference = container as object;

                if (m_SerializedReferences != null && m_SerializedReferences.TryGetSerializedReference(reference, out var id))
                {
                    if (!m_SerializedReferences.SetSerialized(reference))
                    {
                        return new SerializedContainerMetadata
                        {
                            IsSerializedReference = true,
                            SerializedId = id
                        };
                    }

                    metadata.HasSerializedId = true;
                    metadata.SerializedId = id;
                }
            }
            else
            {
                metadata.SerializedId = -1;
            }

            var isRootAndTypeWasGiven = Property is IPropertyWrapper && null != m_SerializedType;
            var declaredValueType = Property.DeclaredValueType();

            // We need to write out the serialize type name when any of the following cases are FALSE:
            // 1) The type is the same as the declared property type. This means deserialization can infer the property type.
            // 2) The root type was explicitly provided. This means the user is expected to provide a type upon deserialization.
            // 3) This is a nullable type. This means deserialization can infer the underlying property type.
            metadata.HasSerializedType = type != declaredValueType && !isRootAndTypeWasGiven && Nullable.GetUnderlyingType(declaredValueType) != type;
            metadata.HasSerializedVersion = m_Migrations.TryGetSerializedVersion<TContainer>(out var serializedVersion);
            metadata.SerializedVersion = serializedVersion;

            return metadata;
        }

        SerializedContainerMetadata GetSerializedContainerMetadata<TContainer>(ref TContainer container)
        {
            var type = typeof(TContainer);

            // Never write metadata for special json types.
            if (type == typeof(JsonObject) || type == typeof(JsonArray)) return default;

            var metadata = default(SerializedContainerMetadata);

            if (!(TypeTraits<TContainer>.IsValueType || container.GetType().IsValueType))
            {
                var reference = container as object;

                if (m_SerializedReferences != null && m_SerializedReferences.TryGetSerializedReference(reference, out var id))
                {
                    if (!m_SerializedReferences.SetSerialized(reference))
                    {
                        return new SerializedContainerMetadata
                        {
                            IsSerializedReference = true,
                            SerializedId = id
                        };
                    }

                    metadata.HasSerializedId = true;
                    metadata.SerializedId = id;
                }
            }
            else
            {
                metadata.SerializedId = -1;
            }

            var isRootAndTypeWasGiven = Property is IPropertyWrapper && null != m_SerializedType;
            var declaredValueType = Property.DeclaredValueType();

            // We need to write out the serialize type name in any of the following cases are FALSE:
            // 1) The type is the same as the declared property type. This means deserialization can infer the property type.
            // 2) The root type was explicitly provided. This means the user is expected to provide a type upon deserialization.
            // 3) This is a nullable type. This means deserialization can infer the underlying property type.
            metadata.HasSerializedType = type != declaredValueType && !isRootAndTypeWasGiven && Nullable.GetUnderlyingType(declaredValueType) != type;
            metadata.HasSerializedVersion = m_Migrations.TryGetSerializedVersion<TContainer>(out var serializedVersion);
            metadata.SerializedVersion = serializedVersion;

            return metadata;
        }

        internal void WriteSerializedContainerMetadata<TContainer>(ref TContainer container, SerializedContainerMetadata metadata, ref int count)
        {
            if (metadata.HasSerializedId)
            {
                using (CreatePropertyScope(s_SerializedIdProperty))
                {
                    var serializedId = new SerializedId { Id = metadata.SerializedId };
                    ((IPropertyAccept<SerializedId>)s_SerializedIdProperty).Accept(this, ref serializedId);
                }
            }

            if (metadata.HasSerializedType)
            {
                using (CreatePropertyScope(s_SerializedTypeProperty))
                {
                    var typeInfo = new SerializedType { Type = container.GetType() };
                    ((IPropertyAccept<SerializedType>)s_SerializedTypeProperty).Accept(this, ref typeInfo);
                }
            }

            if (metadata.HasSerializedVersion)
            {
                using (CreatePropertyScope(s_SerializedVersionProperty))
                {
                    var serializedVersion = new SerializedVersion { Version = metadata.SerializedVersion };
                    ((IPropertyAccept<SerializedVersion>)s_SerializedVersionProperty).Accept(this, ref serializedVersion);
                }
            }
        }

        void WriteSerializedReference(int id)
        {
            using (m_Writer.WriteObjectScope())
            {
                WriteSerializedReferenceKeyValue(id);
            }
        }

        internal void WriteSerializedReferenceKeyValue(int id)
        {
            m_Writer.WriteKey(k_SerializedReferenceKey);
            m_Writer.WriteValue(id);
        }

        void IPropertyBagVisitor.Visit<TContainer>(IPropertyBag<TContainer> properties, ref TContainer container)
        {
            var isRootContainer = properties is IPropertyWrapper;

            var count = 0;
            bool skipBeginEndObject = SkipBeginEndObject;
            SkipBeginEndObject = false;

            if (!isRootContainer)
            {
                var metadata = GetSerializedContainerMetadata(ref container);

                if (metadata.IsSerializedReference && !skipBeginEndObject)
                {
                    WriteSerializedReference(metadata.SerializedId);
                    return;
                }

                if (!skipBeginEndObject)
                {
                    m_Writer.WriteBeginObject();
                }

                if (!skipBeginEndObject)
                {
                    WriteSerializedContainerMetadata(ref container, metadata, ref count);
                }
            }
            foreach (var property in properties.GetProperties(ref container))
            {
                if (PropertyChecks.IsPropertyExcludedFromSerialization(property))
                    continue;

                using (CreatePropertyScope(property))
                    property.Accept(this, ref container);
            }

            if (!isRootContainer && !skipBeginEndObject)
            {
                m_Writer.WriteEndObject();
            }
        }

        void ICollectionPropertyBagVisitor.Visit<TCollection, TElement>(ICollectionPropertyBag<TCollection, TElement> properties, ref TCollection container)
        {
            var metadata = GetSerializedContainerMetadata(ref container);

            if (metadata.IsSerializedReference)
            {
                WriteSerializedReference(metadata.SerializedId);
                return;
            }

            var metadataCount = 0;

            if (metadata.Exists)
            {
                m_Writer.WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata, ref metadataCount);
                m_Writer.WriteKey(k_SerializedElementsKey);
            }

            using (m_Writer.WriteArrayScope())
            {
                foreach (var property in properties.GetProperties(ref container))
                {
                    using (CreatePropertyScope(property))
                        property.Accept(this, ref container);
                }
            }

            if (metadata.Exists)
            {
                m_Writer.WriteEndObject();
            }
        }

        void IListPropertyBagVisitor.Visit<TList, TElement>(IListPropertyBag<TList, TElement> properties, ref TList container)
        {
            var metadata = GetSerializedContainerMetadata(ref container);

            if (metadata.IsSerializedReference)
            {
                WriteSerializedReference(metadata.SerializedId);
                return;
            }

            var metadataCount = 0;

            if (metadata.Exists)
            {
                m_Writer.WriteBeginObject();
                WriteSerializedContainerMetadata(ref container, metadata, ref metadataCount);
                m_Writer.WriteKey(k_SerializedElementsKey);
            }

            using (m_Writer.WriteArrayScope())
            {
                var propertyCollection = properties.GetProperties(ref container);
                foreach (var property in propertyCollection)
                {
                    using (CreatePropertyScope(property))
                        property.Accept(this, ref container);
                }
            }

            if (metadata.Exists)
            {
                m_Writer.WriteEndObject();
            }
        }

        void IDictionaryPropertyBagVisitor.Visit<TDictionary, TKey, TValue>(IDictionaryPropertyBag<TDictionary, TKey, TValue> properties, ref TDictionary container)
        {
            if (typeof(TKey) != typeof(string))
            {
                ((ICollectionPropertyBagVisitor)this).Visit(properties, ref container);
            }
            else
            {
                var metadata = GetSerializedContainerMetadata(ref container);

                if (metadata.IsSerializedReference)
                {
                    WriteSerializedReference(metadata.SerializedId);
                    return;
                }

                var metadataCount = 0;

                if (metadata.Exists)
                {
                    m_Writer.WriteBeginObject();
                    WriteSerializedContainerMetadata(ref container, metadata, ref metadataCount);
                    m_Writer.WriteKey(k_SerializedElementsKey);
                }

                using (m_Writer.WriteObjectScope())
                {
                    // @FIXME allocations
                    var property = new DictionaryElementProperty<TDictionary, TKey, TValue>();

                    foreach (var kvp in container)
                    {
                        property.Key = kvp.Key;
                        ((IPropertyAccept<TDictionary>)property).Accept(this, ref container);
                    }
                }

                if (metadata.Exists)
                {
                    m_Writer.WriteEndObject();
                }
            }
        }

        void IPropertyVisitor.Visit<TContainer, TValue>(Property<TContainer, TValue> property, ref TContainer container)
        {
            var isRootProperty = property is IPropertyWrapper;

            if (!isRootProperty && !(property is ICollectionElementProperty))
            {
                m_Writer.WriteKey(property.Name);
            }

            var value = property.GetValue(ref container);
            WriteValue(ref value, isRootProperty);
        }

        internal void WriteValue<TValue>(ref TValue value, bool isRoot = false)
        {
            var filter = JsonAdapterFilter.All;

            if (isRoot && m_DisableRootAdapters)
            {
                filter = JsonAdapterFilter.None;
            }
            else if (TypeTraits<TValue>.IsPrimitiveOrString)
            {
                if (!m_HasPrimitiveOrStringGlobalAdapters)
                    filter &= ~JsonAdapterFilter.Global;

                if (!m_HasPrimitiveOrStringUserDefinedAdapters)
                    filter &= ~JsonAdapterFilter.UserDefined;
            }

            WriteValueWithAdapters(value, m_Adapters.GetEnumerator(filter), isRoot);
        }

        internal void WriteValueWithAdapters<TValue>(TValue value, JsonAdapterCollection.Enumerator adapters, bool isRoot)
        {
            while (adapters.MoveNext())
            {
                switch (adapters.Current)
                {
                    case IJsonAdapter<TValue> typed:
                        typed.Serialize(new JsonSerializationContext<TValue>(this, adapters, value, isRoot), value);
                        return;

                    case IContravariantJsonAdapter<TValue> typedContravariant:
                        // NOTE: Boxing
                        typedContravariant.Serialize((IJsonSerializationContext)new JsonSerializationContext<TValue>(this, adapters, value, isRoot), value);
                        return;
                }
            }

            // Do the default thing.
            WriteValueWithoutAdapters(value, isRoot);
        }

        internal void WriteValueWithoutAdapters<TValue>(TValue value, bool isRoot)
        {
            if (!TypeTraits<TValue>.IsValueType)
            {
                if (!(isRoot && m_DisableRootAdapters))
                {
                    if (value is UnityEngine.Object)
                    {
                        throw new NotSupportedException("JsonSerialization does not support polymorphic unity object references.");
                    }
                }
            }

#if UNITY_EDITOR
            if (TypeTraits<TValue>.IsLazyLoadReference)
            {
                var instanceID = PropertyContainer.GetValue<TValue, int>(ref value, "m_InstanceID");
                Writer.WriteValue(UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(instanceID).ToString());
                return;
            }
#endif

            if (TypeTraits<TValue>.IsEnum)
            {
                WritePrimitiveBoxed(m_Writer, value, Enum.GetUnderlyingType(typeof(TValue)));
                return;
            }

            if (TypeTraits<TValue>.CanBeNull && EqualityComparer<TValue>.Default.Equals(value, default))
            {
                m_Writer.WriteNull();
                return;
            }

            if (TypeTraits<TValue>.IsMultidimensionalArray)
            {
                // No support for multidimensional arrays yet. This can be done using adapters for now.
                m_Writer.WriteNull();
                return;
            }

            if (TypeTraits<TValue>.IsNullable)
            {
                var underlyingType = Nullable.GetUnderlyingType(typeof(TValue));

                if (TypeTraits.IsContainer(underlyingType))
                {
                    // Unpack Nullable<T> as T
                    var underlyingValue = System.Convert.ChangeType(value, underlyingType);

                    if (!PropertyContainer.TryAccept(this, ref underlyingValue, out var errorCode))
                    {
                        switch (errorCode)
                        {
                            case VisitReturnCode.NullContainer:
                                throw new ArgumentNullException(nameof(value));
                            case VisitReturnCode.InvalidContainerType:
                                throw new InvalidContainerTypeException(value.GetType());
                            case VisitReturnCode.MissingPropertyBag:
                                throw new MissingPropertyBagException(value.GetType());
                            default:
                                throw new Exception($"Unexpected {nameof(VisitReturnCode)}=[{errorCode}]");
                        }
                    }
                }
                else
                {
                    WritePrimitiveBoxed(m_Writer, value, underlyingType);
                }

                return;
            }

            if (TypeTraits<TValue>.IsObject && !TypeTraits.IsContainer(value.GetType()))
            {
                WritePrimitiveBoxed(m_Writer, value, value.GetType());
                return;
            }

            if (TypeTraits<TValue>.IsContainer)
            {
                if (!PropertyContainer.TryAccept(this, ref value, out var errorCode))
                {
                    switch (errorCode)
                    {
                        case VisitReturnCode.NullContainer:
                            throw new ArgumentNullException(nameof(value));
                        case VisitReturnCode.InvalidContainerType:
                            throw new InvalidContainerTypeException(value.GetType());
                        case VisitReturnCode.MissingPropertyBag:
                            throw new MissingPropertyBagException(value.GetType());
                        default:
                            throw new Exception($"Unexpected {nameof(VisitReturnCode)}=[{errorCode}]");
                    }
                }
                return;
            }

            throw new Exception($"Unsupported Type {value.GetType()}.");
        }

        internal static void WritePrimitiveBoxed(JsonWriter writer, object value, Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                    writer.WriteValue((sbyte)value);
                    return;

                case TypeCode.Int16:
                    writer.WriteValue((short)value);
                    return;

                case TypeCode.Int32:
                    writer.WriteValue((int)value);
                    return;

                case TypeCode.Int64:
                    writer.WriteValue((long)value);
                    return;

                case TypeCode.Byte:
                    writer.WriteValue((byte)value);
                    return;

                case TypeCode.UInt16:
                    writer.WriteValue((ushort)value);
                    return;

                case TypeCode.UInt32:
                    writer.WriteValue((uint)value);
                    return;

                case TypeCode.UInt64:
                    writer.WriteValue((ulong)value);
                    return;

                case TypeCode.Single:
                    writer.WriteValue((float)value);
                    return;

                case TypeCode.Double:
                    writer.WriteValue((double)value);
                    return;

                case TypeCode.Boolean:
                    writer.WriteValueLiteral(((bool)value) ? "true" : "false");
                    return;

                case TypeCode.Char:
                    writer.WriteValue((char)value);
                    return;

                case TypeCode.String:
                    writer.WriteValue(value as string);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}