using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IntoChat.OS.Modules.Flutter.Tests.Integration.Contracts;

// A fully populated instance of a wire record, built by reflection so every member appears in its golden: a value
// is derived from the member's name (strings carry the name, numbers its length, enums their last member), so an
// added member changes only its own line and a renamed one fails the pin.
internal static class UiSamples
{
    private const int MaxDepth = 3;

    public static object Of(Type type) => Value(type, type.Name, 0)!;

    private static object? Value(Type type, string name, int depth)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) { return Value(inner, name, depth); }
        if (type == typeof(string)) { return name; }
        if (type == typeof(bool)) { return true; }
        if (type == typeof(int)) { return name.Length; }
        if (type == typeof(long)) { return (long)name.Length * 1000; }
        if (type == typeof(short)) { return (short)name.Length; }
        if (type == typeof(byte)) { return (byte)name.Length; }
        if (type == typeof(uint)) { return (uint)name.Length; }
        if (type == typeof(ulong)) { return (ulong)name.Length; }
        if (type == typeof(double)) { return name.Length + 0.5; }
        if (type == typeof(float)) { return name.Length + 0.5f; }
        if (type == typeof(decimal)) { return name.Length + 0.5m; }
        if (type == typeof(Guid)) { return new Guid(MD5.HashData(Encoding.UTF8.GetBytes(name))); }
        if (type == typeof(DateTimeOffset)) { return new DateTimeOffset(2026, 10, 18, 0, name.Length, 0, TimeSpan.Zero); }
        if (type == typeof(DateTime)) { return new DateTime(2026, 10, 18, 0, name.Length, 0, DateTimeKind.Utc); }
        if (type == typeof(TimeSpan)) { return TimeSpan.FromSeconds(name.Length); }
        if (type == typeof(Uri)) { return new Uri("https://example.test/" + name); }
        if (type == typeof(JsonElement)) { return JsonSerializer.SerializeToElement(new Dictionary<string, string> { [name] = name }); }
        if (type == typeof(byte[])) { return Encoding.UTF8.GetBytes(name); }
        if (type.IsEnum) { return Enum.GetValues(type).Cast<object>().Last(); }
        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            var array = Array.CreateInstance(element, depth >= MaxDepth ? 0 : 1);
            if (array.Length == 1) { array.SetValue(Value(element, name, depth + 1), 0); }
            return array;
        }
        if (type.IsGenericType && Dictionary(type) is { } dictionary) { return dictionary(name, depth); }
        if (type.IsGenericType && Sequence(type) is { } item)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
            if (depth < MaxDepth) { list.Add(Value(item, name, depth + 1)); }
            return type.IsAssignableFrom(list.GetType()) ? list : Activator.CreateInstance(type, list);
        }
        if (type.IsAbstract || type.IsInterface) { throw new NotSupportedException($"No sample for {type.FullName} ({name})."); }
        return Record(type, depth);
    }

    private static object Record(Type type, int depth)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault()
            ?? throw new NotSupportedException($"No public constructor on {type.FullName}.");
        var parameters = constructor.GetParameters();
        var instance = constructor.Invoke([.. parameters.Select(p => Value(p.ParameterType, p.Name!, depth + 1))]);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is not { IsPublic: true } || property.GetIndexParameters().Length > 0) { continue; }
            if (parameters.Any(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase))) { continue; }
            property.SetValue(instance, Value(property.PropertyType, property.Name, depth + 1));
        }
        return instance;
    }

    private static Func<string, int, object>? Dictionary(Type type)
    {
        var definition = type.GetGenericTypeDefinition();
        if (definition != typeof(Dictionary<,>) && definition != typeof(IDictionary<,>) && definition != typeof(IReadOnlyDictionary<,>)) { return null; }
        var (key, value) = (type.GetGenericArguments()[0], type.GetGenericArguments()[1]);
        return (name, depth) =>
        {
            var map = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(key, value))!;
            if (depth < MaxDepth) { map.Add(Value(key, name + "Key", depth + 1)!, Value(value, name, depth + 1)); }
            return map;
        };
    }

    private static Type? Sequence(Type type)
    {
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>)
            || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(ICollection<>)
            ? type.GetGenericArguments()[0] : null;
    }
}
