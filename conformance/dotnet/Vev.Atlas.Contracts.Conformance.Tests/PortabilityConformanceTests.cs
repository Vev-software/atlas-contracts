using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance for the portability surface (#4): the export document (landscape) and the import
/// bundle. Proves the published schemas accept valid documents and reject malformed ones, that the
/// .NET SDK serialises to a schema-valid shape, that a real SDK-produced export round-trips, and
/// that bundle reference resolution behaves (handbook 11 §2-3).
/// </summary>
public sealed class PortabilityConformanceTests
{
    private static readonly string SchemaDir = Path.Combine(AppContext.BaseDirectory, "schemas", "v1");
    private static readonly string SampleDir = Path.Combine(AppContext.BaseDirectory, "samples");

    private static readonly JsonSchema ImportSchema = BuildSchema("import.schema.json");
    private static readonly JsonSchema LandscapeSchema = BuildSchema("landscape.schema.json");

    private static JsonSchema BuildSchema(string entry)
    {
        // Register every schema by its $id so relative $refs resolve against the registry, offline.
        foreach (var file in Directory.EnumerateFiles(SchemaDir, "*.json"))
        {
            SchemaRegistry.Global.Register(JsonSchema.FromText(File.ReadAllText(file)));
        }

        return JsonSchema.FromText(File.ReadAllText(Path.Combine(SchemaDir, entry)));
    }

    private static EvaluationResults Evaluate(JsonSchema schema, JsonNode? instance) =>
        schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

    // ---- export (landscape) ----

    [Fact]
    public void Export_document_round_trips_through_the_sdk()
    {
        // Simulates a runtime export: build with the SDK, serialise, and prove the wire form both
        // conforms to the schema and survives a deserialise → re-serialise round-trip unchanged.
        var export = new LandscapeDocument(
            Assets:
            [
                new Asset("app-1", AssetKind.Application, "Billing", Lifecycle.Active,
                    Tags: [new Tag("tier", "critical")],
                    Application: new ApplicationDetails(Version: "1.0.0", Vendor: "in-house")),
                new Asset("srv-1", AssetKind.Server, "billing-01", Lifecycle.Active,
                    Server: new ServerDetails(Hostname: "billing-01", Environment: "production", OperatingSystem: "RHEL 9"))
            ],
            Relationships: [new Relationship("r1", "app-1", "srv-1", RelationshipType.RunsOn)],
            ExportedAt: DateTimeOffset.UtcNow,
            Generator: new Generator("Atlas Community", "0.1.0"));

        var json1 = JsonSerializer.Serialize(export, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<LandscapeDocument>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(LandscapeSchema, JsonNode.Parse(json2)).IsValid);
    }

    // ---- import bundle ----

    [Fact]
    public void Import_sample_bundle_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "import.sample.json")));

        var results = Evaluate(ImportSchema, instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_import_bundle_serialises_to_a_schema_valid_document()
    {
        var bundle = new ImportBundle(
            Assets:
            [
                new ImportAsset(AssetKind.System, "Payments platform", Lifecycle.Active,
                    ExternalId: "cmdb:SYS-0091"),
                new ImportAsset(AssetKind.Application, "Checkout", Lifecycle.Active,
                    ExternalId: "cmdb:APP-1043",
                    Application: new ApplicationDetails(Version: "4.2.1"))
            ],
            Relationships:
            [
                new ImportRelationship("cmdb:APP-1043", "cmdb:SYS-0091", RelationshipType.PartOf)
            ]);

        var json = JsonSerializer.Serialize(bundle, AtlasContracts.SerializerOptions);
        var results = Evaluate(ImportSchema, JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Import_asset_without_id_or_externalId_is_rejected()
    {
        // The bundle must be able to match/reference every asset, so one identifier is mandatory.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "import", "assets": [
              { "kind": "application", "name": "X", "lifecycle": "active" } ] }
            """);

        Assert.False(Evaluate(ImportSchema, instance).IsValid);
    }

    [Fact]
    public void Import_bundle_with_unknown_property_is_rejected()
    {
        // A paid-core concept must not smuggle itself into the public import contract.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "import", "assets": [
              { "externalId": "e1", "kind": "application", "name": "X", "lifecycle": "active", "criticality": "high" } ] }
            """);

        Assert.False(Evaluate(ImportSchema, instance).IsValid);
    }

    // ---- reference resolution (SDK helper) ----

    [Fact]
    public void Sample_import_bundle_has_no_unresolved_references()
    {
        var bundle = JsonSerializer.Deserialize<ImportBundle>(
            File.ReadAllText(Path.Combine(SampleDir, "import.sample.json")), AtlasContracts.SerializerOptions);

        Assert.Empty(bundle!.UnresolvedReferences());
    }

    [Fact]
    public void Import_bundle_detects_a_dangling_reference()
    {
        var bundle = new ImportBundle(
            Assets: [new ImportAsset(AssetKind.Application, "X", Lifecycle.Active, ExternalId: "e1")],
            Relationships: [new ImportRelationship("e1", "does-not-exist", RelationshipType.DependsOn)]);

        Assert.Equal("does-not-exist", Assert.Single(bundle.UnresolvedReferences()));
    }

    private static string Describe(EvaluationResults results)
    {
        var errors = results.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} — {e.Value}"));
        return "Schema validation failed:\n" + string.Join('\n', errors);
    }
}
