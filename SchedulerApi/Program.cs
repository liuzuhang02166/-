using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SchedulerApi.Data;
using SchedulerApi.Repositories;
using SchedulerApp.Models;
using SchedulerApp.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var connectionString = GetConnectionString(builder.Configuration);
var db = new PgDb(connectionString);
await db.EnsureCreatedAsync();
builder.Services.AddSingleton(db);
builder.Services.AddSingleton<SchedulerRepository>();

var adminPassword = builder.Configuration["ADMIN_PASSWORD"] ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
if (string.IsNullOrWhiteSpace(adminPassword))
    throw new InvalidOperationException("Missing ADMIN_PASSWORD environment variable.");

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("Missing JWT_SECRET environment variable (min length 32).");

var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "scheduler-api";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "scheduler";
var allowedOrigins = builder.Configuration["ALLOWED_ORIGINS"] ?? Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? string.Empty;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(allowedOrigins))
{
    app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
}
else
{
    var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    app.UseCors(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "SchedulerApi");
app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapPost("/auth/login", (LoginRequest req) =>
{
    var pw = req.Password ?? string.Empty;
    if (!FixedTimeEquals(pw, adminPassword))
        return Results.Unauthorized();

    var token = CreateJwt(jwtSecret, jwtIssuer, jwtAudience);
    return Results.Ok(new LoginResponse(token));
});

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/snapshot", async (SchedulerRepository repo, CancellationToken ct) =>
{
    var (teachers, courses, overrides, weekNotes) = await repo.GetSnapshotAsync(ct);
    return Results.Ok(new SchedulerSnapshot(teachers, courses, overrides, weekNotes));
});

