using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>Catalogue lifecycle state of an asset. Held metadata, not analysis (handbook 11 §1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Lifecycle>))]
public enum Lifecycle
{
    /// <summary>Being catalogued; not yet confirmed part of the landscape.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft,

    /// <summary>An active part of the landscape.</summary>
    [JsonStringEnumMemberName("active")]
    Active,

    /// <summary>Retired / decommissioned; kept for the historical record.</summary>
    [JsonStringEnumMemberName("retired")]
    Retired
}
