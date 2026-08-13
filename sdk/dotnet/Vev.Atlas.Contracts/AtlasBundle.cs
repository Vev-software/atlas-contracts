using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>
/// The public, versioned portable Atlas bundle: a self-contained migration package layered ON TOP
/// of the landscape export (<see cref="LandscapeDocument"/>). It carries the resolved landscape plus
/// the Atlas-domain metadata a hosted &lt;-&gt; self-hosted move needs — workspace metadata, diagrams,
/// an attachment manifest, public module/extension data and restore hints — without becoming a
/// private hosted-only escape hatch (handbook 11 §3, 11 §7, 12 §Phase C, ADR 0003).
/// It carries Atlas domain data and migration metadata only: NEVER entitlement/subscription/billing
/// state, live identity credentials or raw secret values.
/// </summary>
/// <param name="Manifest">Machine-checkable inventory of what the bundle carries.</param>
/// <param name="Landscape">The resolved landscape core every other reference points into.</param>
/// <param name="CreatedAt">When the bundle was produced, if known.</param>
/// <param name="Generator">Optional provenance: what produced this bundle.</param>
/// <param name="Compatibility">Producer-declared compatibility floor and provenance.</param>
/// <param name="Workspace">Held, portable workspace display metadata.</param>
/// <param name="Diagrams">Portable diagrams: held layouts over the landscape.</param>
/// <param name="Attachments">Attachment manifest: references + integrity digests, never inline bytes.</param>
/// <param name="Modules">Public module / extension data sections.</param>
/// <param name="Restore">Restore hints: identity-mapping placeholders and secret-rebind instructions.</param>
/// <param name="Excluded">The non-portable categories the producer asserts it left out.</param>
public sealed record AtlasBundle(
    [property: JsonPropertyName("manifest")] BundleManifest Manifest,
    [property: JsonPropertyName("landscape")] LandscapeDocument Landscape,
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt = null,
    [property: JsonPropertyName("generator")] Generator? Generator = null,
    [property: JsonPropertyName("compatibility")] BundleCompatibility? Compatibility = null,
    [property: JsonPropertyName("workspace")] WorkspaceMetadata? Workspace = null,
    ImmutableArray<Diagram> Diagrams = default,
    ImmutableArray<AttachmentManifestEntry> Attachments = default,
    ImmutableArray<ModuleData> Modules = default,
    [property: JsonPropertyName("restore")] RestoreHints? Restore = null,
    ImmutableArray<ExcludedCategory> Excluded = default)
{
    /// <summary>The atlas-contracts schema major version this bundle conforms to.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>Discriminates a portable bundle from the import/export/discovery documents.</summary>
    [JsonPropertyName("kind")]
    public string Kind => "atlas-bundle";

    /// <summary>Portable diagrams; never null (defaults to empty).</summary>
    [JsonPropertyName("diagrams")]
    public ImmutableArray<Diagram> Diagrams { get; init; } = Diagrams.IsDefault ? [] : Diagrams;

    /// <summary>Attachment manifest; never null (defaults to empty).</summary>
    [JsonPropertyName("attachments")]
    public ImmutableArray<AttachmentManifestEntry> Attachments { get; init; } =
        Attachments.IsDefault ? [] : Attachments;

    /// <summary>Public module sections; never null (defaults to empty).</summary>
    [JsonPropertyName("modules")]
    public ImmutableArray<ModuleData> Modules { get; init; } = Modules.IsDefault ? [] : Modules;

    /// <summary>Deliberately excluded categories; never null (defaults to empty).</summary>
    [JsonPropertyName("excluded")]
    public ImmutableArray<ExcludedCategory> Excluded { get; init; } = Excluded.IsDefault ? [] : Excluded;

    /// <summary>
    /// The differences between the declared <see cref="BundleManifest"/> counts and the actual
    /// section sizes. Empty means the manifest is honest. JSON Schema fixes the manifest shape but
    /// cannot express "the counts match"; this is that bundle-level rule.
    /// </summary>
    public ImmutableArray<string> ManifestErrors()
    {
        var errors = ImmutableArray.CreateBuilder<string>();

        void Check(string name, int declared, int actual)
        {
            if (declared != actual)
                errors.Add($"manifest.{name} is {declared} but the bundle carries {actual}");
        }

        Check("landscape.assetCount", Manifest.Landscape.AssetCount, Landscape.Assets.Length);
        Check("landscape.relationshipCount", Manifest.Landscape.RelationshipCount, Landscape.Relationships.Length);
        Check("diagramCount", Manifest.DiagramCount, Diagrams.Length);
        Check("attachmentCount", Manifest.AttachmentCount, Attachments.Length);
        Check("moduleCount", Manifest.ModuleCount, Modules.Length);

        return errors.ToImmutable();
    }

    /// <summary>
    /// The distinct references from diagrams and attachments that do not resolve to an id present in
    /// this bundle — a diagram node pointing at a missing asset, an edge at a missing relationship, a
    /// diagram render or attachment pointing at nothing. Empty means the bundle is internally
    /// self-consistent. This is the reference invariant a self-contained portable bundle relies on.
    /// </summary>
    public ImmutableArray<string> UnresolvedReferences()
    {
        var assetIds = Landscape.Assets.Select(a => a.Id).ToImmutableHashSet(StringComparer.Ordinal);
        var relationshipIds = Landscape.Relationships.Select(r => r.Id).ToImmutableHashSet(StringComparer.Ordinal);
        var diagramIds = Diagrams.Select(d => d.Id).ToImmutableHashSet(StringComparer.Ordinal);
        var attachmentIds = Attachments.Select(a => a.Id).ToImmutableHashSet(StringComparer.Ordinal);

        var unresolved = ImmutableArray.CreateBuilder<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Require(bool present, string reference)
        {
            if (!present && seen.Add(reference))
                unresolved.Add(reference);
        }

        foreach (var diagram in Diagrams)
        {
            foreach (var node in diagram.Nodes)
                Require(assetIds.Contains(node.Ref), node.Ref);
            foreach (var edge in diagram.Edges)
                Require(relationshipIds.Contains(edge.Ref), edge.Ref);
            if (diagram.RenderRef is not null)
                Require(attachmentIds.Contains(diagram.RenderRef), diagram.RenderRef);
        }

        foreach (var attachment in Attachments)
        {
            if (attachment.AttachedToRef is not null)
            {
                var resolves = assetIds.Contains(attachment.AttachedToRef)
                    || relationshipIds.Contains(attachment.AttachedToRef)
                    || diagramIds.Contains(attachment.AttachedToRef);
                Require(resolves, attachment.AttachedToRef);
            }
        }

        return unresolved.ToImmutable();
    }
}

