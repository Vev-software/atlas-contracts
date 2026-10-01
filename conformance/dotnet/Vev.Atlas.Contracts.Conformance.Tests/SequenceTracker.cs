namespace Vev.Atlas.Contracts.Conformance.Tests;

/// <summary>
/// Consumer-side replay protection for the landscape share digest. Tracks the highest sequence
/// accepted per opaque <c>sourceInstanceId</c> and rejects any digest whose sequence is at or
/// below it. Sequence state is scoped per source: a different source starts its own monotonic
/// line, and a source that resets must mint a new sourceInstanceId.
/// </summary>
internal sealed class SequenceTracker
{
    private readonly Dictionary<string, int> _lastAccepted = new(StringComparer.Ordinal);

    /// <summary>
    /// Attempts to accept a digest with the given sequence for the given source. Returns true and
    /// records the sequence when it is strictly higher than the last accepted for that source
    /// (or the first seen); returns false for a replay (sequence at or below the last accepted).
    /// </summary>
    public bool TryAccept(string sourceInstanceId, int sequence)
    {
        if (_lastAccepted.TryGetValue(sourceInstanceId, out var last) && sequence <= last)
        {
            return false;
        }

        _lastAccepted[sourceInstanceId] = sequence;
        return true;
    }
}
