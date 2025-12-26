using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PrintControl.Host.Models;

namespace PrintControl.Host.Data;

public sealed class PrintJobRepository
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public PrintJobRepository(IOptions<PrintControlOptions> options)
    {
        _dbPath = options.Value.DatabasePath;
        _connectionString = $"Data Source={_dbPath}";
    }

    public async Task InitializeAsync()
    {
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS print_jobs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    printed_at TEXT NOT NULL,
    user_name TEXT NOT NULL,
    machine_name TEXT NOT NULL,
    printer_name TEXT NOT NULL,
    pages INTEGER NOT NULL,
    bytes INTEGER NOT NULL,
    job_id TEXT,
    document_name TEXT
);
CREATE INDEX IF NOT EXISTS idx_print_jobs_printed_at ON print_jobs(printed_at);
CREATE INDEX IF NOT EXISTS idx_print_jobs_user_name ON print_jobs(user_name);
CREATE INDEX IF NOT EXISTS idx_print_jobs_printer_name ON print_jobs(printer_name);
";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<long> InsertAsync(PrintJobRecord record, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO print_jobs (printed_at, user_name, machine_name, printer_name, pages, bytes, job_id, document_name)
VALUES ($printed_at, $user_name, $machine_name, $printer_name, $pages, $bytes, $job_id, $document_name);
SELECT last_insert_rowid();
";

        command.Parameters.AddWithValue("$printed_at", ToDbDate(record.PrintedAt));
        command.Parameters.AddWithValue("$user_name", record.UserName);
        command.Parameters.AddWithValue("$machine_name", record.MachineName);
        command.Parameters.AddWithValue("$printer_name", record.PrinterName);
        command.Parameters.AddWithValue("$pages", record.Pages);
        command.Parameters.AddWithValue("$bytes", record.Bytes);
        command.Parameters.AddWithValue("$job_id", (object?)record.JobId ?? DBNull.Value);
        command.Parameters.AddWithValue("$document_name", (object?)record.DocumentName ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<PrintJobRecord>> QueryAsync(PrintJobFilter filter, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var parameters = new List<SqliteParameter>();
        var whereClause = BuildWhere(filter, parameters);

        var sql = new StringBuilder();
        sql.Append("SELECT id, printed_at, user_name, machine_name, printer_name, pages, bytes, job_id, document_name FROM print_jobs");
        sql.Append(whereClause);
        sql.Append(" ORDER BY printed_at DESC LIMIT $limit OFFSET $offset;");

        var command = connection.CreateCommand();
        command.CommandText = sql.ToString();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        command.Parameters.AddWithValue("$limit", filter.Limit);
        command.Parameters.AddWithValue("$offset", filter.Offset);

        var results = new List<PrintJobRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new PrintJobRecord
            {
                Id = reader.GetInt64(0),
                PrintedAt = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                UserName = reader.GetString(2),
                MachineName = reader.GetString(3),
                PrinterName = reader.GetString(4),
                Pages = reader.GetInt32(5),
                Bytes = reader.GetInt64(6),
                JobId = reader.IsDBNull(7) ? null : reader.GetString(7),
                DocumentName = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<AggregateRow>> GetTotalsByUserAsync(PrintJobFilter filter, CancellationToken ct)
    {
        return await GetAggregateAsync(filter, "user_name", 20, ct);
    }

    public async Task<IReadOnlyList<AggregateRow>> GetTotalsByPrinterAsync(PrintJobFilter filter, CancellationToken ct)
    {
        return await GetAggregateAsync(filter, "printer_name", 20, ct);
    }

    public async Task<SummaryRow> GetSummaryAsync(PrintJobFilter filter, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var parameters = new List<SqliteParameter>();
        var whereClause = BuildWhere(filter, parameters);

        var sql = new StringBuilder();
        sql.Append("SELECT COUNT(*), COALESCE(SUM(pages), 0) FROM print_jobs");
        sql.Append(whereClause);

        var command = connection.CreateCommand();
        command.CommandText = sql.ToString();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new SummaryRow
            {
                TotalJobs = reader.GetInt64(0),
                TotalPages = reader.GetInt64(1)
            };
        }

        return new SummaryRow();
    }

    private async Task<IReadOnlyList<AggregateRow>> GetAggregateAsync(PrintJobFilter filter, string column, int limit, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        var parameters = new List<SqliteParameter>();
        var whereClause = BuildWhere(filter, parameters);

        var sql = new StringBuilder();
        sql.Append($"SELECT {column}, COALESCE(SUM(pages), 0) AS total_pages, COUNT(*) AS jobs FROM print_jobs");
        sql.Append(whereClause);
        sql.Append($" GROUP BY {column} ORDER BY total_pages DESC LIMIT $limit;");

        var command = connection.CreateCommand();
        command.CommandText = sql.ToString();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<AggregateRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AggregateRow
            {
                Key = reader.IsDBNull(0) ? "" : reader.GetString(0),
                TotalPages = reader.GetInt64(1),
                Jobs = reader.GetInt64(2)
            });
        }

        return results;
    }

    private static string BuildWhere(PrintJobFilter filter, List<SqliteParameter> parameters)
    {
        var conditions = new List<string>();

        if (filter.From is not null)
        {
            conditions.Add("printed_at >= $from");
            parameters.Add(new SqliteParameter("$from", ToDbDate(filter.From.Value)));
        }

        if (filter.To is not null)
        {
            conditions.Add("printed_at <= $to");
            parameters.Add(new SqliteParameter("$to", ToDbDate(filter.To.Value)));
        }

        if (!string.IsNullOrWhiteSpace(filter.User))
        {
            conditions.Add("user_name LIKE $user ESCAPE '\\' COLLATE NOCASE");
            parameters.Add(new SqliteParameter("$user", ToLike(filter.User)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Machine))
        {
            conditions.Add("machine_name LIKE $machine ESCAPE '\\' COLLATE NOCASE");
            parameters.Add(new SqliteParameter("$machine", ToLike(filter.Machine)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Printer))
        {
            conditions.Add("printer_name LIKE $printer ESCAPE '\\' COLLATE NOCASE");
            parameters.Add(new SqliteParameter("$printer", ToLike(filter.Printer)));
        }

        if (filter.MinPages is not null)
        {
            conditions.Add("pages >= $minPages");
            parameters.Add(new SqliteParameter("$minPages", filter.MinPages.Value));
        }

        if (filter.MaxPages is not null)
        {
            conditions.Add("pages <= $maxPages");
            parameters.Add(new SqliteParameter("$maxPages", filter.MaxPages.Value));
        }

        return conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
    }

    private static string ToDbDate(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static string ToLike(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
