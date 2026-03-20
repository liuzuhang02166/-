using System;

namespace SchedulerApp.Models;

public sealed record Teacher(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    string? ColorHex
);

