using DocumentAnalyzer.Core;
using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Core.Analysis;
using DocumentAnalyzer.Web.Runs;
using Microsoft.AspNetCore.Components;

namespace DocumentAnalyzer.Web.Components.Pages;

public partial class Home : IDisposable
{
    [Inject] private AnalysisRunCoordinator Coordinator { get; set; } = default!;
    [Inject] private AnalysisService Analysis { get; set; } = default!;

    private Constants.AnalysisMode mode = Constants.AnalysisMode.Comparison;
    private bool usePastedText;
    private IReadOnlyList<DocumentInfo> requirementDocs = [];
    private IReadOnlyList<DocumentInfo> designDocs = [];
    private string? selectedRequirements;
    private string? selectedDesign;
    private string requirementsPreview = string.Empty;
    private string designPreview = string.Empty;
    private string pastedRequirements = string.Empty;
    private string pastedDesign = string.Empty;
    private string? formError;
    private IReadOnlyList<ReportSummary> recentRuns = [];
    private ReportView? viewed;
    private RunInfo? lastHandledRun;
    private CancellationTokenSource? tickerCancellation;

    protected override async Task OnInitializedAsync()
    {
        requirementDocs = await Analysis.ListDocumentsAsync(Constants.DocumentKind.Requirements);
        designDocs = await Analysis.ListDocumentsAsync(Constants.DocumentKind.Design);

        selectedRequirements = requirementDocs.FirstOrDefault()?.Name;
        await OnRequirementsChangedAsync();

        recentRuns = await Analysis.ListReportsAsync();

        Coordinator.Changed += OnCoordinatorChanged;
        await HandleCoordinatorChangeAsync(); // picks up a run that finished while this page was closed

        // Redraw once a second so the elapsed time on the progress view keeps moving.
        tickerCancellation = new CancellationTokenSource();
        _ = TickAsync(tickerCancellation.Token);
    }

    // ------------------------------------------------------------------
    // Documents
    // ------------------------------------------------------------------

    /// <summary>Picking a requirements document also selects the design of the same scenario, when there is one.</summary>
    private async Task OnRequirementsChangedAsync()
    {
        requirementsPreview = await ReadPreviewAsync(Constants.DocumentKind.Requirements, selectedRequirements);

        var scenario = requirementDocs.FirstOrDefault(d => d.Name == selectedRequirements)?.Scenario;
        var paired = designDocs.FirstOrDefault(d => d.Scenario == scenario);

        selectedDesign = paired?.Name ?? selectedDesign ?? designDocs.FirstOrDefault()?.Name;

        await OnDesignChangedAsync();
    }

    private async Task OnDesignChangedAsync() =>
        designPreview = await ReadPreviewAsync(Constants.DocumentKind.Design, selectedDesign);

    private async Task<string> ReadPreviewAsync(Constants.DocumentKind kind, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        try
        {
            var text = await Analysis.ReadDocumentAsync(kind, name);
            var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Take(3);

            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return $"Could not read the file: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------
    // Starting, cancelling and viewing runs
    // ------------------------------------------------------------------

    private async Task StartRunAsync()
    {
        formError = null;

        try
        {
            var request = await BuildRequestAsync();

            if (!Coordinator.TryStart(request))
            {
                formError = "A run is already in progress.";
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            formError = ex.Message;
        }
    }

    private async Task<RunRequest> BuildRequestAsync()
    {
        var comparison = mode == Constants.AnalysisMode.Comparison;
        DocumentInput requirements;
        DocumentInput? design = null;

        if (usePastedText)
        {
            requirements = PreparePasted(pastedRequirements, "Pasted requirements", "REQ", "Paste the requirements text first.");

            if (comparison)
            {
                design = PreparePasted(pastedDesign, "Pasted technical design", "DESIGN", "Paste the technical design text first.");
            }
        }
        else
        {
            requirements = await ReadSelectedAsync(Constants.DocumentKind.Requirements, selectedRequirements, "Choose a requirements document.");

            if (comparison)
            {
                design = await ReadSelectedAsync(Constants.DocumentKind.Design, selectedDesign, "Choose a technical design document.");
            }
        }

        return new RunRequest(mode, requirements, design);
    }

    private static DocumentInput PreparePasted(string text, string name, string idPrefix, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(emptyMessage);
        }

        return new DocumentInput(name, StatementNumbering.Prepare(text, idPrefix).Text);
    }

    private async Task<DocumentInput> ReadSelectedAsync(Constants.DocumentKind kind, string? name, string missingMessage)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException(missingMessage);
        }

        return new DocumentInput(name, await Analysis.ReadDocumentAsync(kind, name));
    }

    private void CancelRun() => Coordinator.Cancel();

    private void CloseResult() => viewed = null;

    private void ResetForm()
    {
        formError = null;
        pastedRequirements = string.Empty;
        pastedDesign = string.Empty;
    }

    private async Task OpenReportAsync(string fileName)
    {
        formError = null;

        var report = await Analysis.LoadReportAsync(fileName);

        if (report is null)
        {
            formError = $"Could not read {fileName}.";
            return;
        }

        var title = recentRuns.FirstOrDefault(r => r.FileName == fileName)?.Title ?? fileName;

        viewed = new ReportView(title, report, [], fileName, WarningsKnown: false);
    }

    // ------------------------------------------------------------------
    // Coordinator events (raised on background threads)
    // ------------------------------------------------------------------

    private void OnCoordinatorChanged() => _ = InvokeAsync(HandleCoordinatorChangeAsync);

    private async Task HandleCoordinatorChangeAsync()
    {
        var current = Coordinator.Current;

        if (current is not null && !ReferenceEquals(current, lastHandledRun) && current.Status != Constants.RunStatus.Running)
        {
            lastHandledRun = current;

            switch (current.Status)
            {
                case Constants.RunStatus.Succeeded when current.Result is { } result:
                    viewed = new ReportView(current.Title, result.Report, result.Warnings, result.ReportFileName, WarningsKnown: true);
                    recentRuns = await Analysis.ListReportsAsync();
                    break;

                case Constants.RunStatus.Failed:
                    formError = current.Error;
                    break;
            }
        }

        StateHasChanged();
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (Coordinator.IsRunning)
                {
                    await InvokeAsync(StateHasChanged);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // the page was closed
        }
    }

    // ------------------------------------------------------------------
    // Markup helpers
    // ------------------------------------------------------------------

    private static string SegClass(bool selected) => selected ? "da-seg-btn da-on" : "da-seg-btn";

    private static string Pressed(bool selected) => selected ? "true" : "false";

    private static string ModeLabel(Constants.AnalysisMode analysisMode) =>
        analysisMode == Constants.AnalysisMode.Comparison ? "Comparison" : "Analysis";

    private static string? NumberingNotice(string text, string idPrefix)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var result = StatementNumbering.Prepare(text, idPrefix);

        if (result.WasNumbered)
        {
            return $"No identifiers found. Each line will be numbered {idPrefix}-001, {idPrefix}-002 and so on.";
        }

        return result.LinesWithoutIds > 0
            ? $"{result.LinesWithoutIds} line(s) have no identifier and cannot be matched to a statement in the report."
            : null;
    }

    public void Dispose()
    {
        Coordinator.Changed -= OnCoordinatorChanged;
        tickerCancellation?.Cancel();
        tickerCancellation?.Dispose();
    }
}
