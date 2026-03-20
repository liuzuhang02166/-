using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SchedulerApp.Data;
using SchedulerApp.Models;

namespace SchedulerApp.Repositories;

public sealed class TeacherRepository
{
    private readonly AppDb _db;

    public TeacherRepository(AppDb db)
    {
        _db = db;
    }

    public IReadOnlyList<Teacher> GetAll()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, created_at, color_hex FROM teachers ORDER BY name COLLATE NOCASE;";
        using var reader = cmd.ExecuteReader();
        var list = new List<Teacher>();
        while (reader.Read())
        {
            list.Add(new Teacher(
                reader.GetString(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            ));
        }

        return list;
    }

    public Teacher Add(string name, string? colorHex)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("老师名称不能为空。", nameof(name));

        var id = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;

        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO teachers (id, name, created_at, color_hex) VALUES ($id, $name, $created_at, $color_hex);";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", trimmed);
        cmd.Parameters.AddWithValue("$created_at", now.ToString("O"));
        cmd.Parameters.AddWithValue("$color_hex", (object?)colorHex ?? DBNull.Value);
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("该老师已存在。");
        }

        return new Teacher(id, trimmed, now, colorHex);
    }

    public void DeleteById(string teacherId)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM teachers WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", teacherId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCascade(string teacherId)
    {
        using var conn = _db.OpenConnection();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM courses WHERE teacher_id = $teacher_id;";
            cmd.Parameters.AddWithValue("$teacher_id", teacherId);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM teachers WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", teacherId);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public void SetColorHex(string teacherId, string? colorHex)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE teachers SET color_hex = $color_hex WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", teacherId);
        cmd.Parameters.AddWithValue("$color_hex", (object?)colorHex ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}

