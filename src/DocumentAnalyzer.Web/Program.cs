using DocumentAnalyzer.Core.Abstractions;
using DocumentAnalyzer.Infrastructure.DependencyInjection;
using DocumentAnalyzer.Infrastructure.Storage;
using DocumentAnalyzer.Web.Components;
using DocumentAnalyzer.Web.Runs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Analysis services, the model provider selected in appsettings (Llm:Provider) and file storage.
builder.Services.AddDocumentAnalyzer(builder.Configuration);

// Relative folders in appsettings are resolved against the web project folder.
builder.Services.PostConfigure<StorageOptions>(options =>
{
    options.InputFolder = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.InputFolder));
    options.OutputFolder = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.OutputFolder));
});

// Owns the single run allowed at a time, so a run survives a browser refresh.
builder.Services.AddSingleton<AnalysisRunCoordinator>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// Download endpoint for saved JSON reports; the store only serves files from its own folder.
app.MapGet("/reports/{fileName}", async (string fileName, IReportStore store) =>
{
    var stream = await store.OpenReadAsync(fileName);

    return stream is null ? Results.NotFound() : Results.File(stream, "application/json", fileName);
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
