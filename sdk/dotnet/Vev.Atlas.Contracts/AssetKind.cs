using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>The kind of asset held in the Atlas catalogue.</summary>
/// <remarks>Catalogue concept only (handbook 11 §1). Serialised as the lowercase wire value.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<AssetKind>))]
public enum AssetKind
{
    /// <summary>An application-system: a grouping of applications delivering a business capability.</summary>
    [JsonStringEnumMemberName("system")]
    System,

    /// <summary>A deployable application.</summary>
    [JsonStringEnumMemberName("application")]
    Application,

    /// <summary>A physical or virtual server.</summary>
    [JsonStringEnumMemberName("server")]
    Server,

    /// <summary>An infrastructure item (network, storage, compute, …).</summary>
    [JsonStringEnumMemberName("infrastructure")]
    Infrastructure
}
