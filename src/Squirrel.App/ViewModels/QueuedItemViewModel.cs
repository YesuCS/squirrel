using Squirrel.Core;

namespace Squirrel.App.ViewModels;

public class QueuedItemViewModel
{
    public QueuedAction Model { get; }
    public QueuedItemViewModel(QueuedAction model) => Model = model;

    public string Id => Model.Id;
    public string Text => Model.Text;
}

public class HistoryItemViewModel
{
    public ActionLogEntry Model { get; }
    public HistoryItemViewModel(ActionLogEntry model) => Model = model;

    public string Text => Model.Text;
    public string DateText => Model.CompletedAt.ToLocalTime().ToString("MMM d");
}
