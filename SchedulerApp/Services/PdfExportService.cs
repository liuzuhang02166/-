using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Theme;

namespace SchedulerApp.Services;

public sealed class PdfExportService
{
    private readonly AppServices _services;

    public PdfExportService(AppServices services)
    {
        _services = services;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportWeek(string filePath, DateOnly weekStart, string? teacherIdFilter, bool includeWeekNotes)
    {
        ExportRange(filePath, weekStart, weekStart.AddDays(6), teacherIdFilter, includeWeekNotes);
    }

    public void ExportRange(string filePath, DateOnly startDate, DateOnly endDate, string? teacherIdFilter, bool includeWeekNotes)
    {
        if (endDate < startDate)
            throw new InvalidOperationException("结束日期不能早于开始日期。");

        var teachers = _services.Teachers.GetAll().ToList();
        var teacherById = teachers.ToDictionary(t => t.Id, t => t.Name);
        var teacherColorById = teachers.ToDictionary(t => t.Id, t => t.ColorHex);
        var courses = _services.Courses.GetAll().ToList();
        var coursesById = courses.ToDictionary(c => c.Id, c => c);
        var overrides = _services.Overrides.GetByDateRange(startDate, endDate).ToList();

        var isSingleWeek = endDate == startDate.AddDays(6) && ToWeekday1To7(startDate) == 1;
        var notes = includeWeekNotes && isSingleWeek ? _services.WeekNotes.Get(startDate)?.Notes ?? string.Empty : string.Empty;

        var teacherIds = teacherIdFilter is null
            ? teachers.Select(t => t.Id).ToList()
            : teachers.Where(t => t.Id == teacherIdFilter).Select(t => t.Id).ToList();

        var changes = BuildChangeLines(overrides, coursesById, teacherById);

        Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("排课表导出").FontSize(18).SemiBold();
                        col.Item().Text($"{startDate:yyyy.MM.dd} - {endDate:yyyy.MM.dd}").FontSize(12).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(12).Text("变动通知").FontSize(14).SemiBold();
                        if (changes.Count == 0)
                        {
                            col.Item().PaddingTop(6).Text("本时间段无临时调整。").FontColor(Colors.Grey.Darken2);
                        }
                        else
                        {
                            foreach (var line in changes)
                                col.Item().PaddingTop(4).Text("• " + line);
                        }

                        if (includeWeekNotes && isSingleWeek)
                        {
                            col.Item().PaddingTop(16).Text("本周备注").FontSize(14).SemiBold();
                            col.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(notes) ? "（无）" : notes);
                        }
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("第 ");
                        x.CurrentPageNumber();
                        x.Span(" 页");
                    });
                });

                foreach (var teacherId in teacherIds)
                {
                    var teacherName = teacherById.TryGetValue(teacherId, out var n) ? n : "未知老师";

                    var weeks = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1)
                        .Select(offset => startDate.AddDays(offset))
                        .Select(GetWeekStart)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();

                    foreach (var weekStart in weeks)
                    {
                        var weekEnd = weekStart.AddDays(6);
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(28);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            page.Header().Column(col =>
                            {
                                col.Item().Text($"{teacherName} · 课表").FontSize(16).SemiBold();
                                col.Item().Text($"{weekStart:yyyy.MM.dd} - {weekEnd:yyyy.MM.dd}").FontSize(12).FontColor(Colors.Grey.Darken2);
                                col.Item().Text($"导出范围：{startDate:yyyy.MM.dd} - {endDate:yyyy.MM.dd}").FontSize(10).FontColor(Colors.Grey.Darken1);
                            });

                            page.Content().PaddingTop(12).Row(row =>
                            {
                                row.Spacing(8);
                                for (var i = 0; i < 7; i++)
                                {
                                    var date = weekStart.AddDays(i);
                                    var weekday = i + 1;
                                    var inRange = date >= startDate && date <= endDate;
                                    var items = inRange
                                        ? ComputeOccurrencesForDate(date, teacherId, coursesById, overrides)
                                            .OrderBy(x => x.StartMinute)
                                            .ThenBy(x => x.StudentName, StringComparer.OrdinalIgnoreCase)
                                            .ToList()
                                        : [];

                                    row.RelativeItem().Element(cell =>
                                    {
                                        cell.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(dayCol =>
                                        {
                                            dayCol.Spacing(6);
                                            dayCol.Item().Text($"{date:MM.dd} {WeekdayUtil.ToChinese(weekday)}")
                                                .FontSize(10)
                                                .SemiBold()
                                                .FontColor(inRange ? Colors.Grey.Darken4 : Colors.Grey.Lighten1);

                                            if (items.Count == 0)
                                            {
                                                dayCol.Item().Text(inRange ? "（无课程）" : string.Empty).FontSize(9).FontColor(Colors.Grey.Darken1);
                                                return;
                                            }

                                            var sw = TeacherColorPalette.Get(teacherId, teacherColorById.GetValueOrDefault(teacherId));
                                            var itemBg = Q(sw.BackgroundR, sw.BackgroundG, sw.BackgroundB);
                                            var itemBorder = Q(sw.BorderR, sw.BorderG, sw.BorderB);
                                            foreach (var it in items)
                                            {
                                                var border = it.IsForced ? Q(252, 165, 165) : itemBorder;

                                                dayCol.Item().Border(1).BorderColor(border).Background(itemBg).Padding(6).Column(c =>
                                                {
                                                    c.Spacing(3);
                                                    c.Item().Row(r =>
                                                    {
                                                        r.RelativeItem().Text($"{TimeUtil.FormatMinutes(it.StartMinute)}-{TimeUtil.FormatMinutes(it.EndMinute)}")
                                                            .FontSize(8)
                                                            .FontColor(Colors.Grey.Darken2);
                                                        if (!string.IsNullOrWhiteSpace(it.Badge))
                                                        {
                                                            r.ConstantItem(54).AlignRight().Text(it.Badge)
                                                                .FontSize(8)
                                                                .FontColor(Colors.Grey.Darken2);
                                                        }
                                                    });
                                                    c.Item().Text(it.StudentName).FontSize(10).SemiBold();
                                                    c.Item().Text(it.Content).FontSize(9).FontColor(Colors.Grey.Darken3);
                                                    if (!string.IsNullOrWhiteSpace(it.Note))
                                                        c.Item().Text(it.Note).FontSize(8).FontColor(Colors.Grey.Darken1);
                                                });
                                            }
                                        });
                                    });
                                }
                            });

                            page.Footer().AlignRight().Text(x =>
                            {
                                x.Span("第 ");
                                x.CurrentPageNumber();
                                x.Span(" 页");
                            });
                        });
                    }
                }
            })
            .GeneratePdf(filePath);
    }

    private enum OccurrenceKind
    {
        Base = 0,
        Move = 1,
        Add = 2
    }

    private sealed record Occurrence(
        OccurrenceKind Kind,
        string StudentName,
        string Content,
        string Note,
        int StartMinute,
        int EndMinute,
        string Badge,
        bool IsForced
    );

    private static IReadOnlyList<Occurrence> ComputeOccurrencesForDate(
        DateOnly date,
        string teacherId,
        IReadOnlyDictionary<string, Course> coursesById,
        IReadOnlyList<OverrideEntry> overrides)
    {
        var weekday = ToWeekday1To7(date);

        var baseItems = coursesById.Values
            .Where(c => c.TeacherId == teacherId)
            .Where(c => c.Weekday == weekday)
            .Where(c => date >= c.StartDate)
            .Where(c => c.EndDate is null || date <= c.EndDate.Value)
            .Select(c => new Occurrence(OccurrenceKind.Base, c.StudentName, c.Content, c.Note, c.StartMinute, c.EndMinute, string.Empty, false))
            .ToList();

        var dayOverrides = overrides.Where(o => o.Date == date).ToList();
        foreach (var o in dayOverrides)
        {
            if (o.Kind == OverrideKind.Cancel && o.CourseId is not null)
            {
                if (coursesById.TryGetValue(o.CourseId, out var src) && src.TeacherId == teacherId)
                    baseItems.RemoveAll(x => x.Kind == OccurrenceKind.Base && x.StudentName == src.StudentName && x.StartMinute == src.StartMinute && x.EndMinute == src.EndMinute && x.Content == src.Content);
                continue;
            }

            if (o.Kind == OverrideKind.Move && o.CourseId is not null)
            {
                if (coursesById.TryGetValue(o.CourseId, out var src))
                {
                    if (src.TeacherId == teacherId)
                        baseItems.RemoveAll(x => x.Kind == OccurrenceKind.Base && x.StudentName == src.StudentName && x.StartMinute == src.StartMinute && x.EndMinute == src.EndMinute && x.Content == src.Content);
                }

                var toTeacherId = o.ToTeacherId ?? src?.TeacherId;
                if (toTeacherId != teacherId)
                    continue;

                var badge = o.IsForced ? "窜课·冲突" : "窜课";
                baseItems.Add(new Occurrence(
                    OccurrenceKind.Move,
                    o.StudentName ?? src?.StudentName ?? string.Empty,
                    o.Content ?? src?.Content ?? string.Empty,
                    o.Note,
                    o.StartMinute ?? src?.StartMinute ?? 0,
                    o.EndMinute ?? src?.EndMinute ?? 0,
                    badge,
                    o.IsForced
                ));
                continue;
            }

            if (o.Kind == OverrideKind.Add)
            {
                var t = o.ToTeacherId ?? o.FromTeacherId;
                if (t != teacherId)
                    continue;
                var badge = o.IsForced ? "加课·冲突" : "加课";
                baseItems.Add(new Occurrence(
                    OccurrenceKind.Add,
                    o.StudentName ?? string.Empty,
                    o.Content ?? string.Empty,
                    o.Note,
                    o.StartMinute ?? 0,
                    o.EndMinute ?? 0,
                    badge,
                    o.IsForced
                ));
            }
        }

        return baseItems;
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var weekday = ToWeekday1To7(date);
        return date.AddDays(-(weekday - 1));
    }

    private static QuestPDF.Infrastructure.Color Q(byte r, byte g, byte b)
    {
        return QuestPDF.Infrastructure.Color.FromHex($"#{r:X2}{g:X2}{b:X2}");
    }

    private static List<string> BuildChangeLines(
        IReadOnlyList<OverrideEntry> overrides,
        IReadOnlyDictionary<string, Course> coursesById,
        IReadOnlyDictionary<string, string> teacherById)
    {
        var lines = new List<string>();
        foreach (var o in overrides.OrderBy(x => x.Date).ThenBy(x => x.StartMinute ?? int.MaxValue))
        {
            if (o.Kind == OverrideKind.Cancel && o.CourseId is not null)
            {
                coursesById.TryGetValue(o.CourseId, out var src);
                var tName = src is null ? (o.FromTeacherId is null ? "未知老师" : teacherById.GetValueOrDefault(o.FromTeacherId, "未知老师")) : teacherById.GetValueOrDefault(src.TeacherId, "未知老师");
                var time = src is null ? string.Empty : $"{TimeUtil.FormatMinutes(src.StartMinute)}-{TimeUtil.FormatMinutes(src.EndMinute)}";
                var who = src is null ? string.Empty : $"{src.StudentName} {src.Content}";
                lines.Add($"{o.Date:MM.dd} {tName} {time} 停课 {who}".Trim());
                continue;
            }

            if (o.Kind == OverrideKind.Move && o.CourseId is not null)
            {
                coursesById.TryGetValue(o.CourseId, out var src);
                var fromName = o.FromTeacherId is null ? (src is null ? "未知老师" : teacherById.GetValueOrDefault(src.TeacherId, "未知老师")) : teacherById.GetValueOrDefault(o.FromTeacherId, "未知老师");
                var toName = o.ToTeacherId is null ? "未知老师" : teacherById.GetValueOrDefault(o.ToTeacherId, "未知老师");
                var time = $"{TimeUtil.FormatMinutes(o.StartMinute ?? src?.StartMinute ?? 0)}-{TimeUtil.FormatMinutes(o.EndMinute ?? src?.EndMinute ?? 0)}";
                var who = $"{o.StudentName ?? src?.StudentName ?? string.Empty} {o.Content ?? src?.Content ?? string.Empty}".Trim();
                var flag = o.IsForced ? "（强制）" : string.Empty;
                lines.Add($"{o.Date:MM.dd} 窜课 {fromName} → {toName} {time} {who}{flag}".Trim());
                continue;
            }

            if (o.Kind == OverrideKind.Add)
            {
                var toName = o.ToTeacherId is null ? "未知老师" : teacherById.GetValueOrDefault(o.ToTeacherId, "未知老师");
                var time = $"{TimeUtil.FormatMinutes(o.StartMinute ?? 0)}-{TimeUtil.FormatMinutes(o.EndMinute ?? 0)}";
                var who = $"{o.StudentName ?? string.Empty} {o.Content ?? string.Empty}".Trim();
                var flag = o.IsForced ? "（强制）" : string.Empty;
                lines.Add($"{o.Date:MM.dd} 加课 {toName} {time} {who}{flag}".Trim());
            }
        }
        return lines;
    }

    private static int ToWeekday1To7(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return dow == 0 ? 7 : dow;
    }
}
