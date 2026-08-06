using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>A manual, lightweight classification on an asset: a key with an optional value.</summary>
/// <param name="Key">The tag key. Required.</param>
/// <param name="Value">The optional tag value.</param>
public sealed record Tag(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string? Value = null);
