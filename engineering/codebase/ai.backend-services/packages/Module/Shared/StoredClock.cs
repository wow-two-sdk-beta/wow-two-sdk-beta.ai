namespace WoW.Two.Sdk.Beta.Be.Ai.Module.Shared;

/// <summary>Reads the clock at the precision the store keeps, so what a write returns equals what a later read returns.</summary>
/// <remarks>PostgreSQL's <c>timestamptz</c> keeps microseconds; an untruncated instant would come back one tick different.</remarks>
internal static class StoredClock
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>Gets the current UTC instant, truncated to whole microseconds.</summary>
    /// <param name="time">The clock.</param>
    public static DateTimeOffset StoredUtcNow(this TimeProvider time) => Truncate(time.GetUtcNow());

    /// <summary>Converts an instant to UTC, truncated to whole microseconds, as the store keeps it.</summary>
    /// <param name="instant">The instant.</param>
    public static DateTimeOffset Truncate(DateTimeOffset instant) =>
        new(instant.UtcTicks - (instant.UtcTicks % TicksPerMicrosecond), TimeSpan.Zero);
}
