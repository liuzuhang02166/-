using System;

namespace SchedulerApp.Models;

public enum OverrideKind
{
    Move = 0,
    Cancel = 1,
    Add = 2
}

public sealed record OverrideEntry(
    string Id,
    OverrideKind Kind,
    DateOnly Date,
    string? CourseId,
    string? FromTeacherId,
    string? ToTeacherId,
    string? StudentName,
    string? Content,
    int? StartMinute,
    int? EndMinute,
    string Note,
    bool IsForced,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
