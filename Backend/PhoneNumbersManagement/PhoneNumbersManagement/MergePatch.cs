using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PhoneNumbersManagement.Domain;

namespace PhoneNumbersManagement;

internal static class MergePatch
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static T Apply<T>(T current, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object)
        {
            throw new RuleViolation("PATCH body must be a JSON object.");
        }

        var node = JsonSerializer.SerializeToNode(current, Options)!.AsObject();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in patch.EnumerateObject())
        {
            if (!node.ContainsKey(field.Name) || !seen.Add(field.Name))
            {
                throw new RuleViolation($"Unknown or duplicate PATCH field: {field.Name}.");
            }

            node[field.Name] = JsonNode.Parse(field.Value.GetRawText()); // Explicit null clears a nullable field.
        }

        try
        {
            return node.Deserialize<T>(Options) ?? throw new RuleViolation("Invalid PATCH body.");
        }
        catch (JsonException) { throw new RuleViolation("PATCH contains an invalid field value."); }
    }
}
