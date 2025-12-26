using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrintControl.Agent.Models;

namespace PrintControl.Agent.Services;

public sealed class DiskQueue
{
    private readonly string _path;
    private readonly ILogger<DiskQueue> _logger;
    private readonly object _lock = new();
    private readonly List<PrintJobPayload> _items = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = null };

    public DiskQueue(IOptions<PrintControlOptions> options, ILogger<DiskQueue> logger)
    {
        _logger = logger;
        _path = ResolveQueuePath(options.Value.QueuePath);
        if (string.IsNullOrWhiteSpace(_path))
        {
            return;
        }

        if (File.Exists(_path))
        {
            var fileInfo = new FileInfo(_path);
            if (fileInfo.Length == 0)
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var items = JsonSerializer.Deserialize<List<PrintJobPayload>>(json, _jsonOptions);
                if (items is { Count: > 0 })
                {
                    _items.AddRange(items);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Queue file corrupted. Starting fresh.");
            }
        }
    }

    public IReadOnlyList<PrintJobPayload> Snapshot()
    {
        lock (_lock)
        {
            return _items.ToList();
        }
    }

    public void Enqueue(PrintJobPayload job)
    {
        lock (_lock)
        {
            _items.Add(job);
            SaveUnsafe();
        }
    }

    public void RemoveFirst(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_lock)
        {
            var removeCount = Math.Min(count, _items.Count);
            _items.RemoveRange(0, removeCount);
            SaveUnsafe();
        }
    }

    private void SaveUnsafe()
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(_items, _jsonOptions);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save queue file.");
        }
    }

    private string ResolveQueuePath(string preferred)
    {
        var expandedPreferred = ExpandPath(preferred);
        if (TryEnsurePath(expandedPreferred))
        {
            return expandedPreferred;
        }

        var fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintControl",
            "Agent",
            "queue.json");

        if (TryEnsurePath(fallback))
        {
            _logger.LogWarning("Queue path fallback to {QueuePath}", fallback);
            return fallback;
        }

        _logger.LogWarning("Queue path disabled; running without disk queue.");
        return string.Empty;
    }

    private bool TryEnsurePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot access queue path {QueuePath}", path);
            return false;
        }
    }

    private static string ExpandPath(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? value
            : Environment.ExpandEnvironmentVariables(value);
    }
}
