using System.Collections.Generic;

namespace SchedulerApp.ViewModels;

public sealed record TeacherColorOption(string Name, string? Hex);

public static class TeacherColorOptions
{
    public static IReadOnlyList<TeacherColorOption> All { get; } =
    [
        new("自动（默认）", null),
        new("红", "#EF4444"),
        new("深红", "#DC2626"),
        new("玫红", "#EC4899"),
        new("粉", "#F472B6"),
        new("紫", "#A855F7"),
        new("深紫", "#7C3AED"),
        new("靛", "#6366F1"),
        new("蓝", "#3B82F6"),
        new("深蓝", "#2563EB"),
        new("天蓝", "#0EA5E9"),
        new("青", "#06B6D4"),
        new("蓝绿", "#14B8A6"),
        new("青绿", "#10B981"),
        new("绿", "#22C55E"),
        new("深绿", "#16A34A"),
        new("黄绿", "#84CC16"),
        new("黄", "#EAB308"),
        new("琥珀", "#F59E0B"),
        new("橙", "#F97316"),
        new("深橙", "#EA580C"),
        new("棕", "#A16207")
    ];
}

