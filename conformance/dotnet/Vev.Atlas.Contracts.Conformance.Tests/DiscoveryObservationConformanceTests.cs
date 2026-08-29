using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance for the discovery ingestion contract (atlas-contracts#9): the wire format a scanner
/// emits into Atlas. Proves the published schema accepts a valid batch and the SDK-produced shape,
/// that a batch round-trips through the .NET SDK, and — the boundary that protects the moat — that an
/// observation may not smuggle catalogue/analysis concepts (no Atlas 'id', no 'lifecycle', no
/// 'criticality'). Reconciliation itself is private and is not exercised here (handbook 11 §3).
/// </summary>
public sealed class DiscoveryObservationConformanceTests
{
    private static readonly string SampleDir = TestSchemas.SampleDir;

    private static readonly JsonSchema DiscoverySchema = TestSchemas.Load("discovery-observation.schema.json");

    private static EvaluationResults Evaluate(JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(DiscoverySchema, instance);

    private static DiscoveryObservationBatch SampleBatch() => new(
        Source: new ObservationSource("agent-eu-north-1-01", CollectionMethod.Agent, Version: "0.1.0"),
        ObservedAt: DateTimeOffset.Parse("2026-08-10T07:30:00Z"),
        Observations:
        [
            new Observation("host:5f3c9a2e-checkout-prod-01", AssetKind.Server, "checkout-prod-01",
                Fingerprint:
                [
                    new FingerprintAttribute("machine-id", "5f3c9a2e8b7d4c1f9a0e2b6d3c4f5a1b"),
                    new FingerprintAttribute("cloud-instance-id", "i-0abcd1234ef567890")
                ],
                Ports: [new ObservedPort(443, PortProtocol.Tcp), new ObservedPort(22, PortProtocol.Tcp, "10.0.4.11")],
                Processes: [new ObservedProcess("nginx", 443), new ObservedProcess("checkout-api", 8080)],
                Packages: [new ObservedPackage("openssl", "3.0.13"), new ObservedPackage("dotnet-runtime", "8.0.7")],
                Server: new ServerDetails(Hostname: "checkout-prod-01.vev.internal", Environment: "production", OperatingSystem: "Ubuntu 24.04 LTS")),
            new Observation("proc:checkout-api@checkout-prod-01", AssetKind.Application, "Checkout API",
                Application: new ApplicationDetails(Version: "4.2.1", Vendor: "in-house"))
        ]);

    [Fact]
    public void Sample_batch_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "discovery-observation.sample.json")));

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_batch_serialises_to_a_schema_valid_document()
    {
        var json = JsonSerializer.Serialize(SampleBatch(), AtlasContracts.SerializerOptions);

        var results = Evaluate(JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Batch_round_trips_through_the_sdk()
    {
        var json1 = JsonSerializer.Serialize(SampleBatch(), AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<DiscoveryObservationBatch>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(JsonNode.Parse(json2)).IsValid);
    }

    [Fact]
    public void Data_layer_observations_carry_dataset_and_column_facts_and_conform()
    {
        // Database schema introspection (atlas-enterprise#16) emits the data layer through this same
        // contract: data-areas, datasets and columns as observations carrying held facts (physical name,
        // data type, nullability) — never analysis. Proves the schema accepts them and the SDK round-trips.
        var batch = new DiscoveryObservationBatch(
            Source: new ObservationSource("db-introspect-01", CollectionMethod.Agentless, Version: "0.1.0"),
            ObservedAt: DateTimeOffset.Parse("2026-08-10T07:30:00Z"),
            Observations:
            [
                new Observation("db:orders.public", AssetKind.DataArea, "orders",
                    DataArea: new DataAreaDetails(Realisation: "relational-database")),
                new Observation("db:orders.public.customers", AssetKind.Dataset, "customers",
                    Dataset: new DatasetDetails(PhysicalName: "public.customers", Owner: "CRM team")),
                new Observation("db:orders.public.customers.customer_id", AssetKind.Column, "customer_id",
                    Column: new ColumnDetails(DataType: "uuid", Nullable: false))
            ]);

        var json1 = JsonSerializer.Serialize(batch, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<DiscoveryObservationBatch>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(JsonNode.Parse(json2)).IsValid, Describe(Evaluate(JsonNode.Parse(json2))));
    }

    [Fact]
    public void Column_observation_may_not_smuggle_a_classification_verdict()
    {
        // The data layer stays facts-only: a sensitivity/classification verdict is paid Atlas core and
        // must not ride in on a column observation.
        Assert.False(Evaluate(Observations("""
            { "observedId": "c1", "kind": "column", "name": "ssn", "column": { "dataType": "char(11)" }, "classification": "pii" }
            """)).IsValid);
    }

    [Fact]
    public void Observed_relationships_conform_and_round_trip_through_the_sdk()
    {
        // A scanner reports relationships as facts by observedId — a foreign key as joins-on, containment
        // as part-of. The reconciler (private) resolves the endpoints to catalogue assets; the contract
        // only carries the observed edges.
        var batch = new DiscoveryObservationBatch(
            Source: new ObservationSource("db-introspect-01", CollectionMethod.Agentless),
            ObservedAt: DateTimeOffset.Parse("2026-08-10T07:30:00Z"),
            Observations:
            [
                new Observation("ds", AssetKind.Dataset, "customers", Dataset: new DatasetDetails(PhysicalName: "public.customers")),
                new Observation("c1", AssetKind.Column, "customer_id", Column: new ColumnDetails(DataType: "uuid", Nullable: false)),
                new Observation("c2", AssetKind.Column, "org_id", Column: new ColumnDetails(DataType: "uuid", Nullable: false))
            ],
            Relationships:
            [
                new ObservedRelationship("c1", "ds", RelationshipType.PartOf),
                new ObservedRelationship("c2", "c1", RelationshipType.JoinsOn, Description: "org_id references customer_id")
            ]);

        var json1 = JsonSerializer.Serialize(batch, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<DiscoveryObservationBatch>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(JsonNode.Parse(json2)).IsValid, Describe(Evaluate(JsonNode.Parse(json2))));
    }

    [Fact]
    public void Observed_relationship_with_an_unknown_type_is_rejected()
    {
        // Only the held relationship vocabulary is allowed; a measured/derived integration is paid-core
        // analysis and must not smuggle itself into the public discovery contract.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "discovery-observation",
              "source": { "agentId": "a", "method": "agentless" },
              "observedAt": "2026-08-10T07:30:00Z", "observations": [],
              "relationships": [ { "fromObservedId": "a", "toObservedId": "b", "type": "measured-integration" } ] }
            """);

        Assert.False(Evaluate(instance).IsValid);
    }

    [Fact]
    public void Empty_agentless_batch_is_valid()
    {
        // A scan that saw nothing new is still a well-formed batch.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "discovery-observation",
              "source": { "agentId": "collector-1", "method": "agentless" },
              "observedAt": "2026-08-10T07:30:00Z", "observations": [] }
            """);

        Assert.True(Evaluate(instance).IsValid, Describe(Evaluate(instance)));
    }

    [Fact]
    public void Observation_carrying_an_atlas_id_is_rejected()
    {
        // The reconciler decides catalogue identity — an observation must not assert an Atlas id.
        Assert.False(Evaluate(Observations("""
            { "observedId": "o1", "kind": "server", "name": "x", "id": "srv-1" }
            """)).IsValid);
    }

    [Fact]
    public void Observation_carrying_a_lifecycle_is_rejected()
    {
        // Lifecycle is catalogue state the reconciler assigns, not a fact a scanner observes.
        Assert.False(Evaluate(Observations("""
            { "observedId": "o1", "kind": "application", "name": "x", "lifecycle": "active" }
            """)).IsValid);
    }

    [Fact]
    public void Observation_with_an_analysis_property_is_rejected()
    {
        // A paid-core concept (criticality) must not smuggle itself into the public contract.
        Assert.False(Evaluate(Observations("""
            { "observedId": "o1", "kind": "application", "name": "x", "criticality": "high" }
            """)).IsValid);
    }

    [Fact]
    public void Batch_with_an_unknown_collection_method_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "discovery-observation",
              "source": { "agentId": "a", "method": "carrier-pigeon" },
              "observedAt": "2026-08-10T07:30:00Z", "observations": [] }
            """);

        Assert.False(Evaluate(instance).IsValid);
    }

    [Fact]
    public void Fingerprint_attribute_without_a_value_is_rejected()
    {
        // Unlike a Tag, a fingerprint attribute must carry a value.
        Assert.False(Evaluate(Observations("""
            { "observedId": "o1", "kind": "server", "name": "x", "fingerprint": [ { "key": "mac" } ] }
            """)).IsValid);
    }

    private static JsonNode? Observations(string observationJson) => JsonNode.Parse(
        $$"""
        { "contractVersion": "1", "kind": "discovery-observation",
          "source": { "agentId": "a", "method": "agent" },
          "observedAt": "2026-08-10T07:30:00Z", "observations": [ {{observationJson}} ] }
        """);

    private static string Describe(EvaluationResults results) => JsonSchemaTestHelpers.Describe(results);
}