/// <summary>Machine-checkable inventory of what an <see cref="AtlasBundle"/> carries.</summary>
/// <param name="Landscape">Counts for the embedded landscape core.</param>
/// <param name="DiagramCount">Number of diagrams in the bundle.</param>
/// <param name="AttachmentCount">Number of attachment manifest entries.</param>
/// <param name="ModuleCount">Number of public module sections.</param>
public sealed record BundleManifest(
    [property: JsonPropertyName("landscape")] LandscapeCounts Landscape,
    [property: JsonPropertyName("diagramCount")] int DiagramCount = 0,
    [property: JsonPropertyName("attachmentCount")] int AttachmentCount = 0,
    [property: JsonPropertyName("moduleCount")] int ModuleCount = 0);

/// <summary>Counts for the embedded landscape core.</summary>
/// <param name="AssetCount">Number of assets.</param>
/// <param name="RelationshipCount">Number of manual relationships.</param>
public sealed record LandscapeCounts(
    [property: JsonPropertyName("assetCount")] int AssetCount,
    [property: JsonPropertyName("relationshipCount")] int RelationshipCount);

/// <summary>Producer-declared compatibility floor and provenance for an <see cref="AtlasBundle"/>.</summary>
/// <param name="ProducerAtlasVersion">The Atlas runtime version that produced the bundle, if known.</param>
/// <param name="RequiredModules">Public modules the destination should have to restore module sections.</param>
public sealed record BundleCompatibility(
    [property: JsonPropertyName("producerAtlasVersion")] string? ProducerAtlasVersion = null,
    ImmutableArray<ModuleRequirement> RequiredModules = default)
{
    /// <summary>
    /// The minimum atlas-contracts schema major version a reader must implement to safely import.
    /// For v1 this is the schema major; an older reader must refuse rather than silently drop data.
    /// </summary>
    [JsonPropertyName("minReaderContractVersion")]
    public string MinReaderContractVersion => AtlasContracts.SchemaMajorVersion;

    /// <summary>Required modules; never null (defaults to empty).</summary>
    [JsonPropertyName("requiredModules")]
    public ImmutableArray<ModuleRequirement> RequiredModules { get; init; } =
        RequiredModules.IsDefault ? [] : RequiredModules;
}

