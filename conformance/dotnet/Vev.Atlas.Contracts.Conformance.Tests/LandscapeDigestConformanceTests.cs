using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance kit for the landscape share digest (v1): proves the published schema accepts a
/// valid digest and the .NET SDK serialises to a schema-valid shape, that forbidden fields are
/// rejected by the schema (minimization), that the detached RSA signature over the RFC 8785
/// canonicalization verifies and that a tampered digest fails, and that replayed sequences are
/// rejected per source.
/// </summary>
public sealed class LandscapeDigestConformanceTests
{
    private static readonly string SampleDir = TestSchemas.SampleDir;

    private static readonly JsonSchema DigestSchema = TestSchemas.Load("landscape-digest.schema.json");

    private static EvaluationResults Evaluate(JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(DigestSchema, instance);

    // ---- schema: positive ----

    [Fact]
    public void Sample_landscape_digest_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape-digest.valid.json")));

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_serialises_to_a_schema_valid_digest()
    {
        // The SDK is the contract's hand: what it writes must satisfy the published schema.
        var digest = new LandscapeDigest(
            DigestId: "0195f2a4-7c3e-7000-8000-000000000043",
            GeneratedAt: new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero),
            SourceInstanceId: "atlas-instance-conformance",
            Sequence: 1,
            Scope: new LandscapeDigestScope(
                Kinds: [DigestKind.System, DigestKind.Application, DigestKind.Platform, DigestKind.Vendor],
                Tags: [new Tag("shared", "true")]),
            Items:
            [
                new LandscapeDigestItem(DigestKind.System, "Payments platform", Lifecycle.Active, IntegrationCount: 5),
                new LandscapeDigestItem(DigestKind.Application, "Checkout service", Lifecycle.Active,
                    Vendor: "in-house", PlannedChange: PlannedChange.Replace, IntegrationCount: 3),
                new LandscapeDigestItem(DigestKind.Platform, "EU production compute", Lifecycle.Active, Vendor: "Example Cloud"),
                new LandscapeDigestItem(DigestKind.Vendor, "Example Corp", Lifecycle.Active)
            ]);

        var json = JsonSerializer.Serialize(digest, AtlasContracts.SerializerOptions);
        var instance = JsonNode.Parse(json);

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    // ---- schema: negative (minimization and vocabulary) ----

    [Fact]
    public void Digest_with_forbidden_fields_is_rejected()
    {
        // The fixture carries a description and a server hostname — both explicitly excluded from
        // the digest. additionalProperties: false is the enforcement, not convention.
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape-digest.forbidden-field.json")));

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Digest_with_unknown_root_property_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "digestId": "d-1", "generatedAt": "2026-09-28T12:00:00Z",
              "sourceInstanceId": "s-1", "sequence": 1,
              "scope": { "kinds": ["system"], "tags": [] }, "items": [],
              "generator": { "name": "Atlas Community" } }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Digest_with_a_catalogue_kind_instead_of_a_digest_kind_is_rejected()
    {
        // "server" is a catalogue AssetKind but not a digest kind: the digest folds it into "platform".
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "digestId": "d-1", "generatedAt": "2026-09-28T12:00:00Z",
              "sourceInstanceId": "s-1", "sequence": 1,
              "scope": { "kinds": ["platform"], "tags": [] },
              "items": [ { "kind": "server", "name": "X", "lifecycle": "active" } ] }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Digest_with_an_invalid_planned_change_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "digestId": "d-1", "generatedAt": "2026-09-28T12:00:00Z",
              "sourceInstanceId": "s-1", "sequence": 1,
              "scope": { "kinds": ["application"], "tags": [] },
              "items": [ { "kind": "application", "name": "X", "lifecycle": "active", "plannedChange": "migrate" } ] }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Digest_with_a_zero_sequence_is_rejected()
    {
        // The first digest is sequence 1; 0 is not a valid sequence.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "digestId": "d-1", "generatedAt": "2026-09-28T12:00:00Z",
              "sourceInstanceId": "s-1", "sequence": 0,
              "scope": { "kinds": [], "tags": [] }, "items": [] }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Digest_item_kind_outside_the_declared_scope_is_rejected()
    {
        // JSON Schema cannot express "every item kind is a member of scope.kinds"; the conformance
        // kit checks the invariant the consumer relies on.
        var instance = JsonNode.Parse(
            """
            { "contractVersion": "1", "digestId": "d-1", "generatedAt": "2026-09-28T12:00:00Z",
              "sourceInstanceId": "s-1", "sequence": 1,
              "scope": { "kinds": ["application"], "tags": [] },
              "items": [ { "kind": "vendor", "name": "X", "lifecycle": "active" } ] }
            """);

        Assert.True(Evaluate(instance).IsValid);
        Assert.False(ScopeCoversAllItems(instance!));
    }

