using System.Text.Json;
using Json.Schema;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Registers the published JSON Schemas into the global registry exactly once, so relative $refs
/// (e.g. "common.schema.json#/$defs/...") resolve against the registry rather than the network.
/// JsonSchema.Net throws if the same $id is registered twice, so every test class shares this single
/// registration instead of each populating <see cref="SchemaRegistry.Global"/> itself.
/// </summary>
internal static class TestSchemas
{
    private static readonly object Gate = new();
    private static readonly SchemaRegistry Registry = new();
    private static readonly BuildOptions BuildOptions = new() { SchemaRegistry = Registry };
    private static readonly Dictionary<string, Uri> SchemaIds = new(StringComparer.OrdinalIgnoreCase);
    private static bool _registered;

    private static readonly string SchemaDir = Path.Combine(AppContext.BaseDirectory, "schemas", "v1");

    /// <summary>The samples directory copied next to the schemas at build time.</summary>
    public static string SampleDir { get; } = Path.Combine(AppContext.BaseDirectory, "samples");

    /// <summary>Load a schema by file name, ensuring every schema is registered for $ref resolution.</summary>
    public static JsonSchema Load(string entry)
    {
        EnsureRegistered();
        return (JsonSchema)Registry.Get(SchemaIds[entry])!;
    }

    private static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(SchemaDir, "*.json"))
            {
                var content = File.ReadAllText(file);
                using var document = JsonDocument.Parse(content);
                var id = new Uri(document.RootElement.GetProperty("$id").GetString()!, UriKind.Absolute);

                SchemaIds[Path.GetFileName(file)] = id;
                JsonSchema.FromText(content, BuildOptions);
            }

            _registered = true;
        }
    }
}
