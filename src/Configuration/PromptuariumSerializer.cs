using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Promptuarium;

namespace PentaGrammata.Configuration;

/// <summary>
/// Maps .NET objects to <c>PRMT:CFG1-LE</c> Promptuarium containers and back, as specified
/// in <c>docs/CONFIG_FORMAT.md</c>, without any intermediate representation:
/// <list type="bullet">
/// <item><c>bool</c> ↔ BOOLEAN; every number type ↔ FLOAT64; <c>string</c>, <c>char</c>
/// and enums (by name) ↔ UTF8_STRING; <c>byte[]</c> ↔ BLOB;</item>
/// <item>string-keyed dictionaries and objects (their public read/write properties) ↔ OBJECT;
/// other collections ↔ ARRAY; <c>null</c> ↔ NULL.</item>
/// </list>
/// Reading into <see cref="object"/> gives <see cref="Dictionary{TKey,TValue}"/>,
/// <see cref="List{T}"/>, <see cref="bool"/>, <see cref="double"/>, <see cref="string"/>,
/// <c>byte[]</c> or <c>null</c>. Member names match case-insensitively, and unknown members
/// are ignored.
/// </summary>
public static class PromptuariumSerializer
{
    public const string Signature = "PRMT:CFG1-LE";

    // A configuration file is tiny; the limits only stop a corrupt or hostile file from
    // allocating (or recursing) without bound.
    private static readonly LoadOptions LoadLimits = new() { MaxBytes = 16 * 1024 * 1024, MaxNodes = 1_000_000 };
    private const int MaxDepth = 64;

    private static readonly byte[] SignatureBytes = Encoding.ASCII.GetBytes(Signature);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const byte NameTag = 0x00;
    private const byte TypeTag = 0x01;
    private const byte ItemTag = 0x02;
    private const byte ValueTag = 0x03;

    private const byte NullType = 0x00;
    private const byte BooleanType = 0x01;
    private const byte ObjectType = 0x20;
    private const byte ArrayType = 0x21;
    private const byte Float64Type = 0x63;
    private const byte Utf8StringType = 0x80;
    private const byte BlobType = 0xF0;

    /// <summary>Encodes <paramref name="value"/>, which must map to an OBJECT (an object or a dictionary).</summary>
    public static byte[] Serialize(object value) => ToElement(value).ToBytes();

    /// <inheritdoc cref="Serialize"/>
    public static Element ToElement(object value)
    {
        var root = new ElementBuilder()
            .WithSignature(Data.FromAsciiString(Signature))
            .Build();

        var members = Members(value)
            ?? throw new NotSupportedException($"A {value.GetType().Name} can not be the root of a configuration.");
        foreach (var (name, member) in members)
        {
            root.Add(Holder(NameTag, Data.FromUtf8String(name), member, 1));
        }

        return root;
    }

    /// <exception cref="InvalidDataException">The bytes are not a valid <c>PRMT:CFG1-LE</c> container, or do not fit <typeparamref name="T"/>.</exception>
    public static T Deserialize<T>(ReadOnlyMemory<byte> bytes) => Deserialize<T>(Load(bytes));

    /// <inheritdoc cref="Deserialize{T}(ReadOnlyMemory{byte})"/>
    public static T Deserialize<T>(Element root)
    {
        return (T)ReadObject(RootMembers(root), typeof(T), existing: null, 1)!;
    }

    /// <summary>
    /// Applies a container onto <paramref name="target"/>: members present in it are
    /// overwritten, nested objects and dictionaries are applied member by member, arrays
    /// are replaced, and a NULL member leaves the current value in place. Layering the
    /// per-user settings on top of the defaults is exactly this.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are not a valid <c>PRMT:CFG1-LE</c> container, or do not fit the target.</exception>
    public static void Populate(ReadOnlyMemory<byte> bytes, object target)
    {
        ReadObject(RootMembers(Load(bytes)), target.GetType(), target, 1);
    }

