using System.Collections.Concurrent;

namespace TurneroTcs.Services;

public sealed class GenerationPreviewProgressTracker
{
    private static readonly TimeSpan FinishedRetention = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, GenerationPreviewProgressSnapshot> _operations = new(StringComparer.Ordinal);

    public void Start(string operationId, int totalWeeks, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        CleanupExpired();
        var now = DateTimeOffset.UtcNow;
        _operations[operationId] = new GenerationPreviewProgressSnapshot
        {
            OperationId = operationId,
            Status = "running",
            TotalWeeks = Math.Max(1, totalWeeks),
            CompletedWeeks = 0,
            Percentage = 0,
            Message = message ?? "Starting generation...",
            UpdatedAtUtc = now
        };
    }

    public void ReportProgress(string operationId, int completedWeeks, int totalWeeks, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        CleanupExpired();
        var total = Math.Max(1, totalWeeks);
        var completed = Math.Clamp(completedWeeks, 0, total);
        var percentage = (int)Math.Round((double)completed * 100d / total, MidpointRounding.AwayFromZero);

        _operations.AddOrUpdate(
            operationId,
            _ => new GenerationPreviewProgressSnapshot
            {
                OperationId = operationId,
                Status = "running",
                TotalWeeks = total,
                CompletedWeeks = completed,
                Percentage = percentage,
                Message = message ?? $"Generando Turnos... {percentage}%",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            (_, existing) => existing with
            {
                Status = "running",
                TotalWeeks = total,
                CompletedWeeks = completed,
                Percentage = percentage,
                Message = message ?? $"Generando Turnos... {percentage}%",
                Error = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = null
            });
    }

    public void Complete(string operationId, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        if (_operations.TryGetValue(operationId, out var existing))
        {
            var total = Math.Max(1, existing.TotalWeeks);
            _operations[operationId] = existing with
            {
                Status = "completed",
                CompletedWeeks = total,
                Percentage = 100,
                Message = message ?? "Generación Completada",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public void Fail(string operationId, string error)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return;
        }

        if (_operations.TryGetValue(operationId, out var existing))
        {
            _operations[operationId] = existing with
            {
                Status = "failed",
                Error = error,
                Message = error,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }

    public bool TryGet(string operationId, out GenerationPreviewProgressSnapshot snapshot)
    {
        CleanupExpired();
        if (_operations.TryGetValue(operationId, out snapshot!))
        {
            return true;
        }

        snapshot = new GenerationPreviewProgressSnapshot();
        return false;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _operations)
        {
            if (pair.Value.CompletedAtUtc.HasValue &&
                now - pair.Value.CompletedAtUtc.Value > FinishedRetention)
            {
                _operations.TryRemove(pair.Key, out _);
            }
        }
    }
}

public sealed record GenerationPreviewProgressSnapshot
{
    public string OperationId { get; init; } = string.Empty;
    public string Status { get; init; } = "unknown";
    public int TotalWeeks { get; init; }
    public int CompletedWeeks { get; init; }
    public int Percentage { get; init; }
    public string? Message { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}

public sealed record GenerationProgressUpdate(int CompletedWeeks, int TotalWeeks, string? Message = null);
