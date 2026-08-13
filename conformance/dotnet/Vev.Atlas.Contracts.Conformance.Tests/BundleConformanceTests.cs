using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance for the portable Atlas bundle (#8): the hosted &lt;-&gt; self-hosted migration package
/// layered on top of the landscape export. Proves the published schema accepts a valid bundle and
/// rejects one that smuggles non-portable state, that the .NET SDK serialises to a schema-valid
/// shape and round-trips, and that the SDK's bundle-level invariants (manifest honesty and internal
/// reference resolution) behave (handbook 11 §3, 11 §7, 12 §Phase C, ADR 0003).
/// </summary>
public sealed class BundleConformanceTests
{
    private static readonly string SampleDir = TestSchemas.SampleDir;

    private static readonly JsonSchema BundleSchema = TestSchemas.Load("bundle.schema.json");

    // A real lower-case SHA-256 hex digest (of an arbitrary string) so fixtures satisfy the pattern.
    private const string SampleDigest = "0b0b242f42d62b4e8980598044ad042e914476ae55872619bec89aadff32fd17";

    private static EvaluationResults Evaluate(JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(BundleSchema, instance);

    private static AtlasBundle BuildSdkBundle()
    {
        var landscape = new LandscapeDocument(
            Assets:
            [
                new Asset("app-1", AssetKind.Application, "Billing", Lifecycle.Active,
                    Application: new ApplicationDetails(Version: "1.0.0", Vendor: "in-house")),
                new Asset("srv-1", AssetKind.Server, "billing-01", Lifecycle.Active,
                    Server: new ServerDetails(Hostname: "billing-01", Environment: "production", OperatingSystem: "RHEL 9"))
            ],
            Relationships: [new Relationship("r1", "app-1", "srv-1", RelationshipType.RunsOn)]);

        return new AtlasBundle(
            Manifest: new BundleManifest(new LandscapeCounts(2, 1), DiagramCount: 1, AttachmentCount: 1),
            Landscape: landscape,
            CreatedAt: DateTimeOffset.UtcNow,
            Generator: new Generator("Atlas Community", "0.1.0"),
            Compatibility: new BundleCompatibility(ProducerAtlasVersion: "2026.8.0"),
            Workspace: new WorkspaceMetadata("Billing", Slug: "billing", Locale: "da-DK"),
            Diagrams:
            [
                new Diagram("dg-1", "Overview", DiagramFormat.AtlasLayout,
                    Nodes: [new DiagramNode("app-1", 0, 0), new DiagramNode("srv-1", 0, 120, "billing-01")],
                    Edges: [new DiagramEdge("r1")],
                    RenderRef: "att-1")
            ],
            Attachments:
            [
                new AttachmentManifestEntry("att-1", "overview.png", "image/png", 1024,
                    new Digest(DigestAlgorithm.Sha256, SampleDigest), AttachedToRef: "dg-1")
            ],
            Excluded: [ExcludedCategory.Secrets, ExcludedCategory.LiveCredentials]);
    }

    // ---- schema conformance ----

    [Fact]
    public void Sample_bundle_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "bundle.sample.json")));

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_bundle_serialises_to_a_schema_valid_document()
    {
        var json = JsonSerializer.Serialize(BuildSdkBundle(), AtlasContracts.SerializerOptions);

        var results = Evaluate(JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Bundle_round_trips_through_the_sdk()
    {
        var bundle = BuildSdkBundle();

        var json1 = JsonSerializer.Serialize(bundle, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<AtlasBundle>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(JsonNode.Parse(json2)).IsValid);
    }

    [Fact]
    public void Bundle_missing_the_landscape_core_is_rejected()
    {
        // The bundle is layered ON TOP of the landscape; without it there is nothing to be portable.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "atlas-bundle",
              "manifest": { "landscape": { "assetCount": 0, "relationshipCount": 0 } } }
            """);

        Assert.False(Evaluate(instance).IsValid);
    }

    [Fact]
    public void Attachment_with_inline_bytes_is_rejected()
    {
        // The attachment section is a MANIFEST — references + digests, never inline (possibly secret)
        // bytes. additionalProperties:false forbids a stray "bytes" field.
        var instance = JsonNode.Parse(
            $$"""
            { "contractVersion": "1", "kind": "atlas-bundle",
              "manifest": { "landscape": { "assetCount": 0, "relationshipCount": 0 }, "attachmentCount": 1 },
              "landscape": { "contractVersion": "1", "assets": [] },
              "attachments": [
                { "id": "a1", "name": "x.png", "mediaType": "image/png", "byteSize": 3,
                  "digest": { "algorithm": "sha-256", "value": "{{SampleDigest}}" },
                  "bytes": "AAECAw==" } ] }
            """);

        Assert.False(Evaluate(instance).IsValid);
    }

    [Fact]
    public void Bundle_smuggling_entitlement_state_is_rejected()
    {
        // A non-portable control-plane concept must not smuggle itself in as an unknown top-level key.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "atlas-bundle",
              "manifest": { "landscape": { "assetCount": 0, "relationshipCount": 0 } },
              "landscape": { "contractVersion": "1", "assets": [] },
              "entitlements": { "plan": "enterprise" } }
            """);

        Assert.False(Evaluate(instance).IsValid);
    }

    [Fact]
    public void Attachment_digest_with_a_non_sha256_value_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "kind": "atlas-bundle",
              "manifest": { "landscape": { "assetCount": 0, "relationshipCount": 0 }, "attachmentCount": 1 },
              "landscape": { "contractVersion": "1", "assets": [] },
              "attachments": [
                { "id": "a1", "name": "x.png", "mediaType": "image/png", "byteSize": 3,
                  "digest": { "algorithm": "sha-256", "value": "not-a-digest" } } ] }
            """);

        Assert.False(Evaluate(instance).IsValid);
    }

    // ---- bundle-level invariants (SDK helpers) ----

    [Fact]
    public void Sample_bundle_has_an_honest_manifest_and_no_unresolved_references()
    {
        var bundle = JsonSerializer.Deserialize<AtlasBundle>(
            File.ReadAllText(Path.Combine(SampleDir, "bundle.sample.json")), AtlasContracts.SerializerOptions);

        Assert.Empty(bundle!.ManifestErrors());
        Assert.Empty(bundle.UnresolvedReferences());
    }

    [Fact]
    public void Manifest_count_mismatch_is_detected()
    {
        var bundle = BuildSdkBundle() with
        {
            Manifest = new BundleManifest(new LandscapeCounts(2, 1), DiagramCount: 2, AttachmentCount: 1)
        };

        var error = Assert.Single(bundle.ManifestErrors());
        Assert.Contains("diagramCount", error);
    }

    [Fact]
    public void Diagram_node_pointing_at_a_missing_asset_is_detected()
    {
        var bundle = BuildSdkBundle() with
        {
            Diagrams =
            [
                new Diagram("dg-1", "Overview", DiagramFormat.AtlasLayout,
                    Nodes: [new DiagramNode("does-not-exist", 0, 0)])
            ],
            Attachments = [],
            Manifest = new BundleManifest(new LandscapeCounts(2, 1), DiagramCount: 1, AttachmentCount: 0)
        };

        Assert.Equal("does-not-exist", Assert.Single(bundle.UnresolvedReferences()));
    }

    private static string Describe(EvaluationResults results) => JsonSchemaTestHelpers.Describe(results);
}
