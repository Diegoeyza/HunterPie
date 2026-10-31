using System;

namespace HunterPie.Features.Statistics.Models;

internal record TimeFrameModel(
    DateTime StartedAt,
    DateTime FinishedAt
)
{
    public static TimeFrameModel Start() => new TimeFrameModel(
        StartedAt: DateTime.UtcNow,
        FinishedAt: DateTime.MaxValue
    );

    /// <summary>
    /// Opens a window at a known time (used when a status is first discovered
    /// mid-activation and its elapsed timer tells us when it really started).
    /// </summary>
    public static TimeFrameModel Start(DateTime startedAt) => new TimeFrameModel(
        StartedAt: startedAt,
        FinishedAt: DateTime.MaxValue
    );

    public TimeFrameModel End() => IsRunning() ? this with { FinishedAt = DateTime.UtcNow } : this;

    public bool IsRunning() => FinishedAt == DateTime.MaxValue;
}