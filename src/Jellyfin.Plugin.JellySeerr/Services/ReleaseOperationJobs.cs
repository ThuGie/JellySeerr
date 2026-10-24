using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

/// <summary>Short HTTP requests poll bounded operations; reverse proxies need not hold an indexer search open.</summary>
public sealed class ReleaseOperationJobs(ILogger<ReleaseOperationJobs> logger)
{
    private sealed record Job(Guid UserId, int RequestId, string Key, Task<JObject> Task, DateTime Started);
    private readonly Dictionary<string, Job> _jobs = new();
    private readonly object _gate = new();

    public JObject Start(ReleaseContext context, string operationKey, Func<CancellationToken, Task<JObject>> action)
    {
        lock (_gate)
        {
            foreach (var old in _jobs.Where(p => p.Value.Task.IsCompleted && DateTime.UtcNow - p.Value.Started > TimeSpan.FromMinutes(30)).ToArray())
                _jobs.Remove(old.Key);
            string key = context.Key + ":" + operationKey;
            var existing = _jobs.FirstOrDefault(p => p.Value.Key == key);
            if (existing.Value != null) return Snapshot(existing.Key, existing.Value);
            if (_jobs.Count >= 128 || _jobs.Values.Count(j => !j.Task.IsCompleted) >= 16
                || _jobs.Values.Count(j => j.UserId == context.UserId && !j.Task.IsCompleted) >= 2)
                throw new ReleaseSelectionException(429, "Too many release operations are running. Wait for the current search to finish.");
            string token = Guid.NewGuid().ToString("N");
            Task<JObject> task = Task.Run(async () =>
            {
                using CancellationTokenSource limit = new(TimeSpan.FromMinutes(20));
                try { return new JObject { ["state"] = "completed", ["result"] = await action(limit.Token) }; }
                catch (ReleaseSelectionException ex)
                {
                    logger.LogWarning("JS • release operation for request {RequestId} failed ({Status}): {Reason}", context.RequestId, ex.Status, ex.Message);
                    return Failure(ex.Status, ex.Message);
                }
                catch (OperationCanceledException) { return Failure(504, "The release operation timed out. Check Sonarr/Radarr activity before starting another operation."); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "JS • release operation failed for request {RequestId}", context.RequestId);
                    return Failure(502, "The release operation failed unexpectedly. Check the Jellyfin server log for this request.");
                }
            });
            var job = new Job(context.UserId, context.RequestId, key, task, DateTime.UtcNow);
            _jobs[token] = job;
            return Snapshot(token, job);
        }
    }

    public JObject Get(Guid userId, int requestId, string token)
    {
        lock (_gate)
        {
            if (!_jobs.TryGetValue(token, out Job? job) || job.UserId != userId || job.RequestId != requestId
                || DateTime.UtcNow - job.Started > TimeSpan.FromMinutes(30))
                throw new ReleaseSelectionException(404, "This release operation expired or is unavailable. Refresh and search again.");
            return Snapshot(token, job);
        }
    }

    private static JObject Failure(int status, string message) => new() { ["state"] = "failed", ["status"] = status, ["message"] = message };
    private static JObject Snapshot(string token, Job job)
    {
        JObject result = job.Task.IsCompletedSuccessfully ? (JObject)job.Task.Result.DeepClone() : new JObject { ["state"] = "running" };
        result["jobToken"] = token;
        result["elapsedSeconds"] = (int)(DateTime.UtcNow - job.Started).TotalSeconds;
        return result;
    }
}
