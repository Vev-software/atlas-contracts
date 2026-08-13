using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance for the data-layer catalogue (#11): the data-area → dataset → column model plus the
/// 'joins-on' key. Proves the published schemas accept a valid data layer and reject analysis fields,
/// that the .NET SDK serialises to a schema-valid shape, that a data layer round-trips with no loss,
/// and that the containment invariant ("every higher level points down to a concrete dataset/column")
/// holds via <see cref="DataLayerContainment.DataLayerContainmentErrors"/> (handbook 11 §1).
/// </summary>
public sealed class DataLayerConformanceTests
{
    private static readonly string SampleDir = TestSchemas.SampleDir;

    private static readonly JsonSchema AssetSchema = TestSchemas.Load("asset.schema.json");
    private static readonly JsonSchema LandscapeSchema = TestSchemas.Load("landscape.schema.json");
    private static readonly JsonSchema ImportSchema = TestSchemas.Load("import.schema.json");

    private static EvaluationResults Evaluate(JsonSchema schema, JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(schema, instance);

    /// <summary>A well-formed data layer built with the SDK: two systems, each with a data-area →
    /// dataset → columns, and a cross-dataset 'joins-on' key.</summary>
    private static LandscapeDocument SampleDataLayer() => new(
        Assets:
        [
            new Asset("sys-crm", AssetKind.System, "CRM platform", Lifecycle.Active),
            new Asset("da-customers", AssetKind.DataArea, "Customer master data", Lifecycle.Active,
                DataArea: new DataAreaDetails(Realisation: "microservice")),
            new Asset("ds-customers", AssetKind.Dataset, "Customers", Lifecycle.Active,
                Dataset: new DatasetDetails(PhysicalName: "dbo.customers", Owner: "CRM team")),
            new Asset("col-customer-id", AssetKind.Column, "customer_id", Lifecycle.Active,
                Column: new ColumnDetails(DataType: "uuid", Nullable: false)),
            new Asset("sys-payments", AssetKind.System, "Payments platform", Lifecycle.Active),
            new Asset("da-billing", AssetKind.DataArea, "Billing", Lifecycle.Active,
                DataArea: new DataAreaDetails(Realisation: "reference-catalogue")),
            new Asset("ds-invoices", AssetKind.Dataset, "Invoices", Lifecycle.Active,
                Dataset: new DatasetDetails(PhysicalName: "dbo.invoices", Owner: "Finance team")),
            new Asset("col-invoice-customer", AssetKind.Column, "customer_id", Lifecycle.Active,
                Column: new ColumnDetails(DataType: "uuid", Nullable: false))
        ],
        Relationships:
        [
            new Relationship("c1", "da-customers", "sys-crm", RelationshipType.PartOf),
            new Relationship("c2", "ds-customers", "da-customers", RelationshipType.PartOf),
            new Relationship("c3", "col-customer-id", "ds-customers", RelationshipType.PartOf),
            new Relationship("c4", "da-billing", "sys-payments", RelationshipType.PartOf),
            new Relationship("c5", "ds-invoices", "da-billing", RelationshipType.PartOf),
            new Relationship("c6", "col-invoice-customer", "ds-invoices", RelationshipType.PartOf),
            new Relationship("k1", "col-invoice-customer", "col-customer-id", RelationshipType.JoinsOn)
        ]);

    // ---- schema ----

    [Fact]
    public void Sample_data_layer_landscape_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "landscape-datalayer.sample.json")));

        var results = Evaluate(LandscapeSchema, instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_data_layer_serialises_to_a_schema_valid_document()
    {
        var json = JsonSerializer.Serialize(SampleDataLayer(), AtlasContracts.SerializerOptions);

        var results = Evaluate(LandscapeSchema, JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Data_layer_round_trips_through_the_sdk_with_no_loss()
    {
        var json1 = JsonSerializer.Serialize(SampleDataLayer(), AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<LandscapeDocument>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(LandscapeSchema, JsonNode.Parse(json2)).IsValid);
    }

    [Fact]
    public void Column_with_an_analysis_field_is_rejected()
    {
        // A column carries name + held type only. A classification/quality verdict is paid Atlas core
        // (handbook 11 §1) and must not smuggle itself into the public column contract.
        var instance = JsonNode.Parse(
            """
            { "id": "col-1", "kind": "column", "name": "email", "lifecycle": "active",
              "column": { "dataType": "varchar(320)", "classification": "pii" } }
            """);

        Assert.False(Evaluate(AssetSchema, instance).IsValid);
    }

    [Fact]
    public void Column_details_on_a_non_column_asset_is_rejected()
    {
        // The kind-specific details are gated by kind; a system must not carry column details.
        var instance = JsonNode.Parse(
            """
            { "id": "sys-1", "kind": "system", "name": "X", "lifecycle": "active",
              "column": { "dataType": "uuid" } }
            """);

        Assert.False(Evaluate(AssetSchema, instance).IsValid);
    }

    // ---- containment invariant ----

    [Fact]
    public void Well_formed_data_layer_has_no_containment_errors()
    {
        Assert.Empty(SampleDataLayer().DataLayerContainmentErrors());
        Assert.True(SampleDataLayer().DataLayerContainmentIsValid());
    }

    [Fact]
    public void Sample_data_layer_file_has_no_containment_errors()
    {
        var document = JsonSerializer.Deserialize<LandscapeDocument>(
            File.ReadAllText(Path.Combine(SampleDir, "landscape-datalayer.sample.json")), AtlasContracts.SerializerOptions);

        Assert.Empty(document!.DataLayerContainmentErrors());
    }

    [Fact]
    public void Column_without_a_dataset_parent_is_a_containment_error()
    {
        var document = new LandscapeDocument(
            Assets:
            [
                new Asset("ds-1", AssetKind.Dataset, "Orphaned dataset", Lifecycle.Active),
                new Asset("col-1", AssetKind.Column, "loose_column", Lifecycle.Active)
            ]);

        var errors = document.DataLayerContainmentErrors();

        // The column has no dataset parent AND the dataset has no data-area parent: two violations.
        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, e => e.Contains("col-1"));
        Assert.Contains(errors, e => e.Contains("ds-1"));
    }

    [Fact]
    public void Column_part_of_two_datasets_is_a_containment_error()
    {
        var document = new LandscapeDocument(
            Assets:
            [
                new Asset("sys-1", AssetKind.System, "S", Lifecycle.Active),
                new Asset("da-1", AssetKind.DataArea, "DA", Lifecycle.Active),
                new Asset("ds-1", AssetKind.Dataset, "DS one", Lifecycle.Active),
                new Asset("ds-2", AssetKind.Dataset, "DS two", Lifecycle.Active),
                new Asset("col-1", AssetKind.Column, "ambiguous", Lifecycle.Active)
            ],
            Relationships:
            [
                new Relationship("r1", "da-1", "sys-1", RelationshipType.PartOf),
                new Relationship("r2", "ds-1", "da-1", RelationshipType.PartOf),
                new Relationship("r3", "ds-2", "da-1", RelationshipType.PartOf),
                new Relationship("r4", "col-1", "ds-1", RelationshipType.PartOf),
                new Relationship("r5", "col-1", "ds-2", RelationshipType.PartOf)
            ]);

        var errors = document.DataLayerContainmentErrors();

        Assert.Contains(errors, e => e.Contains("col-1"));
    }

    [Fact]
    public void Data_area_contained_by_a_non_system_is_a_containment_error()
    {
        // A data-area must be part-of a System; parenting it to an application skips the invariant.
        var document = new LandscapeDocument(
            Assets:
            [
                new Asset("app-1", AssetKind.Application, "An app", Lifecycle.Active),
                new Asset("da-1", AssetKind.DataArea, "Misplaced area", Lifecycle.Active)
            ],
            Relationships:
            [
                new Relationship("r1", "da-1", "app-1", RelationshipType.PartOf)
            ]);

        var errors = document.DataLayerContainmentErrors();

        Assert.Contains(errors, e => e.Contains("da-1"));
    }

    // ---- import (portability) ----

    [Fact]
    public void Import_data_layer_sample_conforms_to_the_published_schema()
    {
        var instance = JsonNode.Parse(File.ReadAllText(Path.Combine(SampleDir, "import-datalayer.sample.json")));

        var results = Evaluate(ImportSchema, instance);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Import_data_layer_sample_has_no_unresolved_references()
    {
        var bundle = JsonSerializer.Deserialize<ImportBundle>(
            File.ReadAllText(Path.Combine(SampleDir, "import-datalayer.sample.json")), AtlasContracts.SerializerOptions);

        Assert.Empty(bundle!.UnresolvedReferences());
    }

    [Fact]
    public void Import_data_layer_round_trips_by_external_id_with_no_loss()
    {
        var bundle = new ImportBundle(
            Assets:
            [
                new ImportAsset(AssetKind.System, "CRM platform", Lifecycle.Active, ExternalId: "src:sys-crm"),
                new ImportAsset(AssetKind.DataArea, "Customer master data", Lifecycle.Active,
                    ExternalId: "src:da-customers", DataArea: new DataAreaDetails("microservice")),
                new ImportAsset(AssetKind.Dataset, "Customers", Lifecycle.Active,
                    ExternalId: "src:ds-customers", Dataset: new DatasetDetails("dbo.customers", "CRM team")),
                new ImportAsset(AssetKind.Column, "customer_id", Lifecycle.Active,
                    ExternalId: "src:col-customer-id", Column: new ColumnDetails("uuid", Nullable: false))
            ],
            Relationships:
            [
                new ImportRelationship("src:da-customers", "src:sys-crm", RelationshipType.PartOf),
                new ImportRelationship("src:ds-customers", "src:da-customers", RelationshipType.PartOf),
                new ImportRelationship("src:col-customer-id", "src:ds-customers", RelationshipType.PartOf)
            ]);

        var json1 = JsonSerializer.Serialize(bundle, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<ImportBundle>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(ImportSchema, JsonNode.Parse(json2)).IsValid);
        Assert.Empty(roundTripped!.UnresolvedReferences());
    }

    private static string Describe(EvaluationResults results) => JsonSchemaTestHelpers.Describe(results);
}
