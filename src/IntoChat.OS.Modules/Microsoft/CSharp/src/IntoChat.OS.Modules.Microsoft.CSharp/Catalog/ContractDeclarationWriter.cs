using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntoChat.OS.Framework.Modules;

using MicrosoftSdk = Microsoft;
namespace IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

// These are authoring declarations, not generated source. Each group is self-contained so consumers
// can drop a whole method and its schema without leaving an apparently callable, incomplete method.
internal sealed class ContractDeclarationWriter(ModuleInventory inventory)
{
    private const int MaximumCharacters = 24000;
    private readonly NullabilityInfoContext _nullability = new();

    public Type Resolve(string moduleId, string name)
    {
        var matches = inventory.Modules.Where(module => string.Equals(module.Id.Value, moduleId, StringComparison.OrdinalIgnoreCase))
            .Select(module => module.Contracts).OfType<Assembly>()
            // CompiledTypes names unowned core vocabulary by its assembly name.
            .Concat(inventory.ContractAssemblies().Where(assembly => inventory.Declaring(assembly) is null
                && string.Equals(assembly.GetName().Name, moduleId, StringComparison.OrdinalIgnoreCase))).Distinct()
            .Select(assembly => assembly.GetType(name, false, false)).OfType<Type>().Where(type => type.IsVisible).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(
            $"Contract '{name}' in module '{moduleId}' has {matches.Length} public resolutions in its declared contract assemblies.");
    }

    public static MethodInfo[] Methods(Type contract) => [.. contract.GetInterfaces().Append(contract)
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(method => !method.IsSpecialName).DistinctBy(method => method.ToString())
        .OrderBy(method => method.Name, StringComparer.Ordinal).ThenBy(method => method.ToString(), StringComparer.Ordinal)];

