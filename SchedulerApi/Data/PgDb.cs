using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SchedulerApi.Data;

public sealed class PgDb
{
    public NpgsqlDataSource DataSource { get; }

    public PgDb(string connectionString)
    {
        DataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        await using var conn = await DataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS teachers (
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL UNIQUE,
              created_at TIMESTAMPTZ NOT NULL,
              color_hex TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS courses (
              id TEXT PRIMARY KEY,
              student_name TEXT NOT NULL,
              content TEXT NOT NULL,
              teacher_id TEXT NOT NULL,
              start_date DATE NOT NULL,
              end_date DATE NULL,
              weekday INT NOT NULL,
              start_minute INT NOT NULL,
              end_minute INT NOT NULL,
              note TEXT NOT NULL,
              created_at TIMESTAMPTZ NOT NULL,
              updated_at TIMESTAMPTZ NOT NULL,
              CONSTRAINT fk_courses_teacher FOREIGN KEY (teacher_id) REFERENCES teachers(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS overrides (
              id TEXT PRIMARY KEY,
              kind INT NOT NULL,
              date DATE NOT NULL,
              course_id TEXT NULL,
              from_teacher_id TEXT NULL,
              to_teacher_id TEXT NULL,
              student_name TEXT NULL,
              content TEXT NULL,
              start_minute INT NULL,
              end_minute INT NULL,
              note TEXT NOT NULL,
              is_forced BOOLEAN NOT NULL,
              created_at TIMESTAMPTZ NOT NULL,
              updated_at TIMESTAMPTZ NOT NULL,
              CONSTRAINT fk_overrides_course FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS week_notes (
              week_start DATE PRIMARY KEY,
              notes TEXT NOT NULL,
              updated_at TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_courses_teacher_weekday_time ON courses(teacher_id, weekday, start_minute, student_name);
            CREATE INDEX IF NOT EXISTS idx_overrides_date_time ON overrides(date, start_minute);
            CREATE INDEX IF NOT EXISTS idx_week_notes_week_start ON week_notes(week_start);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

