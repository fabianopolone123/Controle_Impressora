using System.Text;
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

if (!string.IsNullOrWhiteSpace(options.DashboardPassword))
{
    app.Use(async (context, next) =>
    {
        if (IsAgentIngestRequest(context.Request))
        {
            await next();
            return;
        }

        if (IsAuthorized(context.Request, options.DashboardUser, options.DashboardPassword))
        {
            await next();
            return;
        }

        context.Response.Headers.WWWAuthenticate = "Basic realm=\"PrintControl\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    });
}

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

static bool IsAgentIngestRequest(HttpRequest request)
{
    return HttpMethods.IsPost(request.Method)
        && request.Path.Equals("/api/print-jobs", StringComparison.OrdinalIgnoreCase);
}

static bool IsAuthorized(HttpRequest request, string expectedUser, string expectedPassword)
{
    if (!request.Headers.TryGetValue("Authorization", out var headerValues))
    {
        return false;
    }

    var header = headerValues.ToString();
    if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var encoded = header["Basic ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(encoded))
    {
        return false;
    }

    string decoded;
    try
    {
        decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }
    catch (FormatException)
    {
        return false;
    }

    var separatorIndex = decoded.IndexOf(':');
    if (separatorIndex <= 0)
    {
        return false;
    }

    var user = decoded[..separatorIndex];
    var password = decoded[(separatorIndex + 1)..];
    return string.Equals(user, expectedUser, StringComparison.Ordinal)
        && string.Equals(password, expectedPassword, StringComparison.Ordinal);
}
