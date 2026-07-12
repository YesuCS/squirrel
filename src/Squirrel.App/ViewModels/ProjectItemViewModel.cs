using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Squirrel.Core;

namespace Squirrel.App.ViewModels;

public partial class ProjectItemViewModel : ObservableObject
{
    public Project Model { get; }

    [ObservableProperty]
    private string nextActionDraft;

    [ObservableProperty]
    private double priorityDraft;

    [ObservableProperty]
    private DateTimeOffset? dueDateDraft;

    [ObservableProperty]
    private string newStepDraft = "";

    public ObservableCollection<QueuedItemViewModel> Queue { get; } = new();
    public ObservableCollection<HistoryItemViewModel> History { get; } = new();

    public ProjectItemViewModel(
        Project model, int staleDays,
        IEnumerable<QueuedAction>? queue = null,
        IEnumerable<ActionLogEntry>? history = null)
    {
        Model = model;
        nextActionDraft = model.NextAction;
        priorityDraft = model.Priority;
        dueDateDraft = model.DueDate;
        IsStale = model.Status == ProjectStatus.Active && model.DaysSinceTouch >= staleDays;

        if (queue is not null)
            foreach (var q in queue)
                Queue.Add(new QueuedItemViewModel(q));
        if (history is not null)
            foreach (var h in history)
                History.Add(new HistoryItemViewModel(h));
    }

    public int QueueCount => Queue.Count;
    public int WinCount => History.Count;
    public bool HasHistory => History.Count > 0;

    public string PlanHeader
    {
        get
        {
            var parts = new List<string>();
            parts.Add(Queue.Count == 1 ? "1 step queued" : $"{Queue.Count} steps queued");
            if (History.Count > 0)
                parts.Add(History.Count == 1 ? "1 step done" : $"{History.Count} steps done");
            return "Up next & wins · " + string.Join(" · ", parts);
        }
    }

    public string Id => Model.Id;
    public string Name => Model.Name;
    public string NextAction => Model.NextAction;
    public bool HasNextAction => !string.IsNullOrWhiteSpace(Model.NextAction);
    public bool NeedsNextAction => !HasNextAction;
    public bool IsParked => Model.Status == ProjectStatus.Parked;
    public bool IsActive => Model.Status == ProjectStatus.Active;
    public bool IsStale { get; }
    public int Priority => Model.Priority;

    public string StatusText => Model.Status switch
    {
        ProjectStatus.Parked => "Parked",
        ProjectStatus.Done => "Done",
        _ => IsStale ? $"Stale · {Model.DaysSinceTouch}d" : "Active"
    };

    public string TouchedText => Model.DaysSinceTouch switch
    {
        0 => "touched today",
        1 => "touched yesterday",
        var d => $"touched {d} days ago"
    };

    public string DueText => Model.DaysUntilDue switch
    {
        null => "",
        < 0 => $"overdue by {-Model.DaysUntilDue} day{(Model.DaysUntilDue == -1 ? "" : "s")}",
        0 => "due today",
        1 => "due tomorrow",
        var d => $"due in {d} days"
    };

    /// <summary>One muted line under the project name: priority, due, touched.</summary>
    public string MetaText
    {
        get
        {
            var parts = new List<string> { $"Priority {Model.Priority}" };
            if (DueText.Length > 0) parts.Add(DueText);
            parts.Add(TouchedText);
            return string.Join(" · ", parts);
        }
    }
}
