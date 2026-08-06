using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>
/// The manual relationship vocabulary between two assets. Held data, not derived analysis
/// (automatic integration mapping is paid Atlas core — handbook 11 §1). Adding a value is
/// non-breaking; changing the meaning of an existing value is not.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RelationshipType>))]
public enum RelationshipType
{
    /// <summary>Source runs on target (e.g. application → server).</summary>
    [JsonStringEnumMemberName("runs-on")]
    RunsOn,

    /// <summary>Source hosts target (e.g. server → application).</summary>
    [JsonStringEnumMemberName("hosts")]
    Hosts,

    /// <summary>Source connects to target (a manual link, not a measured integration).</summary>
    [JsonStringEnumMemberName("connects-to")]
    ConnectsTo,

    /// <summary>Source depends on target.</summary>
    [JsonStringEnumMemberName("depends-on")]
    DependsOn,

    /// <summary>Source is part of target (e.g. application → system).</summary>
    [JsonStringEnumMemberName("part-of")]
    PartOf
}
