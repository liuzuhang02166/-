using System;
using Microsoft.Data.Sqlite;

namespace SchedulerApp.Data;

public sealed class AppDb
{
    private readonly string _connectionString;

    public AppDb(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public void EnsureCreated()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS teachers (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL UNIQUE,
              created_at TEXT NOT NULL,
              color_hex TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS courses (
              id TEXT PRIMARY KEY,
              student_name TEXT NOT NULL,
              content TEXT NOT NULL,
              teacher_id TEXT NOT NULL,
              start_date TEXT NOT NULL,
              end_date TEXT NULL,
              weekday INTEGER NOT NULL,
              start_minute INTEGER NOT NULL,
              end_minute INTEGER NOT NULL,
              note TEXT NOT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              FOREIGN KEY (teacher_id) REFERENCES teachers(id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS idx_courses_teacher_weekday ON courses (teacher_id, weekday);

            CREATE TABLE IF NOT EXISTS overrides (
              id TEXT PRIMARY KEY,
              kind INTEGER NOT NULL,
              date TEXT NOT NULL,
              course_id TEXT NULL,
              from_teacher_id TEXT NULL,
              to_teacher_id TEXT NULL,
              student_name TEXT NULL,
              content TEXT NULL,
              start_minute INTEGER NULL,
              end_minute INTEGER NULL,
              note TEXT NOT NULL,
              is_forced INTEGER NOT NULL,
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE,
              FOREIGN KEY (from_teacher_id) REFERENCES teachers(id) ON DELETE SET NULL,
              FOREIGN KEY (to_teacher_id) REFERENCES teachers(id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS idx_overrides_date ON overrides (date);
            CREATE INDEX IF NOT EXISTS idx_overrides_course_date ON overrides (course_id, date);

            CREATE TABLE IF NOT EXISTS week_notes (
              week_start TEXT PRIMARY KEY,
              notes TEXT NOT NULL,
              updated_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        EnsureColumn(conn, "teachers", "color_hex", "TEXT");
    }

    private static void EnsureColumn(SqliteConnection conn, string tableName, string columnName, string columnType)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType} NULL;";
        alter.ExecuteNonQuery();
    }
}

