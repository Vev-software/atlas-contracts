using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

public sealed class LandscapeDigestV2ConformanceTests
{
    private static readonly JsonSchema Schema = TestSchemas.Load("v2/landscape-digest.schema.json");
    private static JsonNode Read(string name) => JsonNode.Parse(File.ReadAllText(
        Path.Combine(TestSchemas.SampleDir, $"landscape-digest-v2.{name}.json")))!;
    private static bool SchemaValid(JsonNode node) => JsonSchemaTestHelpers.Evaluate(Schema, node).IsValid;
    private static LandscapeDigestV2 Deserialize(JsonNode node) =>
        node.Deserialize<LandscapeDigestV2>(AtlasContracts.SerializerOptions)!;

    [Theory]
    [InlineData("valid")]
    [InlineData("without-relations")]
    [InlineData("omitted-consent")]
    [InlineData("stage2")]
    public void Positive_fixtures_pass_schema_and_semantic_validation(string name)
    {
        var node = Read(name);
        Assert.True(SchemaValid(node));
        Assert.Empty(LandscapeDigestV2Validation.Validate(Deserialize(node)));
    }

    [Theory]
    [InlineData("unknown-kind")]
    [InlineData("duplicate-edge")]
    [InlineData("extra-edge-field")]
    [InlineData("relations-off")]
    [InlineData("relations-omitted-consent")]
    [InlineData("missing-ref")]
    [InlineData("empty-ref")]
    [InlineData("long-ref")]
    [InlineData("invalid-layer")]
    public void Negative_fixtures_fail_schema_validation(string name) => Assert.False(SchemaValid(Read(name)));

    [Theory]
    [InlineData("dangling-ref", "dangling-ref")]
    [InlineData("self-edge", "self-edge")]
    [InlineData("reverse-integration", "duplicate-edge")]
    [InlineData("duplicate-ref", "duplicate-ref")]
    [InlineData("wrong-vendor-target", "invalid-relation-endpoints")]
    [InlineData("wrong-platform-target", "invalid-relation-endpoints")]
    [InlineData("outside-scope", "item-outside-scope")]
    public void Schema_valid_negative_fixtures_fail_semantic_validation(string name, string error)
    {
        var node = Read(name);
        Assert.True(SchemaValid(node));
        Assert.Contains(error, LandscapeDigestV2Validation.Validate(Deserialize(node)));
    }

    [Fact]
    public void Sdk_writes_both_relations_and_default_off_shapes()
    {
        var digest = Deserialize(Read("valid"));
        var node = JsonNode.Parse(JsonSerializer.Serialize(digest, AtlasContracts.SerializerOptions))!;
        Assert.True(SchemaValid(node));
        Assert.Empty(LandscapeDigestV2Validation.Validate(Deserialize(node)));
        var without = digest with { Scope = new LandscapeDigestScopeV2(digest.Scope.Kinds), Relations = null };
        node = JsonNode.Parse(JsonSerializer.Serialize(without, AtlasContracts.SerializerOptions))!;
        Assert.False(without.Scope.Relations);
        Assert.False(((JsonObject)node).ContainsKey("relations"));
        Assert.True(SchemaValid(node));
    }

    [Fact]
    public void Ref_maximum_length_is_accepted_and_empty_relations_still_require_opt_in()
    {
        var node = Read("without-relations");
        node["items"]![0]!["ref"] = new string('x', 64);
        Assert.True(SchemaValid(node));
        Assert.Empty(LandscapeDigestV2Validation.Validate(Deserialize(node)));
        node["relations"] = new JsonArray();
        Assert.False(SchemaValid(node));
        node["scope"]!["relations"] = true;
        Assert.True(SchemaValid(node));
    }

