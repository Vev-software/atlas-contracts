using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>How an <see cref="ImportBundle"/> applies to the existing catalogue.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ImportMode>))]
public enum ImportMode
{
    /// <summary>Upsert by identifier: assets in the bundle are created or updated; others are left alone.</summary>
    [JsonStringEnumMemberName("merge")]
    Merge,

    /// <summary>Make the target match the bundle: assets not in the bundle are removed.</summary>
    [JsonStringEnumMemberName("replace")]
    Replace
}

/// <summary>
/// The import side of the portability surface: a batch of catalogue assets and manual relationships
/// moved INTO Atlas (handbook 11 §2-3). Unlike an export (<see cref="LandscapeDocument"/>), assets
/// may omit their Atlas <see cref="ImportAsset.Id"/> and instead carry an
/// <see cref="ImportAsset.ExternalId"/> used to resolve references and match on re-import.
/// </summary>
/// <param name="Assets">The assets to import.</param>
/// <param name="Relationships">The manual relationships to import, by reference.</param>
/// <param name="Mode">How the bundle applies to the existing catalogue.</param>
public sealed record ImportBundle(
    [property: JsonPropertyName("assets")] ImmutableArray<ImportAsset> Assets,
    ImmutableArray<ImportRelationship> Relationships = default,
    [property: JsonPropertyName("mode")] ImportMode Mode = ImportMode.Merge)
{
    /// <summary>The atlas-contracts schema major version this bundle conforms to.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>Discriminates an import bundle from an export document on the wire.</summary>
    [JsonPropertyName("kind")]
    public string Kind => "import";

    /// <summary>The manual relationships; never null (defaults to empty).</summary>
    [JsonPropertyName("relationships")]
    public ImmutableArray<ImportRelationship> Relationships { get; init; } =
        Relationships.IsDefault ? [] : Relationships;

    /// <summary>
    /// The set of identifiers this bundle declares — every asset's <see cref="ImportAsset.Id"/> and
    /// <see cref="ImportAsset.ExternalId"/> that is present. Relationship endpoints must resolve to
    /// one of these (or to an asset already in the target catalogue).
    /// </summary>
    public ImmutableHashSet<string> DeclaredReferences()
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var asset in Assets)
        {
            if (asset.Id is not null) builder.Add(asset.Id);
            if (asset.ExternalId is not null) builder.Add(asset.ExternalId);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The distinct relationship endpoint references that do not resolve to any asset declared in
    /// this bundle. Empty means the bundle is internally self-consistent. A non-empty result is only
    /// an error for a self-contained bundle; endpoints may also point at assets already in the
    /// catalogue, which this method — knowing only the bundle — cannot see.
    /// </summary>
    public ImmutableArray<string> UnresolvedReferences()
    {
        var declared = DeclaredReferences();
        var unresolved = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relationship in Relationships)
        {
            foreach (var reference in new[] { relationship.FromRef, relationship.ToRef })
            {
                if (!declared.Contains(reference) && seen.Add(reference))
                    unresolved.Add(reference);
            }
        }

        return unresolved.ToImmutable();
    }
}

/// <summary>
/// An asset to import. Carries at least one of <see cref="Id"/> (an existing Atlas id) or
/// <see cref="ExternalId"/> (a source-system id) so it can be matched and referenced.
/// </summary>
/// <param name="Kind">The kind of asset.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Lifecycle">Catalogue lifecycle state.</param>
/// <param name="Id">An existing Atlas id, if the asset is already catalogued.</param>
/// <param name="ExternalId">An importer-supplied id from the source system.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Tags">Manual classification tags.</param>
/// <param name="Application">Held application metadata, when <paramref name="Kind"/> is <see cref="AssetKind.Application"/>.</param>
/// <param name="Server">Held server metadata, when <paramref name="Kind"/> is <see cref="AssetKind.Server"/>.</param>
/// <param name="Infrastructure">Held infrastructure metadata, when <paramref name="Kind"/> is <see cref="AssetKind.Infrastructure"/>.</param>
public sealed record ImportAsset(
    [property: JsonPropertyName("kind")] AssetKind Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lifecycle")] Lifecycle Lifecycle,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("externalId")] string? ExternalId = null,
    [property: JsonPropertyName("description")] string? Description = null,
    ImmutableArray<Tag> Tags = default,
    [property: JsonPropertyName("application")] ApplicationDetails? Application = null,
    [property: JsonPropertyName("server")] ServerDetails? Server = null,
    [property: JsonPropertyName("infrastructure")] InfrastructureDetails? Infrastructure = null)
{
    /// <summary>Manual classification tags; never null (defaults to empty).</summary>
    [JsonPropertyName("tags")]
    public ImmutableArray<Tag> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}

/// <summary>
/// A manual relationship to import. Endpoints are references — an asset <see cref="ImportAsset.Id"/>
/// or <see cref="ImportAsset.ExternalId"/> present in the bundle (or already in the catalogue).
/// </summary>
/// <param name="FromRef">Reference to the source asset.</param>
/// <param name="ToRef">Reference to the target asset.</param>
/// <param name="Type">The manual relationship type.</param>
/// <param name="Id">An existing Atlas id for the relationship, if known.</param>
/// <param name="Description">Optional free-text note on the link.</param>
public sealed record ImportRelationship(
    [property: JsonPropertyName("fromRef")] string FromRef,
    [property: JsonPropertyName("toRef")] string ToRef,
    [property: JsonPropertyName("type")] RelationshipType Type,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("description")] string? Description = null);
