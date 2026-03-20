using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SchedulerApp.Data;
using SchedulerApp.Models;

namespace SchedulerApp.Repositories;

public sealed class WeekNoteRepository
{
    private readonly AppDb _db;

    public WeekNoteRepository(AppDb db)
    {
        _db = db;
    }

    public WeekNote Upsert(WeekNote note)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO week_notes (week_start, notes, updated_at)
            VALUES ($week_start, $notes, $updated_at)
            ON CONFLICT(week_start)
            DO UPDATE SET
              notes = excluded.notes,
              updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$week_start", note.WeekStart.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$notes", note.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue("$updated_at", note.UpdatedAt.ToString("o"));
        cmd.ExecuteNonQuery();
        return note;
    }

    public IReadOnlyList<WeekNote> GetAll()
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT week_start, notes, updated_at FROM week_notes ORDER BY week_start DESC;";
        using var reader = cmd.ExecuteReader();
        var list = new List<WeekNote>();
        while (reader.Read())
        {
            list.Add(new WeekNote(
                DateOnly.Parse(reader.GetString(0)),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2))
            ));
        }
        return list;
    }

    public WeekNote? Get(DateOnly weekStart)
    {
        using var conn = _db.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT week_start, notes, updated_at FROM week_notes WHERE week_start = $week_start;";
        cmd.Parameters.AddWithValue("$week_start", weekStart.ToString("yyyy-MM-dd"));
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        return new WeekNote(
            DateOnly.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2))
        );
    }
}
