using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SchedulerApi.Data;
using SchedulerApp.Models;

namespace SchedulerApi.Repositories;

public sealed class SchedulerRepository
{
    private readonly PgDb _db;

    public SchedulerRepository(PgDb db)
    {
        _db = db;
    }

    public async Task<(List<Teacher> Teachers, List<Course> Courses, List<OverrideEntry> Overrides, List<WeekNote> WeekNotes)> GetSnapshotAsync(CancellationToken ct)
    {
        var teachers = await GetTeachersAsync(ct);
        var courses = await GetCoursesAsync(ct);
        var overrides = await GetOverridesAsync(ct);
        var weekNotes = await GetWeekNotesAsync(ct);
        return (teachers, courses, overrides, weekNotes);
    }

    public async Task<List<Teacher>> GetTeachersAsync(CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_at, color_hex FROM teachers ORDER BY lower(name);";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Teacher>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Teacher(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            ));
        }
        return list;
    }

    public async Task<Teacher?> GetTeacherByIdAsync(string id, CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_at, color_hex FROM teachers WHERE id = $1 LIMIT 1;";
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return new Teacher(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)
        );
    }

    public async Task<Teacher> InsertTeacherAsync(string name, string? colorHex, CancellationToken ct)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("老师名称不能为空。");

        var teacher = new Teacher(Guid.NewGuid().ToString("N"), trimmed, DateTimeOffset.UtcNow, string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim());

        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO teachers (id, name, created_at, color_hex) VALUES ($1, $2, $3, $4);";
        cmd.Parameters.AddWithValue(teacher.Id);
        cmd.Parameters.AddWithValue(teacher.Name);
        cmd.Parameters.AddWithValue(teacher.CreatedAt);
        cmd.Parameters.AddWithValue((object?)teacher.ColorHex ?? DBNull.Value);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException("该老师已存在。");
        }

        return teacher;
    }

    public async Task<Teacher> UpdateTeacherAsync(string teacherId, string name, string? colorHex, CancellationToken ct)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("老师名称不能为空。");

        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE teachers SET name = $2, color_hex = $3 WHERE id = $1;";
        cmd.Parameters.AddWithValue(teacherId);
        cmd.Parameters.AddWithValue(trimmed);
        cmd.Parameters.AddWithValue((object?)(string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim()) ?? DBNull.Value);
        try
        {
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0)
                throw new InvalidOperationException("未找到该老师。");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException("该老师已存在。");
        }

        return await GetTeacherByIdAsync(teacherId, ct) ?? throw new InvalidOperationException("未找到该老师。");
    }

    public async Task DeleteTeacherCascadeAsync(string teacherId, CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM overrides WHERE from_teacher_id = $1 OR to_teacher_id = $1;";
            cmd.Parameters.AddWithValue(teacherId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM courses WHERE teacher_id = $1;";
            cmd.Parameters.AddWithValue(teacherId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM teachers WHERE id = $1;";
            cmd.Parameters.AddWithValue(teacherId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<List<Course>> GetCoursesAsync(CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at
            FROM courses
            ORDER BY teacher_id, weekday, start_minute, lower(student_name);
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Course>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadCourse(reader));
        }
        return list;
    }

    public async Task<Course?> GetCourseByIdAsync(string id, CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at
            FROM courses
            WHERE id = $1
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return ReadCourse(reader);
    }

    public async Task<Course> InsertCourseAsync(Course draft, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var course = draft with { CreatedAt = now, UpdatedAt = now };

        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO courses
              (id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at)
            VALUES
              ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12);
            """;
        BindCourse(cmd, course);
        await cmd.ExecuteNonQueryAsync(ct);
        return course;
    }

    public async Task<Course> UpdateCourseAsync(Course incoming, CancellationToken ct)
    {
        var existing = await GetCourseByIdAsync(incoming.Id, ct);
        if (existing is null)
            throw new InvalidOperationException("未找到该学员排课。");
        if (existing.UpdatedAt != incoming.UpdatedAt)
            throw new ConcurrencyException("该学员排课已被其他人修改，请刷新后重试。");

        var updated = incoming with { UpdatedAt = DateTimeOffset.UtcNow, CreatedAt = existing.CreatedAt };

        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            UPDATE courses SET
              student_name = $2,
              content = $3,
              teacher_id = $4,
              start_date = $5,
              end_date = $6,
              weekday = $7,
              start_minute = $8,
              end_minute = $9,
              note = $10,
              updated_at = $12
            WHERE id = $1;
            """;
        BindCourse(cmd, updated);
        await cmd.ExecuteNonQueryAsync(ct);
        return updated;
    }

    public async Task DeleteCourseAsync(string courseId, CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM overrides WHERE course_id = $1;";
            cmd.Parameters.AddWithValue(courseId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM courses WHERE id = $1;";
            cmd.Parameters.AddWithValue(courseId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<List<OverrideEntry>> GetOverridesAsync(CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, kind, date, course_id, from_teacher_id, to_teacher_id, student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at
            FROM overrides
            ORDER BY date DESC, start_minute ASC;
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<OverrideEntry>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadOverride(reader));
        }
        return list;
    }

    public async Task<OverrideEntry?> GetOverrideByIdAsync(string id, CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, kind, date, course_id, from_teacher_id, to_teacher_id, student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at
            FROM overrides
            WHERE id = $1
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        return ReadOverride(reader);
    }

    public async Task<OverrideEntry> UpsertOverrideAsync(OverrideEntry incoming, CancellationToken ct)
    {
        var existing = await GetOverrideByIdAsync(incoming.Id, ct);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null && existing.UpdatedAt != incoming.UpdatedAt)
            throw new ConcurrencyException("该临时调整已被其他人修改，请刷新后重试。");

        var createdAt = existing?.CreatedAt ?? incoming.CreatedAt;
        var toSave = incoming with { CreatedAt = createdAt, UpdatedAt = now };

        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        if (existing is null)
        {
            cmd.CommandText =
                """
                INSERT INTO overrides
                  (id, kind, date, course_id, from_teacher_id, to_teacher_id, student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at)
                VALUES
                  ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14);
                """;
        }
        else
        {
            cmd.CommandText =
                """
                UPDATE overrides SET
                  kind=$2, date=$3, course_id=$4, from_teacher_id=$5, to_teacher_id=$6,
                  student_name=$7, content=$8, start_minute=$9, end_minute=$10, note=$11, is_forced=$12, updated_at=$14
                WHERE id=$1;
                """;
        }

        BindOverride(cmd, toSave);
        await cmd.ExecuteNonQueryAsync(ct);
        return toSave;
    }

    public async Task DeleteOverrideAsync(string id, CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM overrides WHERE id = $1;";
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<WeekNote>> GetWeekNotesAsync(CancellationToken ct)
    {
        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT week_start, notes, updated_at FROM week_notes ORDER BY week_start DESC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<WeekNote>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new WeekNote(
                DateOnly.FromDateTime(reader.GetDateTime(0)),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2)
            ));
        }
        return list;
    }

    public async Task<WeekNote> UpsertWeekNoteAsync(WeekNote note, CancellationToken ct)
    {
        var toSave = note with { UpdatedAt = DateTimeOffset.UtcNow };

        await using var conn = await _db.DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO week_notes (week_start, notes, updated_at)
            VALUES ($1,$2,$3)
            ON CONFLICT(week_start)
            DO UPDATE SET notes = excluded.notes, updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue(note.WeekStart.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue(note.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue(toSave.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return toSave;
    }

    private static void BindCourse(NpgsqlCommand cmd, Course course)
    {
        cmd.Parameters.AddWithValue(course.Id);
        cmd.Parameters.AddWithValue(course.StudentName.Trim());
        cmd.Parameters.AddWithValue(course.Content.Trim());
        cmd.Parameters.AddWithValue(course.TeacherId);
        cmd.Parameters.AddWithValue(course.StartDate.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue((object?)course.EndDate?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
        cmd.Parameters.AddWithValue(course.Weekday);
        cmd.Parameters.AddWithValue(course.StartMinute);
        cmd.Parameters.AddWithValue(course.EndMinute);
        cmd.Parameters.AddWithValue(course.Note ?? string.Empty);
        cmd.Parameters.AddWithValue(course.CreatedAt);
        cmd.Parameters.AddWithValue(course.UpdatedAt);
    }

    private static Course ReadCourse(NpgsqlDataReader reader)
    {
        var endDate = reader.IsDBNull(5) ? (DateOnly?)null : DateOnly.FromDateTime(reader.GetDateTime(5));
        return new Course(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateOnly.FromDateTime(reader.GetDateTime(4)),
            endDate,
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetFieldValue<DateTimeOffset>(10),
            reader.GetFieldValue<DateTimeOffset>(11)
        );
    }

    private static void BindOverride(NpgsqlCommand cmd, OverrideEntry entry)
    {
        cmd.Parameters.AddWithValue(entry.Id);
        cmd.Parameters.AddWithValue((int)entry.Kind);
        cmd.Parameters.AddWithValue(entry.Date.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue((object?)entry.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.FromTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.ToTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.StudentName ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.StartMinute ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)entry.EndMinute ?? DBNull.Value);
        cmd.Parameters.AddWithValue(entry.Note ?? string.Empty);
        cmd.Parameters.AddWithValue(entry.IsForced);
        cmd.Parameters.AddWithValue(entry.CreatedAt);
        cmd.Parameters.AddWithValue(entry.UpdatedAt);
    }

    private static OverrideEntry ReadOverride(NpgsqlDataReader reader)
    {
        int? startMinute = reader.IsDBNull(8) ? null : reader.GetInt32(8);
        int? endMinute = reader.IsDBNull(9) ? null : reader.GetInt32(9);
        return new OverrideEntry(
            reader.GetString(0),
            (OverrideKind)reader.GetInt32(1),
            DateOnly.FromDateTime(reader.GetDateTime(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            startMinute,
            endMinute,
            reader.GetString(10),
            reader.GetBoolean(11),
            reader.GetFieldValue<DateTimeOffset>(12),
            reader.GetFieldValue<DateTimeOffset>(13)
        );
    }
}

public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

