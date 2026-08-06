using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>
/// The portability surface: a whole landscape (assets + manual relationships) carried across a
/// boundary. Backs customer-owned data export and community importers/exporters
/// (handbook 11 §2-3, 12 §Phase C).
/// </summary>
/// <param name="Assets">The catalogued assets.</param>
/// <param name="Relationships">The manual relationships between assets.</param>
/// <param name="ExportedAt">When the document was produced, if known.</param>
public sealed record LandscapeDocument(
    [property: JsonPropertyName("assets")] ImmutableArray<Asset> Assets,
    ImmutableArray<Relationship> Relationships = default,
    [property: JsonPropertyName("exportedAt")] DateTimeOffset? ExportedAt = null)
{
    /// <summary>The atlas-contracts schema major version this document conforms to.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>The manual relationships; never null (defaults to empty).</summary>
    [JsonPropertyName("relationships")]
    public ImmutableArray<Relationship> Relationships { get; init; } =
        Relationships.IsDefault ? [] : Relationships;
}

/// <summary>Contract-wide constants and the canonical serializer options for Atlas contracts.</summary>
public static class AtlasContracts
{
    /// <summary>The published schema major version. Breaking it requires an ADR + migration (AGENTS.md §4).</summary>
    public const string SchemaMajorVersion = "1";

    /// <summary>
    /// The canonical <see cref="JsonSerializerOptions"/> for reading and writing Atlas contract
    /// documents. Consumers should reuse this so the wire shape matches the published schemas.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
