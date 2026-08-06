using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance kit: third parties (and the Atlas runtime) run this to prove a payload matches the
/// published schemas. Also proves the .NET SDK serialises to a schema-valid shape (handbook 05 §4).
/// </summary>
public sealed class SchemaConformanceTests
{
    private static readonly string SchemaDir = Path.Combine(AppContext.BaseDirectory, "schemas", "v1");
    private static readonly string SampleDir = Path.Combine(AppContext.BaseDirectory, "samples");

    private static readonly JsonSchema LandscapeSchema = BuildLandscapeSchema();

    private static JsonSchema BuildLandscapeSchema()
    {
        // Register every schema by its $id so relative $refs (e.g. "common.schema.json#/$defs/...")
        // resolve against the registry rather than the network.
        foreach (var file in Directory.EnumerateFiles(SchemaDir, "*.json"))
        {
            var schema = JsonSchema.FromText(File.ReadAllText(file));
            SchemaRegistry.Global.Register(schema);
        }

        return JsonSchema.FromText(File.ReadAllText(Path.Combine(SchemaDir, "landscape.schema.json")));
    }

    private static EvaluationResults Evaluate(JsonNode? instance) =>
        LandscapeSchema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

    [Fact]
    public void Sample_landscape_document_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape.sample.json")));

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_serialises_to_a_schema_valid_document()
    {
        // The SDK is the contract's hand: what it writes must satisfy the published schema.
        var document = new LandscapeDocument(
            Assets:
            [
                new Asset("app-1", AssetKind.Application, "Billing", Lifecycle.Active,
                    Tags: [new Tag("tier", "critical")],
                    Application: new ApplicationDetails(Version: "1.0.0", Vendor: "in-house")),
                new Asset("srv-1", AssetKind.Server, "billing-01", Lifecycle.Active,
                    Server: new ServerDetails(Hostname: "billing-01", Environment: "production", OperatingSystem: "RHEL 9"))
            ],
            Relationships:
            [
                new Relationship("r1", "app-1", "srv-1", RelationshipType.RunsOn)
            ],
            ExportedAt: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(document, AtlasContracts.SerializerOptions);
        var instance = JsonNode.Parse(json);

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Document_with_a_bad_lifecycle_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "assets": [
              { "id": "x", "kind": "application", "name": "X", "lifecycle": "on-fire" } ] }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Document_with_unknown_asset_property_is_rejected()
    {
        // The schema pins the catalogue vocabulary; a paid-core concept (e.g. "criticality")
        // must not smuggle itself into the public contract.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "assets": [
              { "id": "x", "kind": "application", "name": "X", "lifecycle": "active", "criticality": "high" } ] }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    private static string Describe(EvaluationResults results)
    {
        var errors = results.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} — {e.Value}"));
        return "Schema validation failed:\n" + string.Join('\n', errors);
    }
}
