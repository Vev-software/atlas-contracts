using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>
/// A manual, catalogue-level typed link between two assets — held data expressing
/// "what runs on / connects to what". Not derived analysis (handbook 11 §1).
/// </summary>
/// <param name="Id">Stable identifier for the relationship.</param>
/// <param name="FromId">The source asset id.</param>
/// <param name="ToId">The target asset id.</param>
/// <param name="Type">The manual relationship type.</param>
/// <param name="Description">Optional free-text note on the link.</param>
public sealed record Relationship(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("fromId")] string FromId,
    [property: JsonPropertyName("toId")] string ToId,
    [property: JsonPropertyName("type")] RelationshipType Type,
    [property: JsonPropertyName("description")] string? Description = null);