    // ---- detached signature (RSA-SHA-256 over RFC 8785) ----

    [Fact]
    public void Signed_digest_with_a_valid_signature_verifies()
    {
        var wrapper = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape-digest.signed.valid.json")))!;
        var digest = wrapper["digest"]!;
        var signature = wrapper["signature"]!;

        Assert.Equal(DigestSignature.Algorithm, (string?)signature["algorithm"]);
        Assert.Equal(DigestSignature.TestKeyId, (string?)signature["keyId"]);

        var publicKey = DigestSignature.FromBase64Url((string)signature["publicKey"]!);
        var value = DigestSignature.FromBase64Url((string)signature["value"]!);
        var canonical = Jcs.CanonicalizeUtf8(digest);

        Assert.True(DigestSignature.Verify(publicKey, value, canonical), "The detached signature must verify over the RFC 8785 canonicalization of the digest.");
        Assert.True(Evaluate(digest).IsValid, Describe(Evaluate(digest)));
    }

    [Fact]
    public void Tampered_signed_digest_fails_verification()
    {
        // Same signature as the valid fixture, but the digest payload was modified after signing.
        var wrapper = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape-digest.signed.tampered.json")))!;
        var digest = wrapper["digest"]!;
        var signature = wrapper["signature"]!;

        var publicKey = DigestSignature.FromBase64Url((string)signature["publicKey"]!);
        var value = DigestSignature.FromBase64Url((string)signature["value"]!);
        var canonical = Jcs.CanonicalizeUtf8(digest);

        Assert.False(DigestSignature.Verify(publicKey, value, canonical));
    }

    // ---- replay protection (monotonic sequence per source) ----

    [Fact]
    public void Replayed_sequence_is_rejected_and_a_higher_sequence_is_accepted()
    {
        var digest = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape-digest.valid.json")))!;
        var source = (string)digest["sourceInstanceId"]!;
        var tracker = new SequenceTracker();

        Assert.True(tracker.TryAccept(source, 1));
        Assert.False(tracker.TryAccept(source, 1), "A replayed (equal) sequence must be rejected.");
        Assert.True(tracker.TryAccept(source, 2), "A strictly higher sequence must be accepted.");
        Assert.False(tracker.TryAccept(source, 2), "A replayed (equal) sequence must be rejected.");
        Assert.False(tracker.TryAccept(source, 1), "A lower sequence must be rejected.");
    }

    [Fact]
    public void Sequence_state_is_scoped_per_source_instance()
    {
        var tracker = new SequenceTracker();

        Assert.True(tracker.TryAccept("source-a", 1));
        Assert.True(tracker.TryAccept("source-b", 1), "A different source starts its own monotonic line.");
        Assert.False(tracker.TryAccept("source-a", 1));
    }

    // ---- helpers ----

    private static bool ScopeCoversAllItems(JsonNode digest)
    {
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in (JsonArray)digest["scope"]!["kinds"]!)
        {
            kinds.Add((string)kind!);
        }

        foreach (var item in (JsonArray)digest["items"]!)
        {
            if (!kinds.Contains((string)item!["kind"]!))
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(EvaluationResults results) => JsonSchemaTestHelpers.Describe(results);
}
