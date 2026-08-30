using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>The landscape scope a <see cref="TargetArchitectureDocument"/> governs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TargetScopeKind>))]
public enum TargetScopeKind
{
    /// <summary>The whole estate.</summary>
    [JsonStringEnumMemberName("estate")]
    Estate,

    /// <summary>One domain inside the estate.</summary>
    [JsonStringEnumMemberName("domain")]
    Domain,

    /// <summary>One capability area inside the estate.</summary>
    [JsonStringEnumMemberName("capability-area")]
    CapabilityArea
}

/// <summary>The lifecycle status of a <see cref="TargetVersion"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TargetVersionStatus>))]
public enum TargetVersionStatus
{
    /// <summary>Being authored; not yet proposed.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft,

    /// <summary>Proposed for review/approval.</summary>
    [JsonStringEnumMemberName("proposed")]
    Proposed,

    /// <summary>Approved, but not the currently active version.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved,

    /// <summary>The single effective target version for the scope.</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>A previously active or approved version replaced by a newer one.</summary>
    [JsonStringEnumMemberName("superseded")]
    Superseded,

    /// <summary>Explicitly rejected.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>Retained for history, no longer in active use.</summary>
    [JsonStringEnumMemberName("archived")]
    Archived
}

/// <summary>How a target member changes the live estate.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChangeIntent>))]
public enum ChangeIntent
{
    /// <summary>Add a new asset or relationship to the target state.</summary>
    [JsonStringEnumMemberName("add")]
    Add,

    /// <summary>Change an existing asset or relationship into its target state.</summary>
    [JsonStringEnumMemberName("change")]
    Change,

    /// <summary>Retire an existing asset or relationship from the target state.</summary>
    [JsonStringEnumMemberName("retire")]
    Retire
}

/// <summary>
/// A versioned, landscape-level To-Be target: the named target, its ordered versions, the status
/// vocabulary and optional transition/gap shapes. Product-native truth only: ArchiMate remains a
/// peripheral mapping (atlas-contracts#35).
/// </summary>
/// <param name="Id">Stable identifier for the target architecture.</param>
/// <param name="Name">Human-readable target name.</param>
/// <param name="Scope">The part of the estate this target governs.</param>
/// <param name="Versions">The ordered target versions. Exactly one must be <see cref="TargetVersionStatus.Active"/>.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Generator">Optional provenance: what produced this target document.</param>
/// <param name="Transitions">Optional intermediate transitions/gaps/work packages between versions.</param>
public sealed record TargetArchitectureDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("scope")] TargetScope Scope,
    ImmutableArray<TargetVersion> Versions,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("generator")] Generator? Generator = null,
    ImmutableArray<TargetTransition> Transitions = default)
{
    /// <summary>The atlas-contracts schema major version this document conforms to.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>Discriminates a Target Architecture document on the wire.</summary>
    [JsonPropertyName("kind")]
    public string Kind => "target-architecture";

    /// <summary>The target versions; never null.</summary>
    [JsonPropertyName("versions")]
    public ImmutableArray<TargetVersion> Versions { get; init; } = Versions.IsDefault ? [] : Versions;

    /// <summary>The optional transitions; never null.</summary>
    [JsonPropertyName("transitions")]
    public ImmutableArray<TargetTransition> Transitions { get; init; } = Transitions.IsDefault ? [] : Transitions;
}

/// <summary>What part of the estate a Target Architecture governs.</summary>
/// <param name="Kind">Estate, domain or capability area.</param>
/// <param name="Ref">Optional opaque identifier of the scoped thing.</param>
/// <param name="Name">Optional human-readable scope name.</param>
public sealed record TargetScope(
    [property: JsonPropertyName("kind")] TargetScopeKind Kind,
    [property: JsonPropertyName("ref")] string? Ref = null,
    [property: JsonPropertyName("name")] string? Name = null);

/// <summary>One ordered To-Be version within a target architecture.</summary>
/// <param name="Id">Stable identifier for the version.</param>
/// <param name="Name">Human-readable version name.</param>
/// <param name="Sequence">Order of the version within the target.</param>
/// <param name="Status">Lifecycle status.</param>
/// <param name="Rationale">Optional rationale for this version.</param>
/// <param name="IntendedHorizon">Optional human-readable horizon (e.g. Q4 2027).</param>
/// <param name="EffectiveDate">Optional effective date for the target version.</param>
/// <param name="AssetChanges">Asset-level intended changes.</param>
/// <param name="RelationshipChanges">Relationship-level intended changes.</param>
public sealed record TargetVersion(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("status")] TargetVersionStatus Status,
    [property: JsonPropertyName("rationale")] string? Rationale = null,
    [property: JsonPropertyName("intendedHorizon")] string? IntendedHorizon = null,
    [property: JsonPropertyName("effectiveDate")] DateOnly? EffectiveDate = null,
    ImmutableArray<TargetAssetMember> AssetChanges = default,
    ImmutableArray<TargetRelationshipMember> RelationshipChanges = default)
{
    /// <summary>Asset-level intended changes; never null.</summary>
    [JsonPropertyName("assetChanges")]
    public ImmutableArray<TargetAssetMember> AssetChanges { get; init; } = AssetChanges.IsDefault ? [] : AssetChanges;

    /// <summary>Relationship-level intended changes; never null.</summary>
    [JsonPropertyName("relationshipChanges")]
    public ImmutableArray<TargetRelationshipMember> RelationshipChanges { get; init; } =
        RelationshipChanges.IsDefault ? [] : RelationshipChanges;
}