    public string Write(Type contract, MethodInfo? method, string heading)
    {
        var text = new StringBuilder(heading);
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>();
        void Add(string value)
        {
            if (text.Length + value.Length > MaximumCharacters)
            { throw new InvalidOperationException("The complete contract schema exceeds the 24000-character discovery limit; select a smaller method or simplify its DTO contract."); }
            text.Append(value);
        }
        void Visit(Type type)
        {
            if (type.IsByRef || type.IsArray) { Visit(type.GetElementType()!); return; }
            if (type.IsGenericParameter) { return; }
            if (type.IsGenericType) { foreach (var argument in type.GetGenericArguments()) { Visit(argument); } }
            if (Builtin(type)) { return; }
            if (!type.IsVisible || !Allowed(type.Assembly))
            { throw new InvalidOperationException($"Type '{type.FullName}' is not a public type in the installed contract vocabulary."); }
            if (seen.Add(type)) { pending.Enqueue(type); }
        }
        if (method is { })
        {
            Add(" { " + Name(method.ReturnType, _nullability.Create(method.ReturnParameter)) + " " + method.Name + "(" +
                string.Join(", ", method.GetParameters().Select(Parameter)) + "); }");
            Visit(method.ReturnType);
            foreach (var parameter in method.GetParameters()) { Visit(parameter.ParameterType); }
        }
        else { Visit(contract); }
        while (pending.TryDequeue(out var type))
        {
            if (seen.Count > 256) { throw new InvalidOperationException("The complete contract schema exceeds the 256-type discovery limit."); }
            Add("\n");
            if (type.IsEnum)
            {
                Add("enum " + Name(type) + " { " + string.Join(", ", type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.Name + " = " + Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture))) + " }");
                continue;
            }
            var polymorphism = type.GetCustomAttribute<JsonPolymorphicAttribute>();
            var alternatives = type.GetCustomAttributes<JsonDerivedTypeAttribute>().OrderBy(attribute => attribute.DerivedType.FullName, StringComparer.Ordinal).ToArray();
            if (polymorphism is not null || alternatives.Length > 0)
            {
                Add("[JsonPolymorphic(TypeDiscriminatorPropertyName = " + JsonSerializer.Serialize(polymorphism?.TypeDiscriminatorPropertyName ?? "$type") + ")]\n");
                foreach (var alternative in alternatives)
                {
                    Add("[JsonDerivedType(typeof(" + Name(alternative.DerivedType) + "), " + JsonSerializer.Serialize(alternative.TypeDiscriminator) + ")]\n");
                    Visit(alternative.DerivedType);
                }
            }
            Add((type.IsAbstract ? "abstract " : "") + (type.IsValueType ? "struct " : "class ") + Name(type) + " {");
            foreach (var constructor in type.GetConstructors().OrderBy(ctor => ctor.ToString(), StringComparer.Ordinal))
            {
                Add("\n  public " + type.Name.Split('`')[0] + "(" + string.Join(", ", constructor.GetParameters().Select(Parameter)) + ");");
                foreach (var parameter in constructor.GetParameters()) { Visit(parameter.ParameterType); }
            }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (property.GetIndexParameters().Length > 0) { continue; }
                var setter = property.SetMethod is { IsPublic: true } set
                    ? (set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) ? " init;" : " set;") : "";
                Add("\n  public " + (property.IsDefined(typeof(RequiredMemberAttribute)) ? "required " : "") + Name(property.PropertyType, _nullability.Create(property)) + " " + property.Name +
                    " {" + (property.GetMethod is { IsPublic: true } ? " get;" : "") + setter + " }");
                Visit(property.PropertyType);
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                Add("\n  public " + (field.IsInitOnly ? "readonly " : "") + Name(field.FieldType, _nullability.Create(field)) + " " + field.Name + ";");
                Visit(field.FieldType);
            }
            Add("\n}");
        }
        return text.ToString();
    }

    private bool Allowed(Assembly assembly) => inventory.ContractAssemblies().Contains(assembly)
        || assembly.GetName().Name?.EndsWith(".Contracts", StringComparison.Ordinal) == true;

    // Scalars and standard collection/async containers need no DTO declaration. Container arguments
    // still recurse; belonging to a BCL assembly alone never makes an arbitrary type a complete schema.
    private static readonly HashSet<Type> Scalars = [typeof(void), typeof(object), typeof(string), typeof(decimal),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(Guid),
        typeof(Uri), typeof(CancellationToken), typeof(Task), typeof(ValueTask), typeof(JsonElement), typeof(JsonDocument)];
    private static readonly HashSet<Type> Containers = [typeof(Task<>), typeof(ValueTask<>), typeof(Nullable<>),
        typeof(IEnumerable<>), typeof(ICollection<>), typeof(IReadOnlyCollection<>), typeof(IList<>), typeof(IReadOnlyList<>),
        typeof(List<>), typeof(ISet<>), typeof(IReadOnlySet<>), typeof(HashSet<>), typeof(Dictionary<,>),
        typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>), typeof(KeyValuePair<,>), typeof(IAsyncEnumerable<>),
        typeof(System.Collections.Immutable.ImmutableArray<>), typeof(System.Collections.Immutable.ImmutableList<>),
        typeof(System.Collections.Immutable.ImmutableDictionary<,>), typeof(System.Collections.Immutable.ImmutableHashSet<>)];

    private static bool Builtin(Type type) => type.IsPrimitive || Scalars.Contains(type) || type.IsGenericType
        && (Containers.Contains(type.GetGenericTypeDefinition()) || type.Assembly == typeof(string).Assembly
            && (type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal) || type.FullName.StartsWith("System.Tuple`", StringComparison.Ordinal)));

    private string Parameter(ParameterInfo parameter) => Name(parameter.ParameterType, _nullability.Create(parameter)) + " " + parameter.Name
        + (parameter.HasDefaultValue ? " = " + Default(parameter) : "");

    private static string Default(ParameterInfo parameter) => parameter.DefaultValue is null ? (parameter.ParameterType.IsValueType ? "default" : "null")
        : parameter.ParameterType.IsEnum ? "(" + Name(parameter.ParameterType) + ")" + JsonSerializer.Serialize(parameter.DefaultValue)
        : parameter.DefaultValue == DBNull.Value || parameter.DefaultValue == Missing.Value ? "default"
        : parameter.DefaultValue switch
        {
            char value => MicrosoftSdk.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true),
            float value when float.IsNaN(value) => "float.NaN",
            float value when float.IsPositiveInfinity(value) => "float.PositiveInfinity",
            float value when float.IsNegativeInfinity(value) => "float.NegativeInfinity",
            float value => value.ToString("R", CultureInfo.InvariantCulture) + "f",
            double value when double.IsNaN(value) => "double.NaN",
            double value when double.IsPositiveInfinity(value) => "double.PositiveInfinity",
            double value when double.IsNegativeInfinity(value) => "double.NegativeInfinity",
            decimal value => value.ToString(CultureInfo.InvariantCulture) + "m",
            _ => JsonSerializer.Serialize(parameter.DefaultValue),
        };

    private static string Name(Type type, NullabilityInfo? nullable = null)
    {
        if (type.IsByRef) { return Name(type.GetElementType()!, nullable); }
        if (Nullable.GetUnderlyingType(type) is { } underlying) { return Name(underlying) + "?"; }
        var suffix = !type.IsValueType && nullable?.ReadState == NullabilityState.Nullable ? "?" : "";
        if (type.IsArray) { return Name(type.GetElementType()!, nullable?.ElementType) + "[" + new string(',', type.GetArrayRank() - 1) + "]" + suffix; }
        if (type.IsGenericParameter) { return type.Name; }
        var alias = Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => "bool",
            TypeCode.Byte => "byte",
            TypeCode.SByte => "sbyte",
            TypeCode.Char => "char",
            TypeCode.Int16 => "short",
            TypeCode.UInt16 => "ushort",
            TypeCode.Int32 => "int",
            TypeCode.UInt32 => "uint",
            TypeCode.Int64 => "long",
            TypeCode.UInt64 => "ulong",
            TypeCode.Single => "float",
            TypeCode.Double => "double",
            TypeCode.Decimal => "decimal",
            TypeCode.String => "string",
            _ => null,
        };
        if (!type.IsEnum && alias is not null) { return alias + suffix; }
        if (type == typeof(void)) { return "void"; }
        if (!type.IsGenericType) { return (type.FullName ?? type.Name).Replace('+', '.') + suffix; }
        var arguments = type.GetGenericArguments();
        var offset = 0;
        var segments = type.GetGenericTypeDefinition().FullName!.Split('+').Select(segment =>
        {
            var tick = segment.IndexOf('`', StringComparison.Ordinal);
            if (tick < 0) { return segment; }
            var count = int.Parse(segment[(tick + 1)..], CultureInfo.InvariantCulture);
            var values = Enumerable.Range(offset, count).Select(index => Name(arguments[index], nullable?.GenericTypeArguments.ElementAtOrDefault(index))).ToArray();
            offset += count;
            return segment[..tick] + "<" + string.Join(", ", values) + ">";
        });
        return string.Join(".", segments) + suffix;
    }
}
