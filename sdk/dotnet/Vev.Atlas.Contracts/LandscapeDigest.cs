using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>
/// Digest classification of a <see cref="LandscapeDigestItem"/>. Deliberately narrower than
/// <see cref="AssetKind"/>: <see cref="Platform"/> folds in the server/infrastructure/AI substrate,
/// <see cref="Vendor"/> is a producer-derived summary entry, and the data-layer kinds
/// (data-area, dataset, column) are excluded from digests.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DigestKind>))]
public enum DigestKind
{
    /// <summary>An application-system.</summary>
    [JsonStringEnumMemberName("system")]
    System,

    /// <summary>A deployable application.</summary>
    [JsonStringEnumMemberName("application")]
    Application,

    /// <summary>Technical substrate: servers, infrastructure and AI platforms/models.</summary>
    [JsonStringEnumMemberName("platform")]
    Platform,

    /// <summary>A producer-derived vendor summary entry (no direct <see cref="AssetKind"/>).</summary>
    [JsonStringEnumMemberName("vendor")]
    Vendor
}

/// <summary>
/// The intended change for a digest item, taken from the Target Architecture contract's change
/// intent vocabulary (add/change/retire) and extended with <see cref="Replace"/> for digest
/// purposes. The shared change-intent vocabulary is not modified.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannedChange>))]
public enum PlannedChange
{
    /// <summary>Add a new item to the landscape.</summary>
    [JsonStringEnumMemberName("add")]
    Add,

    /// <summary>Change an existing item.</summary>
    [JsonStringEnumMemberName("change")]
    Change,

    /// <summary>Replace an existing item with a different one.</summary>
    [JsonStringEnumMemberName("replace")]
    Replace,

    /// <summary>Retire an existing item.</summary>
    [JsonStringEnumMemberName("retire")]
    Retire
}

/// <summary>
/// Which asset kinds and tags were included in a <see cref="LandscapeDigest"/>. Both arrays are
/// required and may be empty: an empty <see cref="Kinds"/> means no kinds were included, and an
/// empty <see cref="Tags"/> means no tag filter was applied (tag matching is OR within the list,
/// AND with the kind filter).
/// </summary>
/// <param name="Kinds">The digest item kinds included.</param>
/// <param name="Tags">The tag filters that qualified inclusion.</param>
public sealed record LandscapeDigestScope(
    ImmutableArray<DigestKind> Kinds,
    ImmutableArray<Tag> Tags = default)
{
    /// <summary>The digest item kinds included; never null (defaults to empty).</summary>
    [JsonPropertyName("kinds")]
    public ImmutableArray<DigestKind> Kinds { get; init; } = Kinds.IsDefault ? [] : Kinds;

    /// <summary>The tag filters that qualified inclusion; never null (defaults to empty).</summary>
    [JsonPropertyName("tags")]
    public ImmutableArray<Tag> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}

/// <summary>
/// One minimized landscape item. Carries only the fields a consented consumer needs:
/// classification, name, lifecycle, and optional vendor / planned change / integration count.
/// Everything else (hostnames, IPs, server and discovery details, descriptions, attachments,
/// people, column-level data, credentials) is forbidden by the schema.
/// </summary>
/// <param name="Kind">The digest classification of the item.</param>
/// <param name="Name">The item's catalogue name.</param>
/// <param name="Lifecycle">Catalogue lifecycle state.</param>
/// <param name="Vendor">Optional vendor of the item, when known.</param>
/// <param name="PlannedChange">Optional intended change, from the Target Architecture contract.</param>
/// <param name="IntegrationCount">Optional count of integrations the item participates in — a count only, never the identities of the integrated parties.</param>
public sealed record LandscapeDigestItem(
    [property: JsonPropertyName("kind")] DigestKind Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lifecycle")] Lifecycle Lifecycle,
    [property: JsonPropertyName("vendor")] string? Vendor = null,
    [property: JsonPropertyName("plannedChange")] PlannedChange? PlannedChange = null,
    [property: JsonPropertyName("integrationCount")] int? IntegrationCount = null);

/// <summary>
/// A minimized, versioned summary of an Atlas landscape for consented consumers. Push-only
/// sharing surface: Atlas produces the digest and delivers it as a file or over an outbound
/// HTTPS call; a consumer never pulls from the customer's network. This is a summary for
/// sharing — not the customer-owned portability export (<see cref="LandscapeDocument"/>) and
/// not an import document. The digest payload is unsigned on the wire; the detached signature
/// (RSA-SHA-256 PKCS#1 v1.5 over the RFC 8785 canonicalization) travels in a documented
/// wrapper, see docs/landscape-digest.md.
/// </summary>
/// <param name="DigestId">Stable identifier for this digest. Opaque; unique per produced digest (a UUIDv7 is recommended).</param>
/// <param name="GeneratedAt">When the digest was produced.</param>
/// <param name="SourceInstanceId">Opaque identifier of the producing Atlas instance. Consumers scope sequence/replay state by it; a source that resets its sequence must mint a new one.</param>
/// <param name="Sequence">Monotonic per source: the first digest is 1, each later digest is strictly higher (gaps allowed). Consumers reject a sequence at or below the last accepted for the same source.</param>
/// <param name="Scope">Which asset kinds and tags were included.</param>
/// <param name="Items">The minimized landscape items. Every item's kind must be a member of <see cref="LandscapeDigestScope.Kinds"/>.</param>
public sealed record LandscapeDigest(
    [property: JsonPropertyName("digestId")] string DigestId,
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("sourceInstanceId")] string SourceInstanceId,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("scope")] LandscapeDigestScope Scope,
    ImmutableArray<LandscapeDigestItem> Items = default)
{
    /// <summary>The atlas-contracts schema major version this digest conforms to.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>The minimized landscape items; never null (defaults to empty).</summary>
    [JsonPropertyName("items")]
    public ImmutableArray<LandscapeDigestItem> Items { get; init; } = Items.IsDefault ? [] : Items;
}