    [Fact]
    public void Relations_limit_is_inclusive_and_excess_is_rejected()
    {
        var node = Read("valid");
        var items = (JsonArray)node["items"]!;
        var edges = new JsonArray();
        for (var i = 0; i < 20000; i++)
        {
            var reference = $"p{i}";
            items.Add(new JsonObject { ["ref"] = reference, ["kind"] = "platform", ["name"] = "Example", ["lifecycle"] = "active" });
            edges.Add(new JsonObject { ["from"] = "c1", ["to"] = reference, ["kind"] = "runsOn" });
        }

        node["relations"] = edges;
        Assert.True(SchemaValid(node));
        Assert.Empty(LandscapeDigestV2Validation.Validate(Deserialize(node)));
        items.Add(new JsonObject { ["ref"] = "overflow", ["kind"] = "platform", ["name"] = "Example", ["lifecycle"] = "active" });
        edges.Add(new JsonObject { ["from"] = "c1", ["to"] = "overflow", ["kind"] = "runsOn" });
        Assert.False(SchemaValid(node));
        Assert.Contains("too-many-relations", LandscapeDigestV2Validation.Validate(Deserialize(node)));
    }

    [Theory]
    [InlineData("label")]
    [InlineData("direction")]
    [InlineData("protocol")]
    [InlineData("port")]
    [InlineData("host")]
    [InlineData("url")]
    [InlineData("dataDescription")]
    [InlineData("dataCount")]
    public void Every_extra_edge_field_is_rejected(string field)
    {
        var node = Read("valid");
        node["relations"]![0]![field] = "forbidden";
        Assert.False(SchemaValid(node));
    }

    [Theory]
    [InlineData("signed.valid", true)]
    [InlineData("signed.tampered", false)]
    public void Detached_signature_vectors_verify_only_original_payload(string name, bool expected)
    {
        var wrapper = Read(name);
        Assert.True(SchemaValid(wrapper["digest"]!));
        Assert.Empty(LandscapeDigestV2Validation.Validate(Deserialize(wrapper["digest"]!)));
        Assert.Equal(DigestSignature.Algorithm, (string?)wrapper["signature"]!["algorithm"]);
        Assert.Equal(DigestSignature.TestKeyId, (string?)wrapper["signature"]!["keyId"]);
        Assert.Equal(expected, Verify(wrapper));
    }

    [Theory]
    [InlineData("edge")]
    [InlineData("ref")]
    [InlineData("layer")]
    [InlineData("scope")]
    public void Signature_covers_every_v2_addition(string field)
    {
        var wrapper = Read("signed.valid");
        var digest = wrapper["digest"]!;
        switch (field)
        {
            case "edge": digest["relations"]![0]!["from"] = "c4"; break;
            case "ref": digest["items"]![3]!["ref"] = "new-ref"; break;
            case "layer": digest["items"]![0]!["layer"] = "data"; break;
            case "scope": digest["scope"]!["relations"] = false; break;
        }

        Assert.False(Verify(wrapper));
    }

    [Fact]
    public void Jcs_preserves_unicode_and_sorts_keys_independent_of_transport_format()
    {
        var node = JsonNode.Parse("{\"z\":\"æ漢😀\",\"a\":1.0}")!;
        Assert.Equal("{\"a\":1,\"z\":\"æ漢😀\"}", Encoding.UTF8.GetString(DigestV2Jcs.CanonicalizeUtf8(node)));
        var wrapper = Read("signed.valid");
        var digest = (JsonObject)wrapper["digest"]!;
        var reordered = new JsonObject();
        foreach (var entry in digest.Reverse()) reordered[entry.Key] = entry.Value!.DeepClone();
        wrapper["digest"] = reordered;
        Assert.True(Verify(wrapper));
    }

    [Fact]
    public void Directed_edges_have_distinct_orientation_but_integrates_is_undirected()
    {
        var digest = Deserialize(Read("valid"));
        var forward = new LandscapeDigestRelation("c1", "c2", DigestRelationKind.RunsOn);
        var reverse = new LandscapeDigestRelation("c2", "c1", DigestRelationKind.RunsOn);
        Assert.DoesNotContain("duplicate-edge", LandscapeDigestV2Validation.Validate(digest with { Relations = [forward, reverse] }));
        Assert.Contains("duplicate-edge", LandscapeDigestV2Validation.Validate(Deserialize(Read("duplicate-edge"))));
    }

    private static bool Verify(JsonNode wrapper) => DigestSignature.Verify(
        DigestSignature.FromBase64Url((string)wrapper["signature"]!["publicKey"]!),
        DigestSignature.FromBase64Url((string)wrapper["signature"]!["value"]!),
        DigestV2Jcs.CanonicalizeUtf8(wrapper["digest"]!));
}
