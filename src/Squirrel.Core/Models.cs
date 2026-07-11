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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastTouchedAt { get; set; } = DateTimeOffset.UtcNow;

    public int DaysSinceTouch => (int)(DateTimeOffset.UtcNow - LastTouchedAt).TotalDays;
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
