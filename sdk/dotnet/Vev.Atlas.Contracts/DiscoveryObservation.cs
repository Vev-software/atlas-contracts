using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>How a <see cref="DiscoveryObservationBatch"/> was collected.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CollectionMethod>))]
public enum CollectionMethod
{
    /// <summary>An agent installed in the landscape emitted the batch.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent,

    /// <summary>A collector scanned the landscape remotely (agent-less).</summary>
    [JsonStringEnumMemberName("agentless")]
    Agentless
}

/// <summary>Transport protocol of an <see cref="ObservedPort"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PortProtocol>))]
public enum PortProtocol
{
    /// <summary>TCP.</summary>
    [JsonStringEnumMemberName("tcp")]
    Tcp,

    /// <summary>UDP.</summary>
    [JsonStringEnumMemberName("udp")]
    Udp
}

/// <summary>
/// The public wire format a discovery scanner emits INTO Atlas: a batch of raw observations of a
/// landscape that the <b>private</b> Atlas Enterprise reconciliation engine correlates, deduplicates
/// and turns into catalogue assets (handbook 11 §3, 12 §Phase A). This contract carries facts only —
/// an <see cref="Observation"/> has no Atlas id and no lifecycle (the reconciler assigns those), and
/// never any analysis. The reconciliation logic that consumes it is the moat and is not in this repo.
/// </summary>
/// <param name="Source">Provenance: which scanner produced the batch and how.</param>
/// <param name="ObservedAt">When the scan that produced this batch was taken.</param>
/// <param name="Observations">The raw observations; may be empty.</param>
public sealed record DiscoveryObservationBatch(
    [property: JsonPropertyName("source")] ObservationSource Source,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    ImmutableArray<Observation> Observations = default)
{
    /// <summary>The atlas-contracts schema major version this batch conforms to.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>Discriminates a discovery batch from the import/export documents on the wire.</summary>
    [JsonPropertyName("kind")]
    public string Kind => "discovery-observation";

    /// <summary>The raw observations; never null (defaults to empty).</summary>
    [JsonPropertyName("observations")]
    public ImmutableArray<Observation> Observations { get; init; } =
        Observations.IsDefault ? [] : Observations;
}

/// <summary>
/// Which scanner produced a batch. The authoritative tenant binding is the authenticated
/// machine-principal at ingestion (a Fabric concern); <see cref="TenantRef"/> is an optional hint only.
/// </summary>
/// <param name="AgentId">Stable identifier of the emitting agent or collector, constant across scans.</param>
/// <param name="Method">How the batch was collected.</param>
/// <param name="Version">The scanner's version, if known.</param>
/// <param name="TenantRef">Optional, non-authoritative opaque tenant hint.</param>
public sealed record ObservationSource(
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("method")] CollectionMethod Method,
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("tenantRef")] string? TenantRef = null);

