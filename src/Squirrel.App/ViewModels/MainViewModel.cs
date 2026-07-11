using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squirrel.App.Api;
using Squirrel.Core;

namespace Squirrel.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SquirrelStore _store;

    public ObservableCollection<InboxItemViewModel> Inbox { get; } = new();
    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<ProjectItemViewModel> StaleProjects { get; } = new();

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private string captureText = "";

    [ObservableProperty]
    private string newProjectName = "";

    [ObservableProperty]
    private string newProjectNextAction = "";

    [ObservableProperty]
    private ProjectItemViewModel? focusProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoFocus))]
    private bool hasFocus;

    [ObservableProperty]
    private string focusNextStepDraft = "";

    [ObservableProperty]
    private string inboxTabHeader = "Inbox";

    [ObservableProperty]
    private string staleTabHeader = "Resurface";

    [ObservableProperty]
    private string staleDaysText = "7";

    public bool HasNoFocus => !HasFocus;

    public string ApiBaseUrl { get; }
    public string ApiKey { get; }
    public string CurlExample { get; }
    public string DbPath { get; }

    public MainViewModel(SquirrelStore store, string apiBaseUrl)
    {
        _store = store;
        ApiBaseUrl = apiBaseUrl;
        ApiKey = store.ApiKey;
        DbPath = store.DbPath;
        CurlExample =
            $"curl -X POST {apiBaseUrl}/capture \\\n" +
            $"  -H \"X-Api-Key: {store.ApiKey}\" \\\n" +
            "  -H \"Content-Type: application/json\" \\\n" +
            "  -d '{\"text\":\"your idea here\",\"source\":\"curl\"}'";

        staleDaysText = store.StaleDays.ToString();

        // Refresh the UI whenever anything writes to the store,
        // including captures arriving over the local API.
        _store.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public void Refresh()
    {
        var staleDays = _store.StaleDays;

        Inbox.Clear();
        foreach (var i in _store.GetInbox())
            Inbox.Add(new InboxItemViewModel(i));
        InboxTabHeader = Inbox.Count > 0 ? $"Inbox ({Inbox.Count})" : "Inbox";

        Projects.Clear();
        foreach (var p in _store.GetProjects())
            Projects.Add(new ProjectItemViewModel(p, staleDays));

        StaleProjects.Clear();
        foreach (var p in _store.GetStaleProjects())
            StaleProjects.Add(new ProjectItemViewModel(p, staleDays));
        StaleTabHeader = StaleProjects.Count > 0 ? $"Resurface ({StaleProjects.Count})" : "Resurface";

        var focusId = _store.FocusProjectId;
        FocusProject = string.IsNullOrEmpty(focusId)
            ? null
            : Projects.FirstOrDefault(p => p.Id == focusId);
        HasFocus = FocusProject is not null;
    }

    // ---------- Capture ----------

    [RelayCommand]
    private void Capture()
    {
        if (string.IsNullOrWhiteSpace(CaptureText)) return;
        _store.Capture(CaptureText, "app");
        CaptureText = "";
    }

    [RelayCommand]
    private void PromoteInboxItem(InboxItemViewModel item) =>
        _store.PromoteToProject(item.Id);

    [RelayCommand]
    private void DismissInboxItem(InboxItemViewModel item) =>
        _store.MarkProcessed(item.Id);

    // ---------- Projects ----------

    [RelayCommand]
    private void AddProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;
        _store.AddProject(NewProjectName, NewProjectNextAction);
        NewProjectName = "";
        NewProjectNextAction = "";
    }

    [RelayCommand]
    private void SaveNextAction(ProjectItemViewModel p) =>
        _store.SetNextAction(p.Id, p.NextActionDraft);

    [RelayCommand]
    private void TouchProject(ProjectItemViewModel p) =>
        _store.Touch(p.Id);

    [RelayCommand]
    private void ParkProject(ProjectItemViewModel p) =>
        _store.SetStatus(p.Id, ProjectStatus.Parked);

    [RelayCommand]
    private void ReactivateProject(ProjectItemViewModel p) =>
        _store.SetStatus(p.Id, ProjectStatus.Active);

    [RelayCommand]
    private void CompleteProject(ProjectItemViewModel p) =>
        _store.SetStatus(p.Id, ProjectStatus.Done);

    [RelayCommand]
    private void DeleteProject(ProjectItemViewModel p) =>
        _store.DeleteProject(p.Id);

    // ---------- Focus / Now mode ----------

    [RelayCommand]
    private void SetFocus(ProjectItemViewModel p) =>
        _store.FocusProjectId = p.Id;

    [RelayCommand]
    private void ClearFocus() =>
        _store.FocusProjectId = null;

    /// <summary>
    /// "I did the thing." Records the win by touching the project and swaps
    /// in the next tiny step you typed; keeps the momentum loop going.
    /// </summary>
    [RelayCommand]
    private void CompleteFocusStep()
    {
        if (FocusProject is null) return;
        _store.SetNextAction(FocusProject.Id, FocusNextStepDraft);
        FocusNextStepDraft = "";
    }

    [RelayCommand]
    private void TouchFocus()
    {
        if (FocusProject is null) return;
        _store.Touch(FocusProject.Id);
    }

    // ---------- Settings ----------

    [RelayCommand]
    private void SaveStaleDays()
    {
        if (int.TryParse(StaleDaysText, out var days) && days > 0)
            _store.StaleDays = days;
    }
}
