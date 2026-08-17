using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Vev.Atlas.Contracts;
using Xunit;

namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Conformance for the AI asset catalogue (#26): ai-service and ai-model as first-class assets.
/// Proves the published schemas accept valid AI assets, that the .NET SDK serialises to a schema-valid
/// shape, that AI assets round-trip with no loss, and that analysis fields are rejected (handbook 11 §1).
/// </summary>
public sealed class AiAssetConformanceTests
{
    private static readonly JsonSchema AssetSchema = TestSchemas.Load("asset.schema.json");
    private static readonly JsonSchema LandscapeSchema = TestSchemas.Load("landscape.schema.json");
    private static readonly JsonSchema ImportSchema = TestSchemas.Load("import.schema.json");

    private static EvaluationResults Evaluate(JsonSchema schema, JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(schema, instance);

    /// <summary>A well-formed AI estate: an ai-service with two ai-models linked via relationships.</summary>
    private static LandscapeDocument SampleAiEstate() => new(
        Assets:
        [
            new Asset("svc-azure-ai", AssetKind.AiService, "Azure OpenAI", Lifecycle.Active,
                AiService: new AiServiceDetails(Provider: "Azure OpenAI", Endpoint: "https://my-ai.openai.azure.com/", Environment: "production")),
            new Asset("mdl-gpt4", AssetKind.AiModel, "GPT-4", Lifecycle.Active,
                AiModel: new AiModelDetails(ModelName: "gpt-4", Version: "0613", Provider: "OpenAI")),
            new Asset("mdl-embedding", AssetKind.AiModel, "text-embedding-3-large", Lifecycle.Active,
                AiModel: new AiModelDetails(ModelName: "text-embedding-3-large", Version: "1", Provider: "OpenAI", EmbeddingDimensions: 3072))
        ],
        Relationships:
        [
            new Relationship("r1", "mdl-gpt4", "svc-azure-ai", RelationshipType.DependsOn),
            new Relationship("r2", "mdl-embedding", "svc-azure-ai", RelationshipType.DependsOn)
        ]);

    // ---- schema ----

    [Fact]
    public void Dotnet_sdk_ai_service_serialises_to_a_schema_valid_asset()
    {
        var asset = new Asset("svc-1", AssetKind.AiService, "Anthropic", Lifecycle.Active,
            AiService: new AiServiceDetails(Provider: "Anthropic", Endpoint: "https://api.anthropic.com/", Environment: "production"));

        var json = JsonSerializer.Serialize(asset, AtlasContracts.SerializerOptions);
        var results = Evaluate(AssetSchema, JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Dotnet_sdk_ai_model_serialises_to_a_schema_valid_asset()
    {
        var asset = new Asset("mdl-1", AssetKind.AiModel, "Claude 3 Opus", Lifecycle.Active,
            AiModel: new AiModelDetails(ModelName: "claude-3-opus", Version: "20240229", Provider: "Anthropic"));

        var json = JsonSerializer.Serialize(asset, AtlasContracts.SerializerOptions);
        var results = Evaluate(AssetSchema, JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Ai_estate_landscape_conforms_to_the_published_schema()
    {
        var json = JsonSerializer.Serialize(SampleAiEstate(), AtlasContracts.SerializerOptions);
        var results = Evaluate(LandscapeSchema, JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Ai_asset_round_trips_through_the_sdk_with_no_loss()
    {
        var asset = new Asset("svc-1", AssetKind.AiService, "Azure OpenAI", Lifecycle.Active,
            AiService: new AiServiceDetails(Provider: "Azure OpenAI", Endpoint: "https://my-ai.openai.azure.com/", Environment: "production"));

        var json1 = JsonSerializer.Serialize(asset, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<Asset>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.True(Evaluate(AssetSchema, JsonNode.Parse(json2)).IsValid);
    }

    [Fact]
    public void Ai_model_with_embedding_dimensions_round_trips()
    {
        var asset = new Asset("mdl-emb", AssetKind.AiModel, "Embedding model", Lifecycle.Active,
            AiModel: new AiModelDetails(ModelName: "text-embedding-3-large", Provider: "OpenAI", EmbeddingDimensions: 3072));

        var json1 = JsonSerializer.Serialize(asset, AtlasContracts.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<Asset>(json1, AtlasContracts.SerializerOptions);
        var json2 = JsonSerializer.Serialize(roundTripped, AtlasContracts.SerializerOptions);

        Assert.Equal(json1, json2);
        Assert.Equal(3072, roundTripped!.AiModel!.EmbeddingDimensions);
    }

    [Fact]
    public void Ai_service_with_an_analysis_field_is_rejected()
    {
        // An ai-service carries provider, endpoint, environment only. A cost or usage metric is paid
        // Atlas core (handbook 11 §1) and must not smuggle itself into the public contract.
        var instance = JsonNode.Parse(
            """
            { "id": "svc-1", "kind": "ai-service", "name": "AI", "lifecycle": "active",
              "aiService": { "provider": "Azure", "monthlyCost": 1234 } }
            """);

        Assert.False(Evaluate(AssetSchema, instance).IsValid);
    }

    [Fact]
    public void Ai_model_with_an_analysis_field_is_rejected()
    {
        // An ai-model carries name, version, provider, dimensions only. A safety score is paid
        // Atlas core (handbook 11 §1) and must not smuggle itself into the public contract.
        var instance = JsonNode.Parse(
            """
            { "id": "mdl-1", "kind": "ai-model", "name": "Model", "lifecycle": "active",
              "aiModel": { "modelName": "gpt-4", "safetyScore": 0.95 } }
            """);

        Assert.False(Evaluate(AssetSchema, instance).IsValid);
    }

    [Fact]
    public void AiService_details_on_a_non_ai_service_asset_is_rejected()
    {
        // The kind-specific details are gated by kind; an application must not carry aiService details.
        var instance = JsonNode.Parse(
            """
            { "id": "app-1", "kind": "application", "name": "X", "lifecycle": "active",
              "aiService": { "provider": "Azure" } }
            """);

        Assert.False(Evaluate(AssetSchema, instance).IsValid);
    }

    [Fact]
    public void AiModel_details_on_a_non_ai_model_asset_is_rejected()
    {
        // The kind-specific details are gated by kind; a server must not carry aiModel details.
        var instance = JsonNode.Parse(
            """
            { "id": "srv-1", "kind": "server", "name": "X", "lifecycle": "active",
              "aiModel": { "modelName": "gpt-4" } }
            """);

        Assert.False(Evaluate(AssetSchema, instance).IsValid);
    }

    // ---- import (portability) ----

    [Fact]
    public void Import_bundle_with_ai_assets_conforms_to_the_published_schema()
    {
        var bundle = new ImportBundle(
            Assets:
            [
                new ImportAsset(AssetKind.AiService, "Azure OpenAI", Lifecycle.Active,
                    ExternalId: "src:svc-azure",
                    AiService: new AiServiceDetails(Provider: "Azure OpenAI", Endpoint: "https://my-ai.openai.azure.com/", Environment: "production")),
                new ImportAsset(AssetKind.AiModel, "GPT-4", Lifecycle.Active,
                    ExternalId: "src:mdl-gpt4",
                    AiModel: new AiModelDetails(ModelName: "gpt-4", Version: "0613", Provider: "OpenAI"))
            ],
            Relationships:
            [
                new ImportRelationship("src:mdl-gpt4", "src:svc-azure", RelationshipType.DependsOn)
            ]);

        var json = JsonSerializer.Serialize(bundle, AtlasContracts.SerializerOptions);
        var results = Evaluate(ImportSchema, JsonNode.Parse(json));

        Assert.True(results.IsValid, Describe(results));
        Assert.Empty(bundle.UnresolvedReferences());
    }

    [Fact]
    public void Import_bundle_with_ai_assets_round_trips_by_external_id_with_no_loss()
    {
        var bundle = new ImportBundle(
            Assets:
            [
                new ImportAsset(AssetKind.AiService, "Azure OpenAI", Lifecycle.Active,
                    ExternalId: "src:svc-azure",
                    AiService: new AiServiceDetails(Provider: "Azure OpenAI", Environment: "production")),
                new ImportAsset(AssetKind.AiModel, "GPT-4", Lifecycle.Active,
                    ExternalId: "src:mdl-gpt4",
                    AiModel: new AiModelDetails(ModelName: "gpt-4", Version: "0613", Provider: "OpenAI"))
            ],
            Relationships:
            [
                new ImportRelationship("src:mdl-gpt4", "src:svc-azure", RelationshipType.DependsOn)
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
