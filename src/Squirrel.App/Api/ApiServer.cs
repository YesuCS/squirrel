using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Squirrel.Core;

namespace Squirrel.App.Api;

/// <summary>
/// A tiny HTTP API hosted inside the desktop app on localhost, so you can
/// throw tasks at Squirrel from anywhere: curl, Apple Shortcuts, Alfred,
/// Stream Deck, a Rock workflow, whatever. Auth is a single API key sent as
/// the X-Api-Key header (shown in the app's Settings tab).
///
///   curl -X POST http://127.0.0.1:53595/capture \
///     -H "X-Api-Key: YOUR_KEY" -H "Content-Type: application/json" \
///     -d '{"text":"idea: squirrel but for sermon clips","source":"curl"}'
/// </summary>
public class ApiServer
{
    public const int DefaultPort = 53595;

    private WebApplication? _app;
    private readonly SquirrelStore _store;

    public ApiServer(SquirrelStore store) => _store = store;

    public string BaseUrl => $"http://127.0.0.1:{DefaultPort}";

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(BaseUrl);

        var app = builder.Build();

        // API key check on everything except /health.
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path == "/health")
            {
                await next(ctx);
                return;
            }
            var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (key != _store.ApiKey)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "missing or invalid X-Api-Key" });
                return;
            }
            await next(ctx);
        });

        app.MapGet("/health", () => Results.Json(new { ok = true, app = "squirrel" }));

        // -- Capture inbox --
        app.MapPost("/capture", (CaptureRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest(new { error = "text is required" });
            var item = _store.Capture(req.Text, string.IsNullOrWhiteSpace(req.Source) ? "api" : req.Source!);
            return Results.Json(new { item.Id, item.Text, item.Source, item.CreatedAt }, statusCode: 201);
        });

        app.MapGet("/inbox", () =>
            Results.Json(_store.GetInbox().Select(i => new { i.Id, i.Text, i.Source, i.CreatedAt })));

        // -- Projects --
        app.MapGet("/projects", () =>
            Results.Json(_store.GetProjects().Select(ProjectDto)));

        app.MapGet("/projects/stale", () =>
            Results.Json(_store.GetStaleProjects().Select(ProjectDto)));

        app.MapPost("/projects", (ProjectRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required" });

            DateTimeOffset? due = null;
            if (!string.IsNullOrWhiteSpace(req.DueDate))
            {
                if (!DateTimeOffset.TryParse(req.DueDate, out var parsed))
                    return Results.BadRequest(new { error = "dueDate must be a valid date, e.g. 2026-08-01" });
                due = parsed;
            }

            var p = _store.AddProject(
                req.Name, req.NextAction ?? "", req.Notes ?? "",
                req.Priority ?? 5, due);
            return Results.Json(ProjectDto(p), statusCode: 201);
        });

        app.MapPost("/projects/{id}/touch", (string id) =>
        {
            if (_store.GetProject(id) is null) return Results.NotFound();
            _store.Touch(id);
            return Results.Json(ProjectDto(_store.GetProject(id)!));
        });

        app.MapPost("/projects/{id}/next-action", (string id, NextActionRequest req) =>
        {
            if (_store.GetProject(id) is null) return Results.NotFound();
            _store.SetNextAction(id, req.NextAction ?? "");
            return Results.Json(ProjectDto(_store.GetProject(id)!));
        });

        _app = app;
        await app.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_app is not null)
            await _app.StopAsync();
    }

    private static object ProjectDto(Project p) => new
    {
        p.Id,
        p.Name,
        p.NextAction,
        p.Notes,
        Status = p.Status.ToString(),
        p.Priority,
        p.DueDate,
        p.DaysUntilDue,
        p.IsOverdue,
        p.Score,
        p.CreatedAt,
        p.LastTouchedAt,
        p.DaysSinceTouch
    };

    public record CaptureRequest(string? Text, string? Source);
    public record ProjectRequest(string? Name, string? NextAction, string? Notes, int? Priority, string? DueDate);
    public record NextActionRequest(string? NextAction);
}
