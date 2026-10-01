using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance for the Target Architecture contract surface (#35): the named target, its versions,
/// the status vocabulary and the optional transition/gap/work-package shapes.
/// </summary>
public sealed class TargetArchitectureConformanceTests
{
    private static readonly string SampleDir = TestSchemas.SampleDir;

    private static readonly JsonSchema TargetArchitectureSchema = TestSchemas.Load("target-architecture.schema.json");

    private static EvaluationResults Evaluate(JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(TargetArchitectureSchema, instance);

    private static TargetArchitectureDocument SampleTarget() => new(
        Id: "ta-finance-modernisation",
        Name: "Finance Platform Modernisation",
        Scope: new TargetScope(TargetScopeKind.Domain, Ref: "finance", Name: "Finance"),
        Versions:
        [
            new TargetVersion(
                Id: "tv-2026-active",
                Name: "Current approved target",
                Sequence: 1,
                Status: TargetVersionStatus.Superseded,
                Rationale: "Initial modernisation plateau.",
                IntendedHorizon: "Q4 2026",
                EffectiveDate: new DateOnly(2026, 10, 1),
                AssetChanges:
                [
                    new TargetAssetMember(
                        Intent: ChangeIntent.Change,
                        AssetRef: "app-finance-legacy",
                        Asset: new Asset(
                            "app-finance-legacy",
                            AssetKind.Application,
                            "Finance Core",
                            Lifecycle.Active,
                            Description: "Legacy finance suite during migration.",
                            Application: new ApplicationDetails(Version: "2026.10", Vendor: "VEV")))
                ]),
            new TargetVersion(
                Id: "tv-2027-target",
                Name: "ERP target",
                Sequence: 2,
                Status: TargetVersionStatus.Active,
                Rationale: "Move the finance domain onto the supported ERP footprint.",
                IntendedHorizon: "2027",
                EffectiveDate: new DateOnly(2027, 3, 31),
                AssetChanges:
                [
                    new TargetAssetMember(
                        Intent: ChangeIntent.Retire,
                        AssetRef: "app-finance-legacy",
                        Rationale: "Vendor support ends in 2027."),
                    new TargetAssetMember(
                        Intent: ChangeIntent.Add,
                        Asset: new Asset(
                            "app-finance-erp",
                            AssetKind.Application,
                            "Finance ERP",
                            Lifecycle.Active,
                            Application: new ApplicationDetails(Vendor: "Contoso ERP", Version: "1.0")),
                        Rationale: "New supported ERP platform.")
                ],
                RelationshipChanges:
                [
                    new TargetRelationshipMember(
                        Intent: ChangeIntent.Add,
                        Relationship: new Relationship(
                            "rel-finance-erp-runs-on",
                            "app-finance-erp",
                            "srv-finance-prod",
                            RelationshipType.RunsOn),
                        Rationale: "Target hosting for the ERP runtime.")
                ])
        ],
        Description: "The estate-level target for retiring the legacy finance stack and moving onto a supported platform.",
        Generator: new Generator("Atlas Enterprise", "0.1.0"),
        Transitions:
        [
            new TargetTransition(
                Id: "tr-finance-cutover",
                Name: "Cutover programme",
                FromVersionId: "tv-2026-active",
                ToVersionId: "tv-2027-target",
                Description: "The ordered migration steps from the current plateau to the ERP target.",
                Gaps:
                [
                    new TargetGap(
                        Id: "gap-ledger",
                        Name: "Ledger migration gap",
                        Description: "Move ledger integrations and data onto the ERP footprint.",
                        AssetChanges:
                        [
                            new TargetAssetMember(
                                Intent: ChangeIntent.Change,
                                AssetRef: "app-finance-legacy",
                                Asset: new Asset(
                                    "app-finance-legacy",
                                    AssetKind.Application,
                                    "Finance Core",
                                    Lifecycle.Active,
                                    Application: new ApplicationDetails(Version: "2027.01", Vendor: "VEV")),
                                Rationale: "Stabilise the cutover branch before retirement.")
                        ])
                ],
                WorkPackages:
                [
                    new WorkPackage(
                        Id: "wp-erp-rollout",
                        Name: "ERP rollout",
                        Description: "Programme increment for finance cutover.",
                        Deliverables:
                        [
                            new Deliverable("del-tenant-cutover", "Tenant cutover runbook")
                        ])
                ])
        ]);

    [Fact]
    public void Sample_target_architecture_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "target-architecture.sample.json")));

        var results = Evaluate(instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_target_architecture_serialises_to_a_schema_valid_document()
    {
        var json = JsonSerializer.Serialize(SampleTarget(), AtlasContracts.SerializerOptions);

        var results = Evaluate(JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Target_architecture_round_trips_through_the_sdk_with_no_loss()
    {
        var json1 = JsonSerializer.Serialize(SampleTarget(), AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<TargetArchitectureDocument>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(JsonNode.Parse(json2)).IsValid, json2);
    }

    [Fact]
    public void Document_with_two_active_versions_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            {
              "contractVersion": "1",
              "kind": "target-architecture",
              "id": "ta-1",
              "name": "X",
              "scope": { "kind": "estate" },
              "versions": [
                { "id": "v1", "name": "One", "sequence": 1, "status": "active" },
                { "id": "v2", "name": "Two", "sequence": 2, "status": "active" }
              ]
            }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Retire_asset_member_without_a_reference_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            {
              "contractVersion": "1",
              "kind": "target-architecture",
              "id": "ta-1",
              "name": "X",
              "scope": { "kind": "estate" },
              "versions": [
                {
                  "id": "v1",
                  "name": "One",
                  "sequence": 1,
                  "status": "active",
                  "assetChanges": [
                    { "intent": "retire", "rationale": "Missing assetRef" }
                  ]
                }
              ]
            }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Add_relationship_member_without_a_relationship_payload_is_rejected()
    {
        var instance = JsonNode.Parse(
            """
            {
              "contractVersion": "1",
              "kind": "target-architecture",
              "id": "ta-1",
              "name": "X",
              "scope": { "kind": "estate" },
              "versions": [
                {
                  "id": "v1",
                  "name": "One",
                  "sequence": 1,
                  "status": "active",
                  "relationshipChanges": [
                    { "intent": "add" }
                  ]
                }
              ]
            }
            """);

        var results = Evaluate(instance);

        Assert.False(results.IsValid);
    }

    private static string Describe(EvaluationResults results) => JsonSchemaTestHelpers.Describe(results);
}