/// <summary>A public module the destination should have to consume a module data section.</summary>
/// <param name="Id">The public module id.</param>
/// <param name="MinVersion">The minimum public data-contract version of the module the destination needs.</param>
public sealed record ModuleRequirement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("minVersion")] string? MinVersion = null);

/// <summary>
/// Portable workspace display metadata. Presentation only: NO tenant shell, subscription, entitlement,
/// trial or billing state (Fabric/control-plane, recreated or remapped at the destination).
/// </summary>
/// <param name="Name">The workspace display name.</param>
/// <param name="Slug">A stable, URL-safe short name, if any.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Locale">Preferred display locale (BCP-47), a presentation preference.</param>
/// <param name="TimeZone">Preferred display time zone (IANA tz id).</param>
/// <param name="Tags">Manual classification tags.</param>
public sealed record WorkspaceMetadata(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string? Slug = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("locale")] string? Locale = null,
    [property: JsonPropertyName("timeZone")] string? TimeZone = null,
    ImmutableArray<Tag> Tags = default)
{
    /// <summary>Manual classification tags; never null (defaults to empty).</summary>
    [JsonPropertyName("tags")]
    public ImmutableArray<Tag> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}

/// <summary>The layout format of a <see cref="Diagram"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DiagramFormat>))]
public enum DiagramFormat
{
    /// <summary>Held node/edge placement over the landscape.</summary>
    [JsonStringEnumMemberName("atlas-layout")]
    AtlasLayout
}

/// <summary>
/// A portable diagram: a held visual layout over the landscape. Node placements reference asset ids
/// and edges reference relationship ids present in the embedded landscape; a rendered image, if any,
/// is carried as an attachment (via <see cref="RenderRef"/>), not inline.
/// </summary>
/// <param name="Id">Stable diagram identifier, unique within the bundle.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Format">The layout format.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="Nodes">Placed assets.</param>
/// <param name="Edges">Drawn relationships.</param>
/// <param name="RenderRef">Optional attachment id of a rendered image of this diagram.</param>
public sealed record Diagram(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("format")] DiagramFormat Format,
    [property: JsonPropertyName("description")] string? Description = null,
    ImmutableArray<DiagramNode> Nodes = default,
    ImmutableArray<DiagramEdge> Edges = default,
    [property: JsonPropertyName("renderRef")] string? RenderRef = null)
{
    /// <summary>Placed assets; never null (defaults to empty).</summary>
    [JsonPropertyName("nodes")]
    public ImmutableArray<DiagramNode> Nodes { get; init; } = Nodes.IsDefault ? [] : Nodes;

    /// <summary>Drawn relationships; never null (defaults to empty).</summary>
    [JsonPropertyName("edges")]
    public ImmutableArray<DiagramEdge> Edges { get; init; } = Edges.IsDefault ? [] : Edges;
}

/// <summary>A placed asset in a diagram. Position is a held layout fact, not analysis.</summary>
/// <param name="Ref">The asset id this node draws, present in the embedded landscape.</param>
/// <param name="X">Horizontal position.</param>
/// <param name="Y">Vertical position.</param>
/// <param name="Label">Optional display label override.</param>
public sealed record DiagramNode(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("label")] string? Label = null);

/// <summary>A drawn relationship in a diagram.</summary>
/// <param name="Ref">The relationship id this edge draws, present in the embedded landscape.</param>
public sealed record DiagramEdge(
    [property: JsonPropertyName("ref")] string Ref);

/// <summary>The digest algorithm of a <see cref="Digest"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DigestAlgorithm>))]
public enum DigestAlgorithm
{
    /// <summary>SHA-256, the required baseline.</summary>
    [JsonStringEnumMemberName("sha-256")]
    Sha256
}

/// <summary>A content digest so the destination can verify out-of-band attachment bytes.</summary>
/// <param name="Algorithm">The digest algorithm.</param>
/// <param name="Value">Lower-case hex digest.</param>
public sealed record Digest(
    [property: JsonPropertyName("algorithm")] DigestAlgorithm Algorithm,
    [property: JsonPropertyName("value")] string Value);

