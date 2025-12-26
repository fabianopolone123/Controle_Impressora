using System.Diagnostics.Eventing.Reader;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrintControl.Agent.Models;

namespace PrintControl.Agent.Services;

public sealed class PrintWatcherService : BackgroundService
{
    private readonly DiskQueue _queue;
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<PrintWatcherService> _logger;
    private readonly PrintControlOptions _options;
    private EventLogWatcher? _watcher;

    public PrintWatcherService(
        DiskQueue queue,
        IHttpClientFactory clientFactory,
        IOptions<PrintControlOptions> options,
        ILogger<PrintWatcherService> logger)
    {
        _queue = queue;
        _clientFactory = clientFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TryEnableLog();
        StartWatcher();

        await FlushQueueAsync(stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.FlushIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FlushQueueAsync(stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private void StartWatcher()
    {
        try
        {
            var query = new EventLogQuery(
                "Microsoft-Windows-PrintService/Operational",
                PathType.LogName,
                "*[System[(EventID=307)]]");

            _watcher = new EventLogWatcher(query);
            _watcher.EventRecordWritten += OnEventRecordWritten;
            _watcher.Enabled = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nao foi possivel iniciar o monitoramento de impressao.");
        }
    }

    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventException is not null)
        {
            _logger.LogWarning(e.EventException, "Erro ao ler evento de impressao.");
            return;
        }

        if (e.EventRecord is null)
        {
            return;
        }

        using var record = e.EventRecord;
        if (!PrintEventParser.TryParse(record, out var job))
        {
            return;
        }

        if (!_options.IncludeDocumentName)
        {
            job = job with { DocumentName = null };
        }

        _ = Task.Run(() => SendOrQueueAsync(job), CancellationToken.None);
    }

    private async Task SendOrQueueAsync(PrintJobPayload job)
    {
        if (!await TrySendAsync(job, CancellationToken.None))
        {
            _queue.Enqueue(job);
        }
    }

    private async Task<bool> TrySendAsync(PrintJobPayload job, CancellationToken ct)
    {
        try
        {
            using var client = _clientFactory.CreateClient("host");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/print-jobs")
            {
                Content = JsonContent.Create(job)
            };

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _options.ApiKey);
            }

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Host respondeu {StatusCode} para envio de impressao.", response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar impressao para o host.");
            return false;
        }
    }

    private async Task FlushQueueAsync(CancellationToken ct)
    {
        var pending = _queue.Snapshot();
        if (pending.Count == 0)
        {
            return;
        }

        var sent = 0;
        foreach (var job in pending)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (await TrySendAsync(job, ct))
            {
                sent++;
            }
            else
            {
                break;
            }
        }

        if (sent > 0)
        {
            _queue.RemoveFirst(sent);
        }
    }

    private void TryEnableLog()
    {
        try
        {
            using var config = new EventLogConfiguration("Microsoft-Windows-PrintService/Operational");
            if (!config.IsEnabled)
            {
                config.IsEnabled = true;
                config.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nao foi possivel habilitar o log de impressao.");
        }
    }
}