/// <summary>
/// A single raw sighting of something in the landscape. Facts only: no Atlas id and no lifecycle
/// (the private reconciler assigns catalogue identity and state), and no analysis. Kind-specific held
/// facts reuse the catalogue vocabulary; discovery-only facts (ports/processes/packages) are extra
/// observations, not catalogue analysis.
/// </summary>
/// <param name="ObservedId">Scanner-local identifier, stable across scans; NOT an Atlas asset id.</param>
/// <param name="Kind">The kind of thing observed.</param>
/// <param name="Name">Human-readable name as seen.</param>
/// <param name="Fingerprint">Durable correlation attributes (more than a hostname).</param>
/// <param name="Ports">Observed listening ports.</param>
/// <param name="Processes">Observed running processes / services.</param>
/// <param name="Packages">Observed installed runtimes / packages.</param>
/// <param name="Tags">Observed lightweight labels.</param>
/// <param name="FirstSeen">When the source first saw this thing, if tracked.</param>
/// <param name="LastSeen">When the source most recently saw this thing, if tracked.</param>
/// <param name="Application">Held application facts, when <paramref name="Kind"/> is <see cref="AssetKind.Application"/>.</param>
/// <param name="Server">Held server facts, when <paramref name="Kind"/> is <see cref="AssetKind.Server"/>.</param>
/// <param name="Infrastructure">Held infrastructure facts, when <paramref name="Kind"/> is <see cref="AssetKind.Infrastructure"/>.</param>
public sealed record Observation(
    [property: JsonPropertyName("observedId")] string ObservedId,
    [property: JsonPropertyName("kind")] AssetKind Kind,
    [property: JsonPropertyName("name")] string Name,
    ImmutableArray<FingerprintAttribute> Fingerprint = default,
    ImmutableArray<ObservedPort> Ports = default,
    ImmutableArray<ObservedProcess> Processes = default,
    ImmutableArray<ObservedPackage> Packages = default,
    ImmutableArray<Tag> Tags = default,
    [property: JsonPropertyName("firstSeen")] DateTimeOffset? FirstSeen = null,
    [property: JsonPropertyName("lastSeen")] DateTimeOffset? LastSeen = null,
    [property: JsonPropertyName("application")] ApplicationDetails? Application = null,
    [property: JsonPropertyName("server")] ServerDetails? Server = null,
    [property: JsonPropertyName("infrastructure")] InfrastructureDetails? Infrastructure = null)
{
    /// <summary>Durable correlation attributes; never null (defaults to empty).</summary>
    [JsonPropertyName("fingerprint")]
    public ImmutableArray<FingerprintAttribute> Fingerprint { get; init; } =
        Fingerprint.IsDefault ? [] : Fingerprint;

    /// <summary>Observed listening ports; never null (defaults to empty).</summary>
    [JsonPropertyName("ports")]
    public ImmutableArray<ObservedPort> Ports { get; init; } = Ports.IsDefault ? [] : Ports;

    /// <summary>Observed processes; never null (defaults to empty).</summary>
    [JsonPropertyName("processes")]
    public ImmutableArray<ObservedProcess> Processes { get; init; } = Processes.IsDefault ? [] : Processes;

    /// <summary>Observed packages; never null (defaults to empty).</summary>
    [JsonPropertyName("packages")]
    public ImmutableArray<ObservedPackage> Packages { get; init; } = Packages.IsDefault ? [] : Packages;

    /// <summary>Observed labels; never null (defaults to empty).</summary>
    [JsonPropertyName("tags")]
    public ImmutableArray<Tag> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}

/// <summary>
/// One durable correlation attribute: a key and its observed value (both required — unlike a
/// <see cref="Tag"/>, a fingerprint carries no valueless keys). How attributes are weighted for
/// matching is private reconciler logic, not part of this contract.
/// </summary>
/// <param name="Key">The attribute key (e.g. "machine-id", "mac", "cloud-instance-id").</param>
/// <param name="Value">The observed value.</param>
public sealed record FingerprintAttribute(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value);

/// <summary>An observed listening port. A recorded fact, not a measured integration.</summary>
/// <param name="Port">The port number (1–65535).</param>
/// <param name="Protocol">The transport protocol, if known.</param>
/// <param name="Address">The bind address the port was seen on, if known.</param>
public sealed record ObservedPort(
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("protocol")] PortProtocol? Protocol = null,
    [property: JsonPropertyName("address")] string? Address = null);

/// <summary>An observed running process or service. A recorded fact.</summary>
/// <param name="Name">The process / service name.</param>
/// <param name="Port">The port it was seen listening on, if known.</param>
public sealed record ObservedProcess(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("port")] int? Port = null);

/// <summary>
/// An observed installed runtime or package. A recorded fact; the version is what was seen, not an
/// EOL/risk assessment (that is paid Atlas core).
/// </summary>
/// <param name="Name">The package / runtime name.</param>
/// <param name="Version">The observed version, if known.</param>
public sealed record ObservedPackage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version = null);
