using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>
/// A single catalogued asset. Kind-specific attributes are held facts (e.g. a server's OS name),
/// never analysis: no EOL risk, no integration-criticality, no portfolio scoring — those work with
/// the data and live in the private Atlas core (handbook 11 §1).
/// </summary>
/// <param name="Id">Stable, opaque identifier. Never reused or re-meaning'd once published.</param>
/// <param name="Kind">The kind of asset.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Lifecycle">Catalogue lifecycle state.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Tags">Manual classification tags.</param>
/// <param name="Application">Held application metadata, when <see cref="Kind"/> is <see cref="AssetKind.Application"/>.</param>
/// <param name="Server">Held server metadata, when <see cref="Kind"/> is <see cref="AssetKind.Server"/>.</param>
/// <param name="Infrastructure">Held infrastructure metadata, when <see cref="Kind"/> is <see cref="AssetKind.Infrastructure"/>.</param>
public sealed record Asset(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] AssetKind Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lifecycle")] Lifecycle Lifecycle,
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

/// <summary>Held application metadata. Cataloguing only.</summary>
public sealed record ApplicationDetails(
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("vendor")] string? Vendor = null,
    [property: JsonPropertyName("businessOwner")] string? BusinessOwner = null);

/// <summary>Held server metadata. OS is a recorded fact, not an EOL/risk assessment (paid Atlas core).</summary>
public sealed record ServerDetails(
    [property: JsonPropertyName("hostname")] string? Hostname = null,
    [property: JsonPropertyName("environment")] string? Environment = null,
    [property: JsonPropertyName("operatingSystem")] string? OperatingSystem = null);

/// <summary>Held infrastructure metadata. Cataloguing only.</summary>
public sealed record InfrastructureDetails(
    [property: JsonPropertyName("category")] string? Category = null,
    [property: JsonPropertyName("location")] string? Location = null);
