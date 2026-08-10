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
/// <param name="DataArea">Held data-area metadata, when <see cref="Kind"/> is <see cref="AssetKind.DataArea"/>.</param>
/// <param name="Dataset">Held dataset metadata, when <see cref="Kind"/> is <see cref="AssetKind.Dataset"/>.</param>
/// <param name="Column">Held column metadata, when <see cref="Kind"/> is <see cref="AssetKind.Column"/>.</param>
public sealed record Asset(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] AssetKind Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lifecycle")] Lifecycle Lifecycle,
    [property: JsonPropertyName("description")] string? Description = null,
    ImmutableArray<Tag> Tags = default,
    [property: JsonPropertyName("application")] ApplicationDetails? Application = null,
    [property: JsonPropertyName("server")] ServerDetails? Server = null,
    [property: JsonPropertyName("infrastructure")] InfrastructureDetails? Infrastructure = null,
    [property: JsonPropertyName("dataArea")] DataAreaDetails? DataArea = null,
    [property: JsonPropertyName("dataset")] DatasetDetails? Dataset = null,
    [property: JsonPropertyName("column")] ColumnDetails? Column = null)
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

/// <summary>
/// Held data-area (dataområde) metadata. Cataloguing only: <paramref name="Realisation"/> is free
/// metadata (e.g. "microservice", "reference-catalogue", "spreadsheet"), not a locked vocabulary
/// and not analysis.
/// </summary>
/// <param name="Realisation">How the data area is realised, if known. Free-text metadata.</param>
public sealed record DataAreaDetails(
    [property: JsonPropertyName("realisation")] string? Realisation = null);

/// <summary>
/// Held dataset (datamodel) metadata. Physical name and ownership are recorded facts, never quality
/// or classification verdicts (those work with the data and are paid Atlas core, handbook 11 §1).
/// </summary>
/// <param name="PhysicalName">The dataset's physical/table name in the source system, if known.</param>
/// <param name="Owner">The recorded owner of the dataset, if known.</param>
public sealed record DatasetDetails(
    [property: JsonPropertyName("physicalName")] string? PhysicalName = null,
    [property: JsonPropertyName("owner")] string? Owner = null);

/// <summary>
/// Held column (kolonne) metadata. The declared data type and nullability are recorded facts.
/// NO analysis: no quality score, no classification verdict, no PII/sensitivity flag — those are
/// paid Atlas core (handbook 11 §1).
/// </summary>
/// <param name="DataType">The column's declared data type as a held fact (e.g. "varchar(64)", "int").</param>
/// <param name="Nullable">Whether the column is nullable, if known. A held schema fact, not analysis.</param>
public sealed record ColumnDetails(
    [property: JsonPropertyName("dataType")] string? DataType = null,
    [property: JsonPropertyName("nullable")] bool? Nullable = null);
