using DocumentAnalyzer.Core;
using DocumentAnalyzer.Core.Analysis;

namespace DocumentAnalyzer.Web.Runs;

public sealed record RunRequest(Constants.AnalysisMode Mode, DocumentInput Requirements, DocumentInput? Design);

/// <summary>State of the current run. Written by the background run, read by the UI.</summary>
public sealed class RunInfo
{
    public required string Title { get; init; }
    public required Constants.AnalysisMode Mode { get; init; }
    public DateTime StartedUtc { get; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; set; }
    public Constants.RunStatus Status { get; set; } = Constants.RunStatus.Running;
    public Constants.RunStage Stage { get; set; } = Constants.RunStage.LoadingDocuments;
    public AnalysisRunResult? Result { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Singleton that owns the one run allowed at a time. The run executes on a background task, so it keeps going when the
/// browser page is refreshed or closed, and components subscribe to Changed to redraw. Run history is the report files on disk.
/// </summary>
public sealed class AnalysisRunCoordinator(IServiceScopeFactory scopeFactory)
{
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;

    public RunInfo? Current { get; private set; }

    public bool IsRunning => Current?.Status == Constants.RunStatus.Running;

    /// <summary>Raised from background threads; handlers must marshal to the UI thread.</summary>
    public event Action? Changed;

    public bool TryStart(RunRequest request)
    {
        RunInfo run;
        CancellationToken token;

        lock (gate)
        {
            if (IsRunning)
            {
                return false;
            }

            run = new RunInfo { Title = BuildTitle(request), Mode = request.Mode };
            Current = run;
            cancellation = new CancellationTokenSource();
            token = cancellation.Token;
        }

        Changed?.Invoke();

        _ = Task.Run(() => ExecuteAsync(run, request, token));

        return true;
    }

    public void Cancel()
    {
        lock (gate)
        {
            cancellation?.Cancel();
        }
    }

    private async Task ExecuteAsync(RunInfo run, RunRequest request, CancellationToken token)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AnalysisService>();

            var progress = new Progress<Constants.RunStage>(stage =>
            {
                run.Stage = stage;
                Changed?.Invoke();
            });

            run.Result = request.Mode == Constants.AnalysisMode.Comparison
                ? await service.CompareAsync(request.Requirements, request.Design!, progress, token)
                : await service.AnalyzeAsync(request.Requirements, progress, token);

            run.Status = Constants.RunStatus.Succeeded;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            run.Status = Constants.RunStatus.Cancelled;
        }
        catch (OperationCanceledException)
        {
            run.Status = Constants.RunStatus.Failed;
            run.Error = "The model request timed out. Try a smaller document or raise ApiRequestTimeout in the settings.";
        }
        catch (Exception ex)
        {
            run.Status = Constants.RunStatus.Failed;
            run.Error = ex.Message;
        }
        finally
        {
            run.FinishedUtc = DateTime.UtcNow;
            Changed?.Invoke();
        }
    }

    private static string BuildTitle(RunRequest request) =>
        request.Mode == Constants.AnalysisMode.Comparison && request.Design is not null
            ? $"{request.Requirements.Name} vs {request.Design.Name}"
            : request.Requirements.Name;
}
