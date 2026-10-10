using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

public sealed class InUsePeriodConformanceTests
{
    private static readonly InUsePeriod Period = new(
        new DateOnly(2027, 1, 1), new DateOnly(2029, 1, 1), new DateOnly(2027, 2, 15), new DateOnly(2029, 3, 1));

    private static bool SchemaValid(string schema, JsonNode node) =>
        TestSchemas.Load(schema).Evaluate(JsonSerializer.SerializeToElement(node),
            new EvaluationOptions { RequireFormatValidation = true }).IsValid;

    private static void RoundTrip<T>(T value, string schema)
    {
        var json = JsonSerializer.Serialize(value, AtlasContracts.SerializerOptions);
        Assert.True(SchemaValid(schema, JsonNode.Parse(json)!));
        var restored = JsonSerializer.Deserialize<T>(json, AtlasContracts.SerializerOptions);
        Assert.Equal(json, JsonSerializer.Serialize(restored, AtlasContracts.SerializerOptions));
    }

    [Fact]
    public void Period_round_trips_in_export_import_and_portable_bundle()
    {
        var asset = new Asset("app-1", AssetKind.Application, "Billing", Lifecycle.Active, InUsePeriod: Period);
        var landscape = new LandscapeDocument([asset]);
        RoundTrip(landscape, "landscape.schema.json");
        RoundTrip(new ImportBundle([new ImportAsset(AssetKind.Application, "Billing", Lifecycle.Active,
            ExternalId: "source-1", InUsePeriod: Period)]), "import.schema.json");
        var bundle = JsonNode.Parse(File.ReadAllText(Path.Combine(TestSchemas.SampleDir, "bundle.sample.json")))!;
        bundle["landscape"]!["assets"]![0]!["inUsePeriod"] = JsonSerializer.SerializeToNode(Period, AtlasContracts.SerializerOptions);
        RoundTrip(bundle.Deserialize<AtlasBundle>(AtlasContracts.SerializerOptions)!, "bundle.schema.json");
        Assert.Equal(Period, JsonSerializer.Deserialize<LandscapeDocument>(
            JsonSerializer.Serialize(landscape, AtlasContracts.SerializerOptions), AtlasContracts.SerializerOptions)!.Assets[0].InUsePeriod);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"plannedEnd\":\"2029-01-01\"}")]
    [InlineData("{\"actualStart\":null,\"actualEnd\":\"2028-02-29\"}")]
    public void Partial_empty_and_null_periods_are_valid(string period)
    {
        var asset = JsonNode.Parse("{\"id\":\"a\",\"kind\":\"system\",\"name\":\"A\",\"lifecycle\":\"draft\"}")!;
        Assert.True(SchemaValid("asset.schema.json", asset));
        asset["inUsePeriod"] = JsonNode.Parse(period);
        Assert.True(SchemaValid("asset.schema.json", asset));
        Assert.Empty(InUsePeriodValidation.Validate(asset.Deserialize<Asset>(AtlasContracts.SerializerOptions)!.InUsePeriod));
    }

    [Theory]
    [InlineData("2027-02-29")]
    [InlineData("2027-13-01")]
    [InlineData("2027-01-01T00:00:00Z")]
    [InlineData("2027-1-1")]
    [InlineData("0000-01-01")]
    [InlineData("1900-02-29")]
    [InlineData("2027-04-31")]
    [InlineData("10000-01-01")]
    public void Invalid_dates_are_rejected_by_DateOnly(string date)
    {
        var asset = JsonSerializer.SerializeToNode(new Asset("a", AssetKind.System, "A", Lifecycle.Active), AtlasContracts.SerializerOptions)!;
        asset["inUsePeriod"] = new JsonObject { ["plannedStart"] = date };
        Assert.False(SchemaValid("asset.schema.json", asset));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<InUsePeriod>(
            JsonSerializer.Serialize(new { plannedStart = date }), AtlasContracts.SerializerOptions));
    }

    [Fact]
    public void Unknown_period_fields_are_rejected_before_deserialization()
    {
        var asset = JsonSerializer.SerializeToNode(new Asset("a", AssetKind.System, "A", Lifecycle.Active, InUsePeriod: Period), AtlasContracts.SerializerOptions)!;
        asset["inUsePeriod"]!["snapshot"] = "hidden";
        Assert.False(SchemaValid("asset.schema.json", asset));
    }

    [Fact]
    public void Embedded_target_assets_preserve_all_dates_and_reject_unknown_period_fields()
    {
        var target = JsonNode.Parse(File.ReadAllText(Path.Combine(TestSchemas.SampleDir, "target-architecture.sample.json")))!;
        var asset = target["versions"]![0]!["assetChanges"]![0]!["asset"]!;
        asset["inUsePeriod"] = JsonSerializer.SerializeToNode(Period, AtlasContracts.SerializerOptions);
        var restored = target.Deserialize<TargetArchitectureDocument>(AtlasContracts.SerializerOptions)!;
        RoundTrip(restored, "target-architecture.schema.json");
        Assert.Equal(Period, restored.Versions[0].AssetChanges[0].Asset!.InUsePeriod);
        asset["inUsePeriod"]!["unknown"] = true;
        Assert.False(SchemaValid("target-architecture.schema.json", target));
    }

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2000-02-29")]
    [InlineData("9999-12-31")]
    public void Calendar_extremes_and_leap_dates_are_valid_on_every_boundary(string date)
    {
        foreach (var field in new[] { "plannedStart", "plannedEnd", "actualStart", "actualEnd" })
        {
            var asset = JsonSerializer.SerializeToNode(new Asset("a", AssetKind.System, "A", Lifecycle.Active), AtlasContracts.SerializerOptions)!;
            asset["inUsePeriod"] = new JsonObject { [field] = date };
            Assert.True(SchemaValid("asset.schema.json", asset));
            RoundTrip(asset.Deserialize<Asset>(AtlasContracts.SerializerOptions)!, "asset.schema.json");
        }
    }

    [Fact]
    public void Each_pair_has_ordering_but_plan_actual_and_lifecycle_are_independent()
    {
        Assert.Equal(2, InUsePeriodValidation.Validate(new InUsePeriod(
            new DateOnly(2029, 1, 1), new DateOnly(2027, 1, 1),
            new DateOnly(2029, 1, 1), new DateOnly(2027, 1, 1))).Length);
        Assert.Empty(InUsePeriodValidation.Validate(new InUsePeriod(
            new DateOnly(2029, 1, 1), new DateOnly(2029, 1, 1),
            new DateOnly(2027, 1, 1), new DateOnly(2028, 1, 1))));
        Assert.Empty(InUsePeriodValidation.Validate(null));
        RoundTrip(new LandscapeDocument([new Asset("a", AssetKind.System, "A", Lifecycle.Retired, InUsePeriod: Period)]), "landscape.schema.json");
    }

    [Fact]
    public void All_asset_kinds_carry_the_same_period_and_import_rejects_extra_fields()
    {
        foreach (var kind in Enum.GetValues<AssetKind>())
        {
            RoundTrip(new LandscapeDocument([new Asset("a", kind, "A", Lifecycle.Active, InUsePeriod: Period)]), "landscape.schema.json");
            RoundTrip(new ImportBundle([new ImportAsset(kind, "A", Lifecycle.Active, ExternalId: "a", InUsePeriod: Period)]), "import.schema.json");
        }
        var import = JsonSerializer.SerializeToNode(new ImportBundle([new ImportAsset(AssetKind.System,
            "A", Lifecycle.Active, ExternalId: "a", InUsePeriod: Period)]), AtlasContracts.SerializerOptions)!;
        import["assets"]![0]!["inUsePeriod"]!["unknown"] = true;
        Assert.False(SchemaValid("import.schema.json", import));
    }

    [Theory]
    [InlineData("landscape-digest.valid.json", "landscape-digest.schema.json")]
    [InlineData("landscape-digest-v2.valid.json", "v2/landscape-digest.schema.json")]
    public void Neither_digest_version_accepts_period_dates(string sample, string schema)
    {
        var digest = JsonNode.Parse(File.ReadAllText(Path.Combine(TestSchemas.SampleDir, sample)))!;
        Assert.True(SchemaValid(schema, digest));
        digest["items"]![0]!["inUsePeriod"] = JsonSerializer.SerializeToNode(Period, AtlasContracts.SerializerOptions);
        Assert.False(SchemaValid(schema, digest));
    }
}
