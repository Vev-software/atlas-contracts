using System.Text.Json;
using System.Text.Json.Nodes;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

public sealed class IntegrationObservationConformanceTests
{
    [Theory]
    [InlineData(AssetKind.Integration, "integration")]
    [InlineData(AssetKind.BusinessProcess, "business-process")]
    public void Source_identity_and_kind_survive_discovery_round_trip(AssetKind kind, string wireKind)
    {
        var batch = new DiscoveryObservationBatch(
            new ObservationSource("inventory", CollectionMethod.Agentless),
            DateTimeOffset.Parse("2026-09-17T00:00:00Z"),
            [new Observation("source:123", kind, "Order fulfilment",
                Fingerprint: [new FingerprintAttribute("source-id", "123")],
                Tags: [new Tag("source-status", "enabled")]),
                new Observation("app:456", AssetKind.Application, "Orders")],
            [new ObservedRelationship("source:123", "app:456", RelationshipType.DependsOn)]);

        var json = JsonSerializer.Serialize(batch, AtlasContracts.SerializerOptions);
        var node = JsonNode.Parse(json)!;
        Assert.Equal(wireKind, node["observations"]![0]!["kind"]!.GetValue<string>());
        Assert.True(JsonSchemaTestHelpers.Evaluate(TestSchemas.Load("discovery-observation.schema.json"), node).IsValid);
        var restored = JsonSerializer.Deserialize<DiscoveryObservationBatch>(json, AtlasContracts.SerializerOptions)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored, AtlasContracts.SerializerOptions));
        Assert.Equal("source:123", restored.Observations[0].ObservedId);
        Assert.Equal(kind, restored.Observations[0].Kind);
    }

    [Theory]
    [InlineData(AssetKind.Integration)]
    [InlineData(AssetKind.BusinessProcess)]
    public void New_kinds_remain_portable_through_catalogue_and_import(AssetKind kind)
    {
        var asset = new Asset("asset-1", kind, "Orders", Lifecycle.Active);
        AssertValid("asset.schema.json", asset);
        AssertValid("landscape.schema.json", new LandscapeDocument(Assets: [asset]));
        AssertValid("import.schema.json", new ImportBundle(
            [new ImportAsset(kind, "Orders", Lifecycle.Active, ExternalId: "source:123")]));
        var json = JsonSerializer.Serialize(asset, AtlasContracts.SerializerOptions);
        Assert.Equal(kind, JsonSerializer.Deserialize<Asset>(json, AtlasContracts.SerializerOptions)!.Kind);
    }

    [Theory]
    [InlineData("integration", "application", "{}")]
    [InlineData("business-process", "server", "{}")]
    [InlineData("integration", "id", "\"catalogue-id\"")]
    [InlineData("business-process", "lifecycle", "\"active\"")]
    [InlineData("integration", "criticality", "\"high\"")]
    public void New_kinds_do_not_relax_observation_boundaries(string kind, string property, string value)
    {
        var node = JsonNode.Parse($$"""
            { "contractVersion": "1", "kind": "discovery-observation",
              "source": { "agentId": "inventory", "method": "agentless" },
              "observedAt": "2026-09-17T00:00:00Z",
              "observations": [{ "observedId": "source:123", "kind": "{{kind}}", "name": "Orders",
                "{{property}}": {{value}} }] }
            """);
        Assert.False(JsonSchemaTestHelpers.Evaluate(TestSchemas.Load("discovery-observation.schema.json"), node).IsValid);
    }

    private static void AssertValid<T>(string schema, T value) =>
        Assert.True(JsonSchemaTestHelpers.Evaluate(TestSchemas.Load(schema),
            JsonNode.Parse(JsonSerializer.Serialize(value, AtlasContracts.SerializerOptions))).IsValid);
}
