using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SchedulerApp.Data;
using SchedulerApp.Domain;
using SchedulerApp.Models;

namespace SchedulerApp.Repositories;

public sealed class CourseRepository
{
    private readonly AppDb _db;

    public CourseRepository(AppDb db)
    {
        _db = db;
    }

    public IReadOnlyList<Course> GetByTeacher(string teacherId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at
            FROM courses
            WHERE teacher_id = $teacher_id
            ORDER BY weekday, start_minute, student_name COLLATE NOCASE;
            """;
        cmd.Parameters.AddWithValue("$teacher_id", teacherId);
        using var reader = cmd.ExecuteReader();
        var list = new List<Course>();
        while (reader.Read())
        {
            list.Add(Map(reader));
        }

        return list;
    }

    public IReadOnlyList<Course> GetAll()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at
            FROM courses
            ORDER BY teacher_id, weekday, start_minute, student_name COLLATE NOCASE;
            """;
        using var reader = cmd.ExecuteReader();
        var list = new List<Course>();
        while (reader.Read())
        {
            list.Add(Map(reader));
        }

        return list;
    }

    public Course Add(Course draft)
    {
        var now = DateTimeOffset.UtcNow;
        var course = draft with { CreatedAt = now, UpdatedAt = now };

        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO courses
              (id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at)
            VALUES
              ($id, $student_name, $content, $teacher_id, $start_date, $end_date, $weekday, $start_minute, $end_minute, $note, $created_at, $updated_at);
            """;
        Bind(cmd, course);
        cmd.ExecuteNonQuery();
        return course;
    }

    public Course Update(Course course)
    {
        var updated = course with { UpdatedAt = DateTimeOffset.UtcNow };
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            UPDATE courses SET
              student_name = $student_name,
              content = $content,
              teacher_id = $teacher_id,
              start_date = $start_date,
              end_date = $end_date,
              weekday = $weekday,
              start_minute = $start_minute,
              end_minute = $end_minute,
              note = $note,
              updated_at = $updated_at
            WHERE id = $id;
            """;
        Bind(cmd, updated);
        cmd.ExecuteNonQuery();
        return updated;
    }

    public Course? GetById(string id)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at
            FROM courses
            WHERE id = $id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        return Map(reader);
    }

    public Course? FindConflictingCourse(
        string teacherId,
        int weekday,
        int startMinute,
        int endMinute,
        string? excludeCourseId = null)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText =
            """
            SELECT id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at
            FROM courses
            WHERE teacher_id = $teacher_id
              AND weekday = $weekday
              AND ($exclude_id IS NULL OR id <> $exclude_id)
              AND start_minute < $end_minute
              AND $start_minute < end_minute
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$teacher_id", teacherId);
        cmd.Parameters.AddWithValue("$weekday", weekday);
        cmd.Parameters.AddWithValue("$start_minute", startMinute);
        cmd.Parameters.AddWithValue("$end_minute", endMinute);
        cmd.Parameters.AddWithValue("$exclude_id", (object?)excludeCourseId ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return Map(reader);
    }

    public int CountByTeacherId(string teacherId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM courses WHERE teacher_id = $teacher_id;";
        cmd.Parameters.AddWithValue("$teacher_id", teacherId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void DeleteById(string id)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM courses WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void DeleteByTeacherId(string teacherId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM courses WHERE teacher_id = $teacher_id;";
        cmd.Parameters.AddWithValue("$teacher_id", teacherId);
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, Course course)
    {
        cmd.Parameters.AddWithValue("$id", course.Id);
        cmd.Parameters.AddWithValue("$student_name", course.StudentName.Trim());
        cmd.Parameters.AddWithValue("$content", course.Content.Trim());
        cmd.Parameters.AddWithValue("$teacher_id", course.TeacherId);
        cmd.Parameters.AddWithValue("$start_date", DbFormat.ToDbDate(course.StartDate));
        cmd.Parameters.AddWithValue("$end_date", course.EndDate is null ? DBNull.Value : DbFormat.ToDbDate(course.EndDate.Value));
        cmd.Parameters.AddWithValue("$weekday", course.Weekday);
        cmd.Parameters.AddWithValue("$start_minute", course.StartMinute);
        cmd.Parameters.AddWithValue("$end_minute", course.EndMinute);
        cmd.Parameters.AddWithValue("$note", course.Note ?? string.Empty);
        cmd.Parameters.AddWithValue("$created_at", course.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updated_at", course.UpdatedAt.ToString("O"));
    }

    private static Course Map(SqliteDataReader reader)
    {
        var endDate = reader.IsDBNull(5) ? (DateOnly?)null : DbFormat.ParseDbDate(reader.GetString(5));
        return new Course(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DbFormat.ParseDbDate(reader.GetString(4)),
            endDate,
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetString(9),
            DateTimeOffset.Parse(reader.GetString(10)),
            DateTimeOffset.Parse(reader.GetString(11))
        );
    }
}

