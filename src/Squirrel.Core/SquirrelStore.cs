using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Squirrel.Core;

/// <summary>
/// SQLite-backed store. Single file, no server, no migrations framework;
/// tables are created on first run. Raises <see cref="Changed"/> after every
/// write so the UI can refresh even when writes come from the local API.
/// </summary>
public class SquirrelStore
{
    private readonly string _connectionString;

    /// <summary>Fired after any write. May fire on a non-UI thread.</summary>
    public event Action? Changed;

    public string DbPath { get; }

    public SquirrelStore(string? dbPath = null)
    {
        DbPath = dbPath ?? DefaultDbPath();
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DbPath }.ToString();
        Initialize();
    }

    public static string DefaultDbPath()
    {
        var baseDir = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);
        return Path.Combine(baseDir, "Squirrel", "squirrel.db");
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        Exec(conn, """
            CREATE TABLE IF NOT EXISTS Project (
                Id            TEXT PRIMARY KEY,
                Name          TEXT NOT NULL,
                Notes         TEXT NOT NULL DEFAULT '',
                NextAction    TEXT NOT NULL DEFAULT '',
                Status        INTEGER NOT NULL DEFAULT 0,
                Priority      INTEGER NOT NULL DEFAULT 5,
                DueDate       TEXT NULL,
                CreatedAt     TEXT NOT NULL,
                LastTouchedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS InboxItem (
                Id          TEXT PRIMARY KEY,
                Text        TEXT NOT NULL,
                Source      TEXT NOT NULL DEFAULT 'app',
                CreatedAt   TEXT NOT NULL,
                ProcessedAt TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS Setting (
                Key   TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """);

        MigrateProjectColumns(conn);

        if (GetSetting("ApiKey") is null)
            SetSettingInternal("ApiKey", GenerateApiKey());
        if (GetSetting("StaleDays") is null)
            SetSettingInternal("StaleDays", "7");
    }

    /// <summary>Adds columns introduced after the first release to older databases.</summary>
    private static void MigrateProjectColumns(SqliteConnection conn)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Project)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                existing.Add(reader.GetString(reader.GetOrdinal("name")));
        }

        if (!existing.Contains("Priority"))
            Exec(conn, "ALTER TABLE Project ADD COLUMN Priority INTEGER NOT NULL DEFAULT 5");
        if (!existing.Contains("DueDate"))
            Exec(conn, "ALTER TABLE Project ADD COLUMN DueDate TEXT NULL");
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return "sq_" + Convert.ToBase64String(bytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
    }

    // ---------- Settings ----------

    public string? GetSetting(string key)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Setting WHERE Key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        SetSettingInternal(key, value);
        Changed?.Invoke();
    }

    private void SetSettingInternal(string key, string value)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Setting (Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value = $v
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public string ApiKey => GetSetting("ApiKey")!;

    public int StaleDays
    {
        get => int.TryParse(GetSetting("StaleDays"), out var d) ? d : 7;
        set => SetSetting("StaleDays", value.ToString());
    }

    public string? FocusProjectId
    {
        get => GetSetting("FocusProjectId");
        set => SetSetting("FocusProjectId", value ?? "");
    }

    // ---------- Projects ----------

    public List<Project> GetProjects(bool includeDone = false)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = includeDone
            ? "SELECT * FROM Project"
            : "SELECT * FROM Project WHERE Status != 2";
        using var reader = cmd.ExecuteReader();
        var list = new List<Project>();
        while (reader.Read())
            list.Add(ReadProject(reader));

        // Active first, then parked, then done; urgency score within each.
        return list
            .OrderBy(p => p.Status)
            .ThenByDescending(p => p.Score)
            .ToList();
    }

    public Project? GetProject(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Project WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadProject(reader) : null;
    }

    /// <summary>Active projects untouched for longer than <see cref="StaleDays"/>.</summary>
    public List<Project> GetStaleProjects()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-StaleDays);
        return GetProjects()
            .Where(p => p.Status == ProjectStatus.Active && p.LastTouchedAt < cutoff)
            .OrderBy(p => p.LastTouchedAt)
            .ToList();
    }

    public Project AddProject(
        string name, string nextAction = "", string notes = "",
        int priority = 5, DateTimeOffset? dueDate = null)
    {
        var p = new Project
        {
            Name = name.Trim(),
            NextAction = nextAction.Trim(),
            Notes = notes,
            Priority = Math.Clamp(priority, 1, 10),
            DueDate = dueDate
        };
        using (var conn = Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Project (Id, Name, Notes, NextAction, Status, Priority, DueDate, CreatedAt, LastTouchedAt)
                VALUES ($id, $name, $notes, $next, $status, $priority, $due, $created, $touched)
                """;
            cmd.Parameters.AddWithValue("$id", p.Id);
            cmd.Parameters.AddWithValue("$name", p.Name);
            cmd.Parameters.AddWithValue("$notes", p.Notes);
            cmd.Parameters.AddWithValue("$next", p.NextAction);
            cmd.Parameters.AddWithValue("$status", (int)p.Status);
            cmd.Parameters.AddWithValue("$priority", p.Priority);
            cmd.Parameters.AddWithValue("$due", (object?)p.DueDate?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$created", p.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$touched", p.LastTouchedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        Changed?.Invoke();
        return p;
    }

    public void UpdateProject(Project p)
    {
        using (var conn = Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE Project SET Name = $name, Notes = $notes, NextAction = $next,
                    Status = $status, Priority = $priority, DueDate = $due, LastTouchedAt = $touched
                WHERE Id = $id
                """;
            cmd.Parameters.AddWithValue("$id", p.Id);
            cmd.Parameters.AddWithValue("$name", p.Name);
            cmd.Parameters.AddWithValue("$notes", p.Notes);
            cmd.Parameters.AddWithValue("$next", p.NextAction);
            cmd.Parameters.AddWithValue("$status", (int)p.Status);
            cmd.Parameters.AddWithValue("$priority", Math.Clamp(p.Priority, 1, 10));
            cmd.Parameters.AddWithValue("$due", (object?)p.DueDate?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$touched", p.LastTouchedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        Changed?.Invoke();
    }

    /// <summary>Mark a project as worked on right now (resets staleness).</summary>
    public void Touch(string projectId)
    {
        var p = GetProject(projectId);
        if (p is null) return;
        p.LastTouchedAt = DateTimeOffset.UtcNow;
        UpdateProject(p);
    }

    /// <summary>Update next action, priority, and due date together; resets staleness.</summary>
    public void UpdateProjectMeta(string projectId, string nextAction, int priority, DateTimeOffset? dueDate)
    {
        var p = GetProject(projectId);
        if (p is null) return;
        p.NextAction = nextAction.Trim();
        p.Priority = Math.Clamp(priority, 1, 10);
        p.DueDate = dueDate;
        p.LastTouchedAt = DateTimeOffset.UtcNow;
        UpdateProject(p);
    }

    /// <summary>Set the single next action for a project and reset staleness.</summary>
    public void SetNextAction(string projectId, string nextAction)
    {
        var p = GetProject(projectId);
        if (p is null) return;
        p.NextAction = nextAction.Trim();
        p.LastTouchedAt = DateTimeOffset.UtcNow;
        UpdateProject(p);
    }

    public void SetStatus(string projectId, ProjectStatus status)
    {
        var p = GetProject(projectId);
        if (p is null) return;
        p.Status = status;
        p.LastTouchedAt = DateTimeOffset.UtcNow;
        UpdateProject(p);
    }

    public void DeleteProject(string projectId)
    {
        using (var conn = Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Project WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", projectId);
            cmd.ExecuteNonQuery();
        }
        if (FocusProjectId == projectId)
            FocusProjectId = null;
        Changed?.Invoke();
    }

    private static Project ReadProject(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("Id")),
        Name = r.GetString(r.GetOrdinal("Name")),
        Notes = r.GetString(r.GetOrdinal("Notes")),
        NextAction = r.GetString(r.GetOrdinal("NextAction")),
        Status = (ProjectStatus)r.GetInt32(r.GetOrdinal("Status")),
        Priority = r.GetInt32(r.GetOrdinal("Priority")),
        DueDate = r.IsDBNull(r.GetOrdinal("DueDate"))
            ? null
            : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("DueDate"))),
        CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        LastTouchedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("LastTouchedAt")))
    };

    // ---------- Inbox ----------

    public List<InboxItem> GetInbox(bool includeProcessed = false)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = includeProcessed
            ? "SELECT * FROM InboxItem ORDER BY CreatedAt DESC"
            : "SELECT * FROM InboxItem WHERE ProcessedAt IS NULL ORDER BY CreatedAt DESC";
        using var reader = cmd.ExecuteReader();
        var list = new List<InboxItem>();
        while (reader.Read())
        {
            list.Add(new InboxItem
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Text = reader.GetString(reader.GetOrdinal("Text")),
                Source = reader.GetString(reader.GetOrdinal("Source")),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
                ProcessedAt = reader.IsDBNull(reader.GetOrdinal("ProcessedAt"))
                    ? null
                    : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ProcessedAt")))
            });
        }
        return list;
    }

    public InboxItem Capture(string text, string source = "app")
    {
        var item = new InboxItem { Text = text.Trim(), Source = source };
        using (var conn = Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO InboxItem (Id, Text, Source, CreatedAt, ProcessedAt)
                VALUES ($id, $text, $source, $created, NULL)
                """;
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.Parameters.AddWithValue("$text", item.Text);
            cmd.Parameters.AddWithValue("$source", item.Source);
            cmd.Parameters.AddWithValue("$created", item.CreatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        Changed?.Invoke();
        return item;
    }

    public void MarkProcessed(string inboxItemId)
    {
        using (var conn = Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE InboxItem SET ProcessedAt = $now WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", inboxItemId);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        Changed?.Invoke();
    }

    /// <summary>Turn a captured idea into a real project in one step.</summary>
    public Project PromoteToProject(string inboxItemId, string? nextAction = null)
    {
        var item = GetInbox(includeProcessed: true).FirstOrDefault(i => i.Id == inboxItemId)
            ?? throw new InvalidOperationException($"Inbox item {inboxItemId} not found.");
        var project = AddProject(item.Text, nextAction ?? "");
        MarkProcessed(inboxItemId);
        return project;
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