    /// <exception cref="InvalidDataException">The bytes are not a Promptuarium container.</exception>
    public static Element Load(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return Element.FromBytes(bytes, LoadLimits);
        }
        catch (PromptuariumException ex)
        {
            throw new InvalidDataException("The configuration is not a Promptuarium container.", ex);
        }
    }

    // ---------------------------------------------------------------- writing

    private static Element Holder(byte tag, ReadOnlyMemory<byte> name, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new NotSupportedException("The configuration is nested too deeply (or contains a cycle).");
        }

        var holder = new Element(name, Data.FromByte(tag));
        switch (value)
        {
            case null:
                holder.Add(TypeNode(NullType));
                break;
            case bool flag:
                holder.Add(TypeNode(BooleanType), ValueNode(Data.FromBool(flag)));
                break;
            case string text:
                holder.Add(TypeNode(Utf8StringType), ValueNode(Data.FromUtf8String(text)));
                break;
            case char character:
                holder.Add(TypeNode(Utf8StringType), ValueNode(Data.FromUtf8String(character.ToString())));
                break;
            case Enum enumValue:
                holder.Add(TypeNode(Utf8StringType), ValueNode(Data.FromUtf8String(enumValue.ToString())));
                break;
            case byte[] blob:
                holder.Add(TypeNode(BlobType), ValueNode(blob));
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (!double.IsFinite(number))
                {
                    throw new NotSupportedException("A configuration number must be finite.");
                }

                holder.Add(TypeNode(Float64Type), ValueNode(Data.FromDouble(number)));
                break;
            default:
                if (Members(value) is { } members)
                {
                    holder.Add(TypeNode(ObjectType));
                    foreach (var (memberName, member) in members)
                    {
                        holder.Add(Holder(NameTag, Data.FromUtf8String(memberName), member, depth + 1));
                    }
                }
                else
                {
                    holder.Add(TypeNode(ArrayType));
                    foreach (var item in (IEnumerable)value)
                    {
                        holder.Add(Holder(ItemTag, ReadOnlyMemory<byte>.Empty, item, depth + 1));
                    }
                }
                break;
        }

        return holder;
    }

    /// <summary>The members of an OBJECT, or null when <paramref name="value"/> is an ARRAY.</summary>
    private static IEnumerable<(string Name, object? Value)>? Members(object value)
    {
        if (value is IDictionary dictionary)
        {
            return DictionaryMembers(dictionary);
        }

        if (value is IEnumerable)
        {
            return null;
        }

        return ReadableProperties(value.GetType()).Select(property => (property.Name, property.GetValue(value)));
    }

    // IDictionary's own enumerator: the generic one of a Dictionary<,> yields KeyValuePairs.
    private static IEnumerable<(string Name, object? Value)> DictionaryMembers(IDictionary dictionary)
    {
        var entries = dictionary.GetEnumerator();
        while (entries.MoveNext())
        {
            yield return (Convert.ToString(entries.Key, CultureInfo.InvariantCulture)!, entries.Value);
        }
    }

    private static IEnumerable<PropertyInfo> ReadableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0);
    }

    private static Element TypeNode(byte type) => new(Data.FromByte(type), Data.FromByte(TypeTag));

    private static Element ValueNode(ReadOnlyMemory<byte> value) => new(value, Data.FromByte(ValueTag));

    // ---------------------------------------------------------------- reading

    private static IReadOnlyList<Element> RootMembers(Element root)
    {
        if (!root.Data.IsEmpty || !root.MetaData.Span.SequenceEqual(SignatureBytes))
        {
            throw new InvalidDataException($"The configuration does not have the signature \"{Signature}\".");
        }

        return root.Children;
    }

    private static object? ReadHolder(Element holder, Type type, object? existing, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidDataException("The configuration is nested too deeply.");
        }

        var children = holder.Children;
        if (children.Count == 0)
        {
            throw new InvalidDataException("A configuration holder has no TYPE node.");
        }

        var typeNode = children[0];
        RequireTag(typeNode, TypeTag, "the first child of a holder");
        RequireLeaf(typeNode);
        if (typeNode.Data.Length != 1)
        {
            throw new InvalidDataException("A TYPE node must hold exactly one byte.");
        }

        var target = Nullable.GetUnderlyingType(type) ?? type;
        switch (typeNode.Data.Span[0])
        {
            case NullType:
                RequireChildCount(holder, 1);
                return existing;

            case ObjectType:
                return ReadObject(children.Skip(1).ToList(), target, existing, depth);

            case ArrayType:
                return ReadArray(children.Skip(1).ToList(), target, depth);

            case BooleanType:
                var flag = ScalarValue(holder);
                if (flag.Length != 1 || flag.Span[0] > 1)
                {
                    throw new InvalidDataException("A BOOLEAN value must be 0x00 or 0x01.");
                }

                return Fit(flag.Span[0] == 1, target);

            case Float64Type:
                var raw = ScalarValue(holder);
                if (raw.Length != sizeof(double))
                {
                    throw new InvalidDataException("A FLOAT64 value must be 8 bytes.");
                }

                var number = raw.AsDouble();
                if (!double.IsFinite(number))
                {
                    throw new InvalidDataException("A FLOAT64 value must be finite.");
                }

                return ConvertNumber(number, target);

            case Utf8StringType:
                return ConvertString(DecodeUtf8(ScalarValue(holder)), target);

            case BlobType:
                return Fit(ScalarValue(holder).ToArray(), target);

            case var unknown:
                throw new InvalidDataException($"Unsupported configuration type code 0x{unknown:x2}.");
        }
    }

    private static object ReadObject(IReadOnlyList<Element> members, Type type, object? existing, int depth)
    {
        if (type == typeof(object))
        {
            type = typeof(Dictionary<string, object?>);
        }

        if (IsScalar(type))
        {
            throw new InvalidDataException($"An OBJECT does not fit a {type.Name}.");
        }

        var target = existing ?? Create(type);
        if (target is IDictionary dictionary)
        {
            var valueType = DictionaryValueType(type);
            foreach (var member in members)
            {
                RequireTag(member, NameTag, "an object member");
                var name = DecodeUtf8(member.Data);
                dictionary[name] = ReadHolder(member, valueType, dictionary.Contains(name) ? dictionary[name] : null, depth + 1);
            }

            return target;
        }

        var properties = ReadableProperties(type)
            .GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var member in members)
        {
            RequireTag(member, NameTag, "an object member");
            if (!properties.TryGetValue(DecodeUtf8(member.Data), out var property))
            {
                ReadHolder(member, typeof(object), null, depth + 1); // validated, then ignored
                continue;
            }

            // A get-only member is still filled in place when it is an object or a dictionary;
            // a value it can not take (e.g. a computed one) is ignored.
            var current = property.GetValue(target);
            var value = ReadHolder(member, property.PropertyType, current, depth + 1);
            if (property.SetMethod is { IsPublic: true })
            {
                property.SetValue(target, value);
            }
        }

        return target;
    }

    private static object ReadArray(IReadOnlyList<Element> items, Type type, int depth)
    {
        if (IsScalar(type) || typeof(IDictionary).IsAssignableFrom(type))
        {
            throw new InvalidDataException($"An ARRAY does not fit a {type.Name}.");
        }

        var elementType = type == typeof(object) ? typeof(object) : CollectionElementType(type);
        var elementIsNullable = !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) is not null;
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in items)
        {
            RequireTag(item, ItemTag, "an array element");
            if (!item.Data.IsEmpty)
            {
                throw new InvalidDataException("An ITEM node must not have data.");
            }

            var element = ReadHolder(item, elementType, null, depth + 1);
            if (element is null && !elementIsNullable)
            {
                throw new InvalidDataException($"A NULL element does not fit a {elementType.Name}.");
            }

            list.Add(element);
        }

        if (type.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        if (type.IsAssignableFrom(list.GetType()))
        {
            return list;
        }

        // A concrete collection type of its own (e.g. a List<T> subclass).
        var collection = (IList)Create(type);
        foreach (var element in list)
        {
            collection.Add(element);
        }

        return collection;
    }

    private static bool IsScalar(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(byte[]);
    }

    private static object Create(Type type)
    {
        try
        {
            return Activator.CreateInstance(type, nonPublic: true)
                ?? throw new InvalidDataException($"Can not create a {type.Name}.");
        }
        catch (Exception ex) when (ex is MissingMethodException or ArgumentException or NotSupportedException or MemberAccessException)
        {
            throw new InvalidDataException($"Can not create a {type.Name} from a configuration.", ex);
        }
    }

    private static Type DictionaryValueType(Type type)
    {
        var dictionary = GenericInterface(type, typeof(IDictionary<,>));
        if (dictionary is null)
        {
            return typeof(object);
        }

        var arguments = dictionary.GetGenericArguments();
        return arguments[0] == typeof(string)
            ? arguments[1]
            : throw new InvalidDataException($"A {type.Name} is not keyed by strings.");
    }

    private static Type CollectionElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        return GenericInterface(type, typeof(IEnumerable<>))?.GetGenericArguments()[0]
            ?? throw new InvalidDataException($"An ARRAY does not fit a {type.Name}.");
    }

    private static Type? GenericInterface(Type type, Type definition)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == definition
            ? type
            : type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == definition);
    }

    private static object Fit(object value, Type target)
    {
        return target.IsInstanceOfType(value)
            ? value
            : throw new InvalidDataException($"A {value.GetType().Name} does not fit a {target.Name}.");
    }

    private static object ConvertNumber(double number, Type target)
    {
        if (target == typeof(double) || target == typeof(object))
        {
            return number;
        }

        if (target.IsEnum)
        {
            return Enum.ToObject(target, (long)ConvertNumber(number, typeof(long)));
        }

        var integral = target == typeof(sbyte) || target == typeof(byte) || target == typeof(short) || target == typeof(ushort)
            || target == typeof(int) || target == typeof(uint) || target == typeof(long) || target == typeof(ulong);
        if (integral && Math.Floor(number) != number)
        {
            throw new InvalidDataException($"{number.ToString(CultureInfo.InvariantCulture)} does not fit a {target.Name}.");
        }

        try
        {
            return System.Convert.ChangeType(number, target, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or OverflowException)
        {
            throw new InvalidDataException($"A number does not fit a {target.Name}.", ex);
        }
    }

    private static object ConvertString(string text, Type target)
    {
        if (target == typeof(string) || target == typeof(object))
        {
            return text;
        }

        if (target.IsEnum)
        {
            return Enum.TryParse(target, text, ignoreCase: true, out var value)
                ? value!
                : throw new InvalidDataException($"\"{text}\" is not a {target.Name}.");
        }

        if (target == typeof(char) && text.Length == 1)
        {
            return text[0];
        }

        throw new InvalidDataException($"A string does not fit a {target.Name}.");
    }

    private static ReadOnlyMemory<byte> ScalarValue(Element holder)
    {
        RequireChildCount(holder, 2);
        var valueNode = holder.Children[1];
        RequireTag(valueNode, ValueTag, "the value of a holder");
        RequireLeaf(valueNode);
        return valueNode.Data;
    }

    private static string DecodeUtf8(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes.Span);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("A configuration string is not valid UTF-8.", ex);
        }
    }

    private static void RequireTag(Element node, byte tag, string role)
    {
        if (node.MetaData.Length != 1 || node.MetaData.Span[0] != tag)
        {
            throw new InvalidDataException($"Unexpected metadata on {role}.");
        }
    }

    private static void RequireLeaf(Element node)
    {
        if (node.Children.Count != 0)
        {
            throw new InvalidDataException("TYPE and VALUE nodes must not have children.");
        }
    }

    private static void RequireChildCount(Element holder, int count)
    {
        if (holder.Children.Count != count)
        {
            throw new InvalidDataException("A configuration holder has an unexpected number of children.");
        }
    }
}
