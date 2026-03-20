using System;

namespace SchedulerApp.Models;

public sealed record WeekNote(
    DateOnly WeekStart,
    string Notes,
    DateTimeOffset UpdatedAt
);