/// <summary>
/// One attachment listed by reference and integrity digest — never inline secret bytes. The bytes
/// travel out of band; this entry lets the destination locate and verify them.
/// </summary>
/// <param name="Id">Stable attachment identifier, unique within the bundle.</param>
/// <param name="Name">The attachment's file name.</param>
/// <param name="MediaType">IANA media type.</param>
/// <param name="ByteSize">Size of the attachment in bytes.</param>
/// <param name="Digest">Content digest for verification.</param>
/// <param name="AttachedToRef">Optional id of the asset, relationship or diagram this belongs to.</param>
public sealed record AttachmentManifestEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mediaType")] string MediaType,
    [property: JsonPropertyName("byteSize")] long ByteSize,
    [property: JsonPropertyName("digest")] Digest Digest,
    [property: JsonPropertyName("attachedToRef")] string? AttachedToRef = null);

/// <summary>
/// A public module / extension data section. The bundle contract fixes only the envelope (which
/// module, which of ITS contract versions); the payload shape is governed by that module's own public
/// contract and is opaque here. The same exclusions apply — no secrets or entitlement state.
/// </summary>
/// <param name="Id">The public module id.</param>
/// <param name="Version">The version of the module's OWN public data contract governing <paramref name="Data"/>.</param>
/// <param name="ItemCount">Optional count of items in <paramref name="Data"/>.</param>
/// <param name="Data">The module's portable payload, governed by the module's own public contract.</param>
public sealed record ModuleData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("itemCount")] int? ItemCount = null,
    [property: JsonPropertyName("data")] JsonObject? Data = null);

/// <summary>
/// Non-data instructions for reconstructing the workspace at the destination. Placeholders and
/// instructions only: this section NEVER carries password/session material or secret values.
/// </summary>
/// <param name="IdentityMappings">Source-to-destination identity-mapping placeholders.</param>
/// <param name="SecretRebinds">Secret-rebind instructions, by opaque reference.</param>
/// <param name="Notes">Free-text operator notes for the restore.</param>
public sealed record RestoreHints(
    ImmutableArray<IdentityMapping> IdentityMappings = default,
    ImmutableArray<SecretRebind> SecretRebinds = default,
    [property: JsonPropertyName("notes")] string? Notes = null)
{
    /// <summary>Identity-mapping placeholders; never null (defaults to empty).</summary>
    [JsonPropertyName("identityMappings")]
    public ImmutableArray<IdentityMapping> IdentityMappings { get; init; } =
        IdentityMappings.IsDefault ? [] : IdentityMappings;

    /// <summary>Secret-rebind instructions; never null (defaults to empty).</summary>
    [JsonPropertyName("secretRebinds")]
    public ImmutableArray<SecretRebind> SecretRebinds { get; init; } =
        SecretRebinds.IsDefault ? [] : SecretRebinds;
}

/// <summary>One identity-mapping placeholder. Carries references and hints, never credentials.</summary>
/// <param name="SourceRef">An opaque reference to the source principal. Not an email, not a credential.</param>
/// <param name="DisplayName">Optional display name to help an operator match at the destination.</param>
/// <param name="BindHint">Optional hint for binding at the destination.</param>
public sealed record IdentityMapping(
    [property: JsonPropertyName("sourceRef")] string SourceRef,
    [property: JsonPropertyName("displayName")] string? DisplayName = null,
    [property: JsonPropertyName("bindHint")] string? BindHint = null);

/// <summary>One secret-rebind instruction. Names WHAT must be re-provided, never the value.</summary>
/// <param name="Ref">An opaque reference to the secret in the source. Not the secret itself.</param>
/// <param name="Instruction">How to re-provide the secret at the destination.</param>
public sealed record SecretRebind(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("instruction")] string? Instruction = null);

/// <summary>A category of state that is deliberately NOT portable through the bundle.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExcludedCategory>))]
public enum ExcludedCategory
{
    /// <summary>Secret values.</summary>
    [JsonStringEnumMemberName("secrets")]
    Secrets,

    /// <summary>Live identity credentials (passwords, sessions, tokens).</summary>
    [JsonStringEnumMemberName("live-credentials")]
    LiveCredentials,

    /// <summary>Entitlement state.</summary>
    [JsonStringEnumMemberName("entitlements")]
    Entitlements,

    /// <summary>Subscription state.</summary>
    [JsonStringEnumMemberName("subscription")]
    Subscription,

    /// <summary>Billing / accounting records.</summary>
    [JsonStringEnumMemberName("billing")]
    Billing,

    /// <summary>Audit records.</summary>
    [JsonStringEnumMemberName("audit-logs")]
    AuditLogs,

    /// <summary>Telemetry aggregates.</summary>
    [JsonStringEnumMemberName("telemetry")]
    Telemetry
}
