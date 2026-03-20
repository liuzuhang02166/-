using System;

namespace SchedulerApp.Models;

public sealed record Course(
    string Id,
    string StudentName,
    string Content,
    string TeacherId,
    DateOnly StartDate,
    DateOnly? EndDate,
    int Weekday,
    int StartMinute,
    int EndMinute,
    string Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

