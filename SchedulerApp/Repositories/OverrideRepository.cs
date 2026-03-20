using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SchedulerApp.Data;
using SchedulerApp.Models;

namespace SchedulerApp.Repositories;

public sealed class OverrideRepository
{
    private readonly AppDb _db;

    public OverrideRepository(AppDb db)
    {
        _db = db;
    }

    public OverrideEntry Add(OverrideEntry entry)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO overrides (
              id, kind, date, course_id, from_teacher_id, to_teacher_id,
              student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at
            )
            VALUES (
              $id, $kind, $date, $course_id, $from_teacher_id, $to_teacher_id,
              $student_name, $content, $start_minute, $end_minute, $note, $is_forced, $created_at, $updated_at
            );
            """;
        BindOverride(cmd, entry);
        cmd.ExecuteNonQuery();
        return entry;
    }

    public OverrideEntry Update(OverrideEntry entry)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            UPDATE overrides SET
              kind = $kind,
              date = $date,
              course_id = $course_id,
              from_teacher_id = $from_teacher_id,
              to_teacher_id = $to_teacher_id,
              student_name = $student_name,
              content = $content,
              start_minute = $start_minute,
              end_minute = $end_minute,
              note = $note,
              is_forced = $is_forced,
              updated_at = $updated_at
            WHERE id = $id;
            """;
        BindOverride(cmd, entry);
        cmd.ExecuteNonQuery();
        return entry;
    }

    public IReadOnlyList<OverrideEntry> GetByDateRange(DateOnly start, DateOnly end)
    {
        var items = new List<OverrideEntry>();
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT
              id, kind, date, course_id, from_teacher_id, to_teacher_id,
              student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at
            FROM overrides
            WHERE date >= $start AND date <= $end
            ORDER BY date ASC, start_minute ASC;
            """;
        cmd.Parameters.AddWithValue("$start", start.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$end", end.ToString("yyyy-MM-dd"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            items.Add(ReadOverride(reader));
        return items;
    }

    public IReadOnlyList<OverrideEntry> GetAll()
    {
        var items = new List<OverrideEntry>();
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT
              id, kind, date, course_id, from_teacher_id, to_teacher_id,
              student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at
            FROM overrides
            ORDER BY date DESC, start_minute ASC;
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            items.Add(ReadOverride(reader));
        return items;
    }

    public void DeleteById(string id)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM overrides WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteByCourseId(string courseId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM overrides WHERE course_id = $course_id;";
        cmd.Parameters.AddWithValue("$course_id", courseId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteByTeacherId(string teacherId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            DELETE FROM overrides
            WHERE from_teacher_id = $teacher_id OR to_teacher_id = $teacher_id;
            """;
        cmd.Parameters.AddWithValue("$teacher_id", teacherId);
        cmd.ExecuteNonQuery();
    }

    private static void BindOverride(SqliteCommand cmd, OverrideEntry entry)
    {
        cmd.Parameters.AddWithValue("$id", entry.Id);
        cmd.Parameters.AddWithValue("$kind", (int)entry.Kind);
        cmd.Parameters.AddWithValue("$date", entry.Date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$course_id", (object?)entry.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$from_teacher_id", (object?)entry.FromTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$to_teacher_id", (object?)entry.ToTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$student_name", (object?)entry.StudentName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$content", (object?)entry.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$start_minute", (object?)entry.StartMinute ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$end_minute", (object?)entry.EndMinute ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$note", entry.Note ?? string.Empty);
        cmd.Parameters.AddWithValue("$is_forced", entry.IsForced ? 1 : 0);
        cmd.Parameters.AddWithValue("$created_at", entry.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$updated_at", entry.UpdatedAt.ToString("o"));
    }

    private static OverrideEntry ReadOverride(SqliteDataReader reader)
    {
        var id = reader.GetString(0);
        var kind = (OverrideKind)reader.GetInt32(1);
        var date = DateOnly.Parse(reader.GetString(2));
        var courseId = reader.IsDBNull(3) ? null : reader.GetString(3);
        var fromTeacherId = reader.IsDBNull(4) ? null : reader.GetString(4);
        var toTeacherId = reader.IsDBNull(5) ? null : reader.GetString(5);
        var studentName = reader.IsDBNull(6) ? null : reader.GetString(6);
        var content = reader.IsDBNull(7) ? null : reader.GetString(7);
        int? startMinute = reader.IsDBNull(8) ? null : reader.GetInt32(8);
        int? endMinute = reader.IsDBNull(9) ? null : reader.GetInt32(9);
        var note = reader.GetString(10);
        var isForced = reader.GetInt32(11) != 0;
        var createdAt = DateTimeOffset.Parse(reader.GetString(12));
        var updatedAt = DateTimeOffset.Parse(reader.GetString(13));
        return new OverrideEntry(
            id,
            kind,
            date,
            courseId,
            fromTeacherId,
            toTeacherId,
            studentName,
            content,
            startMinute,
            endMinute,
            note,
            isForced,
            createdAt,
            updatedAt
        );
    }
}
