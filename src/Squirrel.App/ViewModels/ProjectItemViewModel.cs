using CommunityToolkit.Mvvm.ComponentModel;
using Squirrel.Core;

namespace Squirrel.App.ViewModels;

public partial class ProjectItemViewModel : ObservableObject
{
    public Project Model { get; }

    [ObservableProperty]
    private string nextActionDraft;

    public ProjectItemViewModel(Project model, int staleDays)
    {
        Model = model;
        nextActionDraft = model.NextAction;
        IsStale = model.Status == ProjectStatus.Active && model.DaysSinceTouch >= staleDays;
    }

    public string Id => Model.Id;
    public string Name => Model.Name;
    public string NextAction => Model.NextAction;
    public bool HasNextAction => !string.IsNullOrWhiteSpace(Model.NextAction);
    public bool NeedsNextAction => !HasNextAction;
    public bool IsParked => Model.Status == ProjectStatus.Parked;
    public bool IsActive => Model.Status == ProjectStatus.Active;
    public bool IsStale { get; }

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
}
