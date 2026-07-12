using System.Collections.ObjectModel;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squirrel.App.Views;
using Squirrel.Core;

namespace Squirrel.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SquirrelStore _store;
    private int _suggestionOffset;

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
    private double newProjectPriority = 5;

    [ObservableProperty]
    private string newProjectDescription = "";

    [ObservableProperty]
    private DateTimeOffset? newProjectDueDate;

    [ObservableProperty]
    private ProjectItemViewModel? focusProject;

    [ObservableProperty]
    private bool hasFocus;

    [ObservableProperty]
    private string focusNextStepDraft = "";

    [ObservableProperty]
    private string focusQueueHint = "";

    // Now-tab suggestion: Squirrel opens with an offer, never a blank choice.
    [ObservableProperty]
    private ProjectItemViewModel? suggestedProject;

    [ObservableProperty]
    private bool showSuggestion;

    [ObservableProperty]
    private bool showEmpty;

    [ObservableProperty]
    private string suggestedMeta = "";

    [ObservableProperty]
    private string inboxTabHeader = "Inbox";

    [ObservableProperty]
    private string staleTabHeader = "Resurface";

    [ObservableProperty]
    private string staleDaysText = "7";

    [ObservableProperty]
    private string selectedTheme;

    public string[] ThemeOptions { get; } = { "System", "Light", "Dark" };

    public string ApiBaseUrl { get; }
    public string ApiKey { get; }
    public string CurlExample { get; }
    public string DbPath { get; }
    public string ManualText { get; }

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
        selectedTheme = store.GetSetting("Theme") ?? "System";
        ManualText = LoadManual();

        // Refresh the UI whenever anything writes to the store,
        // including captures arriving over the local API.
        _store.Changed += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    private static string LoadManual()
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://Squirrel/Assets/MANUAL.md"));
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return "Manual could not be loaded. The full manual also lives in the repo at src/Squirrel.App/Assets/MANUAL.md.";
        }
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
            Projects.Add(new ProjectItemViewModel(
                p, staleDays, _store.GetQueue(p.Id), _store.GetHistory(p.Id)));

        StaleProjects.Clear();
        foreach (var p in _store.GetStaleProjects())
            StaleProjects.Add(new ProjectItemViewModel(p, staleDays));
        StaleTabHeader = StaleProjects.Count > 0 ? $"Resurface ({StaleProjects.Count})" : "Resurface";

        var focusId = _store.FocusProjectId;
        FocusProject = string.IsNullOrEmpty(focusId)
            ? null
            : Projects.FirstOrDefault(p => p.Id == focusId);
        HasFocus = FocusProject is not null;

        FocusQueueHint = FocusProject is { QueueCount: > 0 } fp
            ? $"{fp.QueueCount} step{(fp.QueueCount == 1 ? "" : "s")} queued; finish with the box empty to pull the next one."
            : "";

        UpdateSuggestion();
    }

    /// <summary>
    /// With no focus set, offer the highest-urgency active project instead of
    /// presenting a blank "choose something" wall. "Something else" cycles
    /// down the urgency-sorted list.
    /// </summary>
    private void UpdateSuggestion()
    {
        var active = Projects.Where(p => p.IsActive).ToList();

        ShowEmpty = !HasFocus && active.Count == 0;

        if (HasFocus || active.Count == 0)
        {
            SuggestedProject = null;
            ShowSuggestion = false;
            SuggestedMeta = "";
            return;
        }

        var index = ((_suggestionOffset % active.Count) + active.Count) % active.Count;
        SuggestedProject = active[index];  // Projects is already urgency-sorted
        SuggestedMeta = SuggestedProject.MetaText;
        ShowSuggestion = true;
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
        _store.AddProject(
            NewProjectName, NewProjectNextAction,
            notes: NewProjectDescription,
            priority: (int)Math.Round(NewProjectPriority),
            dueDate: NewProjectDueDate);
        NewProjectName = "";
        NewProjectNextAction = "";
        NewProjectDescription = "";
        NewProjectPriority = 5;
        NewProjectDueDate = null;
    }

    [RelayCommand]
    private void ClearNewDueDate() => NewProjectDueDate = null;

    /// <summary>Save a project card's edits: next action, priority, due date, description.</summary>
    [RelayCommand]
    private void SaveProject(ProjectItemViewModel p) =>
        _store.UpdateProjectMeta(
            p.Id, p.NextActionDraft,
            (int)Math.Round(p.PriorityDraft), p.DueDateDraft,
            p.DescriptionDraft);

    [RelayCommand]
    private void ClearDueDate(ProjectItemViewModel p) => p.DueDateDraft = null;

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

    [RelayCommand]
    private void AcceptSuggestion()
    {
        if (SuggestedProject is not null)
            _store.FocusProjectId = SuggestedProject.Id;
    }

    [RelayCommand]
    private void NextSuggestion()
    {
        _suggestionOffset++;
        UpdateSuggestion();
    }

    [RelayCommand]
    private void GoToProjects() => SelectedTabIndex = 2;

    /// <summary>
    /// "I did the thing." Logs the finished step to the project's win
    /// history, then swaps in what you typed, or pulls the top of the
    /// up-next queue when the box is empty.
    /// </summary>
    [RelayCommand]
    private void CompleteFocusStep()
    {
        if (FocusProject is null) return;
        _store.CompleteNextAction(FocusProject.Id, FocusNextStepDraft);
        FocusNextStepDraft = "";
    }

    // ---------- Up-next queue ----------

    [RelayCommand]
    private void AddQueuedStep(ProjectItemViewModel p)
    {
        if (string.IsNullOrWhiteSpace(p.NewStepDraft)) return;
        _store.AddQueuedAction(p.Id, p.NewStepDraft);
        p.NewStepDraft = "";
    }

    [RelayCommand]
    private void PromoteQueued(QueuedItemViewModel q) =>
        _store.PromoteQueuedAction(q.Id);

    [RelayCommand]
    private void MoveQueuedUp(QueuedItemViewModel q) =>
        _store.MoveQueuedAction(q.Id, up: true);

    [RelayCommand]
    private void MoveQueuedDown(QueuedItemViewModel q) =>
        _store.MoveQueuedAction(q.Id, up: false);

    [RelayCommand]
    private void DeleteQueued(QueuedItemViewModel q) =>
        _store.DeleteQueuedAction(q.Id);

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

    [RelayCommand]
    private void OpenManual() => new ManualWindow(ManualText).Show();

    partial void OnSelectedThemeChanged(string value)
    {
        _store.SetSetting("Theme", value);
        ApplyTheme(value);
    }

    public static void ApplyTheme(string theme)
    {
        var app = Avalonia.Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
