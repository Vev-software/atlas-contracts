using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>Digest classification derived from asset kind, independent of catalogue layers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DigestLayer>))]
public enum DigestLayer
{
    /// <summary>Reserved for future business items.</summary>
    [JsonStringEnumMemberName("business")] Business,
    /// <summary>Systems and applications.</summary>
    [JsonStringEnumMemberName("application")] Application,
    /// <summary>Reserved for future data items.</summary>
    [JsonStringEnumMemberName("data")] Data,
    /// <summary>Servers, infrastructure and AI substrate.</summary>
    [JsonStringEnumMemberName("infrastructure")] Infrastructure
}

/// <summary>Closed digest relation vocabulary; integrates requires separate stage 2 consent.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DigestRelationKind>))]
public enum DigestRelationKind
{
    /// <summary>A system/application runs on a platform.</summary>
    [JsonStringEnumMemberName("runsOn")] RunsOn,
    /// <summary>A component is supplied by a vendor item in the same digest.</summary>
    [JsonStringEnumMemberName("suppliedBy")] SuppliedBy,
    /// <summary>Undirected system integration; contract-only stage 2.</summary>
    [JsonStringEnumMemberName("integrates")] Integrates
}

/// <summary>Signed inclusion scope. Relations default off; this payload is never authorization.</summary>
public sealed record LandscapeDigestScopeV2(
    ImmutableArray<DigestKind> Kinds,
    ImmutableArray<Tag> Tags = default,
    [property: JsonPropertyName("relations")] bool Relations = false)
{
    /// <summary>Included kinds.</summary>
    [JsonPropertyName("kinds")]
    public ImmutableArray<DigestKind> Kinds { get; init; } = Kinds.IsDefault ? [] : Kinds;
    /// <summary>Included tag filters.</summary>
    [JsonPropertyName("tags")]
    public ImmutableArray<Tag> Tags { get; init; } = Tags.IsDefault ? [] : Tags;
}

/// <summary>Minimized v2 item. Ref is minted per digest, never a stable asset id.</summary>
public sealed record LandscapeDigestItemV2(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("kind")] DigestKind Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lifecycle")] Lifecycle Lifecycle,
    [property: JsonPropertyName("vendor")] string? Vendor = null,
    [property: JsonPropertyName("plannedChange")] PlannedChange? PlannedChange = null,
    [property: JsonPropertyName("integrationCount")] int? IntegrationCount = null,
    [property: JsonPropertyName("layer")] DigestLayer? Layer = null);

/// <summary>Only endpoint refs and kind can travel on an edge.</summary>
public sealed record LandscapeDigestRelation(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("kind")] DigestRelationKind Kind);

/// <summary>Independent v2 sharing surface; v1 types and schemas remain available unchanged.</summary>
public sealed record LandscapeDigestV2(
    [property: JsonPropertyName("digestId")] string DigestId,
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("sourceInstanceId")] string SourceInstanceId,
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("scope")] LandscapeDigestScopeV2 Scope,
    ImmutableArray<LandscapeDigestItemV2> Items = default,
    [property: JsonPropertyName("relations")] ImmutableArray<LandscapeDigestRelation>? Relations = null)
{
    /// <summary>The digest schema version, independent of other Atlas contracts.</summary>
    [JsonPropertyName("contractVersion")]
    public string ContractVersion => "2";
    /// <summary>Minimized items, never null.</summary>
    [JsonPropertyName("items")]
    public ImmutableArray<LandscapeDigestItemV2> Items { get; init; } = Items.IsDefault ? [] : Items;
}

/// <summary>Cross-item validation to run after JSON Schema validation, before accepting a digest.</summary>
public static class LandscapeDigestV2Validation
{
    /// <summary>Returns semantic errors; does not grant permission to share or consume relations.</summary>
    public static ImmutableArray<string> Validate(LandscapeDigestV2 digest)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var items = new Dictionary<string, LandscapeDigestItemV2>(StringComparer.Ordinal);
        foreach (var item in digest.Items)
        {
            if (!items.TryAdd(item.Ref, item)) errors.Add("duplicate-ref");
            if (!digest.Scope.Kinds.Contains(item.Kind)) errors.Add("item-outside-scope");
        }

        if (digest.Relations is not { } relations) return errors.ToImmutable();
        if (!digest.Scope.Relations) errors.Add("relations-not-enabled");
        if (relations.Length > 20000) errors.Add("too-many-relations");
        var edges = new HashSet<(DigestRelationKind Kind, string From, string To)>();
        foreach (var edge in relations)
        {
            if (edge.From == edge.To) errors.Add("self-edge");
            var from = edge.From;
            var to = edge.To;
            if (edge.Kind == DigestRelationKind.Integrates && string.CompareOrdinal(from, to) > 0)
                (from, to) = (to, from);
            if (!edges.Add((edge.Kind, from, to))) errors.Add("duplicate-edge");
            if (!items.TryGetValue(edge.From, out var source) || !items.TryGetValue(edge.To, out var target))
            {
                errors.Add("dangling-ref");
                continue;
            }

            var validEndpoints = edge.Kind switch
            {
                DigestRelationKind.RunsOn => IsSystem(source.Kind) && target.Kind == DigestKind.Platform,
                DigestRelationKind.SuppliedBy => source.Kind != DigestKind.Vendor && target.Kind == DigestKind.Vendor,
                DigestRelationKind.Integrates => IsSystem(source.Kind) && IsSystem(target.Kind),
                _ => false
            };
            if (!validEndpoints) errors.Add("invalid-relation-endpoints");
        }

        return errors.ToImmutable();
    }

    private static bool IsSystem(DigestKind kind) => kind is DigestKind.System or DigestKind.Application;
}
