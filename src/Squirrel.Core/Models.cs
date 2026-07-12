namespace Squirrel.Core;

public enum ProjectStatus
{
    Active = 0,
    Parked = 1,
    Done = 2
}

/// <summary>
/// A project. The rule that keeps re-entry friction low: every project carries
/// exactly ONE next action; a tiny, concrete, physical step.
/// </summary>
public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public string NextAction { get; set; } = "";
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>Required, 1 (someday) to 10 (critical). Defaults to 5.</summary>
    public int Priority { get; set; } = 5;

    /// <summary>Optional. Deadlines create urgency; absence creates freedom.</summary>
    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastTouchedAt { get; set; } = DateTimeOffset.UtcNow;

    public int DaysSinceTouch => (int)(DateTimeOffset.UtcNow - LastTouchedAt).TotalDays;

    /// <summary>Days until the due date (negative when overdue); null without one.</summary>
    public int? DaysUntilDue => DueDate is { } due
        ? (int)(due.LocalDateTime.Date - DateTime.Now.Date).TotalDays
        : null;

    public bool IsOverdue => DaysUntilDue is < 0;

    /// <summary>
    /// Urgency score used to order projects and pick the Now suggestion.
    /// Priority is the base; an approaching due date adds pressure sharply;
    /// staleness adds a small nudge so neglected work wins ties.
    /// </summary>
    public double Score
    {
        get
        {
            double score = Priority;
            if (DaysUntilDue is { } days)
            {
                score += days <= 0 ? 15
                    : days <= 1 ? 10
                    : days <= 3 ? 7
                    : days <= 7 ? 4
                    : days <= 14 ? 2
                    : 0;
            }
            score += Math.Min(DaysSinceTouch, 14) * 0.1;
            return score;
        }
    }
}

/// <summary>
/// A completed step; the win log. Every tiny action you finish is recorded
/// here so you can see the trail that got a project done.
/// </summary>
public class ActionLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A future step waiting its turn. The queue holds the plan; the project
/// still shows exactly ONE next action at a time.
/// </summary>
public class QueuedAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = "";
    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>
/// A raw capture. Zero required fields beyond the text so the shiny new idea
/// gets out of your head and saved in under two seconds.
/// </summary>
public class InboxItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public string Source { get; set; } = "app";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
