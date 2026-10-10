using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Vev.Atlas.Contracts;

/// <summary>Planned and actual calendar-date intervals. Starts are inclusive, ends exclusive; null means unknown.</summary>
/// <param name="PlannedStart">Planned first day in use.</param>
/// <param name="PlannedEnd">Planned first day out of use.</param>
/// <param name="ActualStart">Actual first day in use.</param>
/// <param name="ActualEnd">Actual first day out of use.</param>
public sealed record InUsePeriod(
    [property: JsonPropertyName("plannedStart")] DateOnly? PlannedStart = null,
    [property: JsonPropertyName("plannedEnd")] DateOnly? PlannedEnd = null,
    [property: JsonPropertyName("actualStart")] DateOnly? ActualStart = null,
    [property: JsonPropertyName("actualEnd")] DateOnly? ActualEnd = null);

/// <summary>Pair ordering beyond JSON Schema. Validate raw JSON first; lifecycle is independent.</summary>
public static class InUsePeriodValidation
{
    /// <summary>Returns ordering errors; omitted periods and unknown boundaries are valid.</summary>
    public static ImmutableArray<string> Validate(InUsePeriod? period)
    {
        if (period is null) return [];
        var errors = ImmutableArray.CreateBuilder<string>();
        if (period.PlannedStart > period.PlannedEnd)
            errors.Add("plannedStart must be on or before plannedEnd.");
        if (period.ActualStart > period.ActualEnd)
            errors.Add("actualStart must be on or before actualEnd.");
        return errors.ToImmutable();
    }
}