/// <summary>One asset-level intended change in a target version or gap.</summary>
/// <param name="Intent">How the asset changes.</param>
/// <param name="AssetRef">Reference to an existing asset when changing or retiring.</param>
/// <param name="Asset">The target-state asset when adding or changing.</param>
/// <param name="Rationale">Optional rationale for the change.</param>
public sealed record TargetAssetMember(
    [property: JsonPropertyName("intent")] ChangeIntent Intent,
    [property: JsonPropertyName("assetRef")] string? AssetRef = null,
    [property: JsonPropertyName("asset")] Asset? Asset = null,
    [property: JsonPropertyName("rationale")] string? Rationale = null);

/// <summary>One relationship-level intended change in a target version or gap.</summary>
/// <param name="Intent">How the relationship changes.</param>
/// <param name="RelationshipRef">Reference to an existing relationship when changing or retiring.</param>
/// <param name="Relationship">The target-state relationship when adding or changing.</param>
/// <param name="Rationale">Optional rationale for the change.</param>
public sealed record TargetRelationshipMember(
    [property: JsonPropertyName("intent")] ChangeIntent Intent,
    [property: JsonPropertyName("relationshipRef")] string? RelationshipRef = null,
    [property: JsonPropertyName("relationship")] Relationship? Relationship = null,
    [property: JsonPropertyName("rationale")] string? Rationale = null);

/// <summary>Optional intermediate transition architecture between two target versions.</summary>
/// <param name="Id">Stable identifier for the transition.</param>
/// <param name="Name">Human-readable transition name.</param>
/// <param name="FromVersionId">Version the transition starts from.</param>
/// <param name="ToVersionId">Version the transition ends at.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Gaps">Optional gap slices inside the transition.</param>
/// <param name="WorkPackages">Optional work packages/deliverables for the transition.</param>
public sealed record TargetTransition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("fromVersionId")] string FromVersionId,
    [property: JsonPropertyName("toVersionId")] string ToVersionId,
    [property: JsonPropertyName("description")] string? Description = null,
    ImmutableArray<TargetGap> Gaps = default,
    ImmutableArray<WorkPackage> WorkPackages = default)
{
    /// <summary>Gap slices; never null.</summary>
    [JsonPropertyName("gaps")]
    public ImmutableArray<TargetGap> Gaps { get; init; } = Gaps.IsDefault ? [] : Gaps;

    /// <summary>Work packages; never null.</summary>
    [JsonPropertyName("workPackages")]
    public ImmutableArray<WorkPackage> WorkPackages { get; init; } = WorkPackages.IsDefault ? [] : WorkPackages;
}

/// <summary>One optional intermediate gap slice between two target versions.</summary>
/// <param name="Id">Stable identifier for the gap.</param>
/// <param name="Name">Human-readable gap name.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="AssetChanges">Asset-level intended changes in this gap.</param>
/// <param name="RelationshipChanges">Relationship-level intended changes in this gap.</param>
public sealed record TargetGap(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    ImmutableArray<TargetAssetMember> AssetChanges = default,
    ImmutableArray<TargetRelationshipMember> RelationshipChanges = default)
{
    /// <summary>Asset-level intended changes in this gap; never null.</summary>
    [JsonPropertyName("assetChanges")]
    public ImmutableArray<TargetAssetMember> AssetChanges { get; init; } = AssetChanges.IsDefault ? [] : AssetChanges;

    /// <summary>Relationship-level intended changes in this gap; never null.</summary>
    [JsonPropertyName("relationshipChanges")]
    public ImmutableArray<TargetRelationshipMember> RelationshipChanges { get; init; } =
        RelationshipChanges.IsDefault ? [] : RelationshipChanges;
}

/// <summary>A transition work package grouping one or more deliverables.</summary>
/// <param name="Id">Stable identifier for the work package.</param>
/// <param name="Name">Human-readable work package name.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Deliverables">The deliverables this work package owns.</param>
public sealed record WorkPackage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    ImmutableArray<Deliverable> Deliverables = default)
{
    /// <summary>The deliverables this work package owns; never null.</summary>
    [JsonPropertyName("deliverables")]
    public ImmutableArray<Deliverable> Deliverables { get; init; } = Deliverables.IsDefault ? [] : Deliverables;
}

/// <summary>One deliverable within a transition work package.</summary>
/// <param name="Id">Stable identifier for the deliverable.</param>
/// <param name="Name">Human-readable deliverable name.</param>
/// <param name="Description">Optional free-text description.</param>
public sealed record Deliverable(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null);
