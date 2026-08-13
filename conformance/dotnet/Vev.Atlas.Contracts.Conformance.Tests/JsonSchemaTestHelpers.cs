using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Vev.Atlas.Contracts.Conformance.Tests;

internal static class JsonSchemaTestHelpers
{
    private static readonly JsonElement NullElement = JsonDocument.Parse("null").RootElement.Clone();

    internal static EvaluationResults Evaluate(JsonSchema schema, JsonNode? instance) =>
        schema.Evaluate(ToJsonElement(instance), new EvaluationOptions { OutputFormat = OutputFormat.List });

    internal static string Describe(EvaluationResults results)
    {
        var errors = EnumerateResults(results)
            .Where(r => r.Errors is { Count: > 0 })
            .SelectMany(r => r.Errors!.Select(e => $"{r.InstanceLocation}: {e.Key} - {e.Value}"));

        return "Schema validation failed:\n" + string.Join('\n', errors);
    }

    private static JsonElement ToJsonElement(JsonNode? instance)
    {
        if (instance is null) return NullElement;

        using var document = JsonDocument.Parse(instance.ToJsonString());
        return document.RootElement.Clone();
    }

    private static IEnumerable<EvaluationResults> EnumerateResults(EvaluationResults result)
    {
        yield return result;

        foreach (var detail in result.Details ?? [])
        {
            foreach (var nested in EnumerateResults(detail))
            {
                yield return nested;
            }
        }
    }
}
