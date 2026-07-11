using Squirrel.Core;

namespace Squirrel.App.ViewModels;

public class InboxItemViewModel
{
    public InboxItem Model { get; }

    public InboxItemViewModel(InboxItem model) => Model = model;

    public string Id => Model.Id;
    public string Text => Model.Text;
    public string Meta => $"{Model.Source} · {Model.CreatedAt.ToLocalTime():MMM d, h:mm tt}";
}