api.MapPost("/snapshot/import", async (PgDb pg, SchedulerRepository repo, SchedulerSnapshot snapshot, CancellationToken ct) =>
{
    await using var conn = await pg.DataSource.OpenConnectionAsync(ct);
    await using var tx = await conn.BeginTransactionAsync(ct);

    async Task ExecAsync(string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    await ExecAsync("DELETE FROM overrides;");
    await ExecAsync("DELETE FROM courses;");
    await ExecAsync("DELETE FROM teachers;");
    await ExecAsync("DELETE FROM week_notes;");

    foreach (var t in snapshot.Teachers ?? [])
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO teachers (id, name, created_at, color_hex) VALUES ($1,$2,$3,$4);";
        cmd.Parameters.AddWithValue(t.Id);
        cmd.Parameters.AddWithValue(t.Name);
        cmd.Parameters.AddWithValue(t.CreatedAt);
        cmd.Parameters.AddWithValue((object?)t.ColorHex ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    foreach (var c in snapshot.Courses ?? [])
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO courses
              (id, student_name, content, teacher_id, start_date, end_date, weekday, start_minute, end_minute, note, created_at, updated_at)
            VALUES
              ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12);
            """;
        cmd.Parameters.AddWithValue(c.Id);
        cmd.Parameters.AddWithValue(c.StudentName);
        cmd.Parameters.AddWithValue(c.Content);
        cmd.Parameters.AddWithValue(c.TeacherId);
        cmd.Parameters.AddWithValue(c.StartDate.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue((object?)c.EndDate?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
        cmd.Parameters.AddWithValue(c.Weekday);
        cmd.Parameters.AddWithValue(c.StartMinute);
        cmd.Parameters.AddWithValue(c.EndMinute);
        cmd.Parameters.AddWithValue(c.Note ?? string.Empty);
        cmd.Parameters.AddWithValue(c.CreatedAt);
        cmd.Parameters.AddWithValue(c.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    foreach (var o in snapshot.Overrides ?? [])
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO overrides
              (id, kind, date, course_id, from_teacher_id, to_teacher_id, student_name, content, start_minute, end_minute, note, is_forced, created_at, updated_at)
            VALUES
              ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14);
            """;
        cmd.Parameters.AddWithValue(o.Id);
        cmd.Parameters.AddWithValue((int)o.Kind);
        cmd.Parameters.AddWithValue(o.Date.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue((object?)o.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)o.FromTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)o.ToTeacherId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)o.StudentName ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)o.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)o.StartMinute ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)o.EndMinute ?? DBNull.Value);
        cmd.Parameters.AddWithValue(o.Note ?? string.Empty);
        cmd.Parameters.AddWithValue(o.IsForced);
        cmd.Parameters.AddWithValue(o.CreatedAt);
        cmd.Parameters.AddWithValue(o.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    foreach (var n in snapshot.WeekNotes ?? [])
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO week_notes (week_start, notes, updated_at) VALUES ($1,$2,$3);";
        cmd.Parameters.AddWithValue(n.WeekStart.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue(n.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue(n.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    await tx.CommitAsync(ct);
    var (teachers, courses, overrides, weekNotes) = await repo.GetSnapshotAsync(ct);
    return Results.Ok(new SchedulerSnapshot(teachers, courses, overrides, weekNotes));
});

api.MapGet("/teachers", async (SchedulerRepository repo, CancellationToken ct) => Results.Ok(await repo.GetTeachersAsync(ct)));
api.MapPost("/teachers", async (SchedulerRepository repo, CreateTeacherRequest req, CancellationToken ct) => Results.Ok(await repo.InsertTeacherAsync(req.Name, req.ColorHex, ct)));
api.MapPut("/teachers/{id}", async (SchedulerRepository repo, string id, UpdateTeacherRequest req, CancellationToken ct) => Results.Ok(await repo.UpdateTeacherAsync(id, req.Name, req.ColorHex, ct)));
api.MapDelete("/teachers/{id}", async (SchedulerRepository repo, string id, CancellationToken ct) =>
{
    await repo.DeleteTeacherCascadeAsync(id, ct);
    return Results.NoContent();
});

api.MapGet("/courses", async (SchedulerRepository repo, CancellationToken ct) => Results.Ok(await repo.GetCoursesAsync(ct)));
api.MapPost("/courses", async (SchedulerRepository repo, Course course, CancellationToken ct) => Results.Ok(await repo.InsertCourseAsync(course, ct)));
api.MapPut("/courses/{id}", async (SchedulerRepository repo, string id, Course course, CancellationToken ct) =>
{
    if (!string.Equals(id, course.Id, StringComparison.Ordinal))
        throw new InvalidOperationException("ID 不匹配。");
    try
    {
        var updated = await repo.UpdateCourseAsync(course, ct);
        return Results.Ok(updated);
    }
    catch (ConcurrencyException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});
api.MapDelete("/courses/{id}", async (SchedulerRepository repo, string id, CancellationToken ct) =>
{
    await repo.DeleteCourseAsync(id, ct);
    return Results.NoContent();
});

api.MapGet("/overrides", async (SchedulerRepository repo, CancellationToken ct) => Results.Ok(await repo.GetOverridesAsync(ct)));
api.MapPut("/overrides/{id}", async (SchedulerRepository repo, string id, OverrideEntry entry, CancellationToken ct) =>
{
    if (!string.Equals(id, entry.Id, StringComparison.Ordinal))
        throw new InvalidOperationException("ID 不匹配。");
    try
    {
        var saved = await repo.UpsertOverrideAsync(entry, ct);
        return Results.Ok(saved);
    }
    catch (ConcurrencyException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});
api.MapDelete("/overrides/{id}", async (SchedulerRepository repo, string id, CancellationToken ct) =>
{
    await repo.DeleteOverrideAsync(id, ct);
    return Results.NoContent();
});

api.MapGet("/week-notes", async (SchedulerRepository repo, CancellationToken ct) => Results.Ok(await repo.GetWeekNotesAsync(ct)));
api.MapPut("/week-notes/{weekStart}", async (SchedulerRepository repo, string weekStart, WeekNoteUpsertRequest req, CancellationToken ct) =>
{
    if (!DateOnly.TryParse(weekStart, out var ws))
        throw new InvalidOperationException("weekStart 不合法。");
    var saved = await repo.UpsertWeekNoteAsync(new WeekNote(ws, req.Notes ?? string.Empty, DateTimeOffset.UtcNow), ct);
    return Results.Ok(saved);
});

api.MapGet("/export/pdf", async (SchedulerRepository repo, DateOnly start, DateOnly end, string? teacherId, bool includeWeekNotes, CancellationToken ct) =>
{
    var (teachers, courses, overrides, weekNotes) = await repo.GetSnapshotAsync(ct);
    var bytes = PdfExportService.ExportRangeToBytes(start, end, teacherId, includeWeekNotes, teachers, courses, overrides, weekNotes);
    var teacherName = string.IsNullOrWhiteSpace(teacherId)
        ? "全部老师"
        : teachers.FirstOrDefault(t => t.Id == teacherId)?.Name ?? "老师";
    var fileName = $"{teacherName}_排课表_{start:yyyyMMdd}-{end:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf", fileName);
});

app.Run();

static string GetConnectionString(IConfiguration cfg)
{
    var fromConn = cfg.GetConnectionString("Default");
    if (!string.IsNullOrWhiteSpace(fromConn))
        return fromConn;

    var fromEnv = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(fromEnv))
        throw new InvalidOperationException("Missing database connection string. Set DATABASE_URL or ConnectionStrings:Default.");

    if (fromEnv.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        fromEnv.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(fromEnv);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var dbName = uri.AbsolutePath.Trim('/').Split('/', 2)[0];
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = username,
            Password = password,
            Database = dbName
        };
        return csb.ToString();
    }

    return fromEnv;
}

static string CreateJwt(string jwtSecret, string issuer, string audience)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var now = DateTimeOffset.UtcNow;
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, "admin"),
        new Claim(ClaimTypes.Role, "admin")
    };
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: now.UtcDateTime,
        expires: now.AddDays(7).UtcDateTime,
        signingCredentials: creds
    );
    return new JwtSecurityTokenHandler().WriteToken(token);
}

static bool FixedTimeEquals(string a, string b)
{
    var ba = Encoding.UTF8.GetBytes(a);
    var bb = Encoding.UTF8.GetBytes(b);
    if (ba.Length != bb.Length)
        return false;
    return CryptographicOperations.FixedTimeEquals(ba, bb);
}

public sealed record LoginRequest(string Password);
public sealed record LoginResponse(string Token);
public sealed record CreateTeacherRequest(string Name, string? ColorHex);
public sealed record UpdateTeacherRequest(string Name, string? ColorHex);
public sealed record WeekNoteUpsertRequest(string Notes);

public sealed record SchedulerSnapshot(
    List<Teacher> Teachers,
    List<Course> Courses,
    List<OverrideEntry> Overrides,
    List<WeekNote> WeekNotes
);
