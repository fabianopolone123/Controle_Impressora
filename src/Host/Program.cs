using Microsoft.Extensions.Options;
using PrintControl.Host;
using PrintControl.Host.Data;
using PrintControl.Host.Models;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Host.UseWindowsService();

builder.Services.Configure<PrintControlOptions>(builder.Configuration.GetSection("PrintControl"));
var options = builder.Configuration.GetSection("PrintControl").Get<PrintControlOptions>() ?? new PrintControlOptions();
if (!string.IsNullOrWhiteSpace(options.ListenUrls))
{
    builder.WebHost.UseUrls(options.ListenUrls);
}

builder.Services.AddSingleton<PrintJobRepository>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var repository = app.Services.GetRequiredService<PrintJobRepository>();
await repository.InitializeAsync();

app.MapPost("/api/print-jobs", async (
    HttpRequest request,
    PrintJobIngest ingest,
    PrintJobRepository repo,
    IOptions<PrintControlOptions> opts,
    CancellationToken ct) =>
{
    if (!ApiKeyValidator.IsAuthorized(request, opts.Value.ApiKey))
    {
        return Results.Unauthorized();
    }

    var id = await repo.InsertAsync(ingest.ToRecord(), ct);
    return Results.Ok(new { id });
});

app.MapGet("/api/print-jobs", async (HttpRequest request, PrintJobRepository repo, CancellationToken ct) =>
{
    var filter = PrintJobFilterParser.FromQuery(request.Query);
    var results = await repo.QueryAsync(filter, ct);
    return Results.Ok(results);
});

app.MapGet("/api/print-jobs/export", async (HttpRequest request, HttpResponse response, PrintJobRepository repo, CancellationToken ct) =>
{
    var filter = PrintJobFilterParser.FromQuery(request.Query);
    var results = await repo.QueryAsync(filter, ct);
    response.Headers.ContentDisposition = "attachment; filename=print-jobs.csv";
    await CsvWriter.WriteAsync(response, results, ct);
});

app.MapGet("/api/stats/users", async (HttpRequest request, PrintJobRepository repo, CancellationToken ct) =>
{
    var filter = PrintJobFilterParser.FromQuery(request.Query);
    var results = await repo.GetTotalsByUserAsync(filter, ct);
    return Results.Ok(results);
});

app.MapGet("/api/stats/printers", async (HttpRequest request, PrintJobRepository repo, CancellationToken ct) =>
{
    var filter = PrintJobFilterParser.FromQuery(request.Query);
    var results = await repo.GetTotalsByPrinterAsync(filter, ct);
    return Results.Ok(results);
});

app.MapGet("/api/stats/summary", async (HttpRequest request, PrintJobRepository repo, CancellationToken ct) =>
{
    var filter = PrintJobFilterParser.FromQuery(request.Query);
    var result = await repo.GetSummaryAsync(filter, ct);
    return Results.Ok(result);
});

app.Run();
