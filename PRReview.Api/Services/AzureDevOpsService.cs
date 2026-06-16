using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PRReview.Api.Configuration;
using PRReview.Api.Models.AzureDevOps;
using PRReview.Api.Services.Interfaces;

namespace PRReview.Api.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private const int MaxFiles = 50;

    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AzureDevOpsService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureDevOpsOptions> options,
        ILogger<AzureDevOpsService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AzureDevOps");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PullRequestDetails> GetPullRequestByIdAsync(
        int prNumber, CancellationToken ct = default)
    {
        // Org-level endpoint: PR ids are unique across the whole organization,
        // so we can resolve the repository from the id alone.
        var url = BuildOrgUrl($"git/pullrequests/{prNumber}");
        _logger.LogInformation("Resolving PR #{PrNumber} via {Url}", prNumber, url);

        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, "resolve pull request");

        var json = await response.Content.ReadAsStringAsync(ct);
        var pr = JsonSerializer.Deserialize<PullRequestDetails>(json, JsonOpts)
                 ?? throw new InvalidOperationException("Failed to deserialize PR details.");

        if (string.IsNullOrEmpty(pr.Repository.Id))
            throw new InvalidOperationException(
                $"PR #{prNumber} did not return an owning repository.");

        return pr;
    }

    public async Task<string> GetPullRequestDiffAsync(
        string repositoryId, int prNumber, CancellationToken ct = default)
    {
        var iteration = await GetLatestIterationAsync(repositoryId, prNumber, ct);
        var baseCommit = iteration.CommonRefCommit.CommitId; // merge base
        var prCommit = iteration.SourceRefCommit.CommitId;   // PR tip

        if (string.IsNullOrEmpty(baseCommit) || string.IsNullOrEmpty(prCommit))
            throw new InvalidOperationException(
                $"PR #{prNumber} iteration {iteration.Id} is missing commit references.");

        var changedFiles = await GetIterationChangesAsync(repositoryId, prNumber, iteration.Id, ct);

        if (changedFiles.Count == 0)
        {
            _logger.LogWarning("No changed files in PR #{Pr} (repo {Repo})", prNumber, repositoryId);
            return "No file changes detected in this pull request.";
        }

        var diffBuilder = new StringBuilder();
        diffBuilder.AppendLine("=== CHANGED FILES SUMMARY ===");
        foreach (var file in changedFiles)
            diffBuilder.AppendLine($"{NormalizeChangeType(file.ChangeType)}: {file.Item.Path}");
        diffBuilder.AppendLine();

        var filesToReview = changedFiles.Take(MaxFiles).ToList();
        if (changedFiles.Count > MaxFiles)
            diffBuilder.AppendLine(
                $"(NOTE: {changedFiles.Count} files changed; reviewing the first {MaxFiles}.)\n");

        foreach (var file in filesToReview)
        {
            var changeType = file.ChangeType.ToLowerInvariant();
            diffBuilder.AppendLine($"=== FILE: {file.Item.Path} ({file.ChangeType}) ===");

            try
            {
                // Only fetch the versions we actually need for this change type.
                var baseText = changeType.Contains("add")
                    ? string.Empty
                    : await GetFileContentAtCommitAsync(repositoryId, file.Item.Path, baseCommit, ct);

                var prText = changeType.Contains("delete")
                    ? string.Empty
                    : await GetFileContentAtCommitAsync(repositoryId, file.Item.Path, prCommit, ct);

                var fileDiff = UnifiedDiffGenerator.Build(
                    file.Item.Path, file.ChangeType, baseText, prText);

                diffBuilder.Append(
                    string.IsNullOrWhiteSpace(fileDiff)
                        ? "(no textual changes)\n"
                        : fileDiff);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not build diff for {Path}", file.Item.Path);
                diffBuilder.AppendLine($"(diff unavailable: {ex.Message})");
            }

            diffBuilder.AppendLine();
        }

        return diffBuilder.ToString();
    }

    /// <summary>Latest iteration of the PR, including its source and merge-base commits.</summary>
    private async Task<PullRequestIteration> GetLatestIterationAsync(
        string repositoryId, int prNumber, CancellationToken ct)
    {
        var url = BuildUrl($"git/repositories/{repositoryId}/pullrequests/{prNumber}/iterations");
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, "fetch PR iterations");

        var json = await response.Content.ReadAsStringAsync(ct);
        var iterations = JsonSerializer.Deserialize<IterationsResponse>(json, JsonOpts);

        if (iterations?.Value == null || iterations.Value.Count == 0)
            throw new InvalidOperationException($"No iterations found for PR #{prNumber}.");

        return iterations.Value[^1];
    }

    /// <summary>Files changed in the given PR iteration (folders excluded).</summary>
    private async Task<List<ChangeEntry>> GetIterationChangesAsync(
        string repositoryId, int prNumber, int iterationId, CancellationToken ct)
    {
        var url = BuildUrl(
            $"git/repositories/{repositoryId}/pullrequests/{prNumber}/iterations/{iterationId}/changes",
            "$top=1000");

        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, "fetch iteration changes");

        var json = await response.Content.ReadAsStringAsync(ct);
        var changes = JsonSerializer.Deserialize<IterationChangesResponse>(json, JsonOpts);

        return changes?.ChangeEntries
                   .Where(c => !c.Item.IsFolder)
                   .ToList()
               ?? [];
    }

    /// <summary>Raw text content of a file at a given commit; empty string if absent (404).</summary>
    private async Task<string> GetFileContentAtCommitAsync(
        string repositoryId, string path, string commitId, CancellationToken ct)
    {
        var url = BuildUrl(
            $"git/repositories/{repositoryId}/items",
            $"path={Uri.EscapeDataString(path)}" +
            $"&versionDescriptor.version={commitId}" +
            "&versionDescriptor.versionType=commit&includeContent=true&$format=text");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        using var response = await _httpClient.SendAsync(request, ct);

        // Missing on this branch (e.g. an added/deleted file) → treat as empty side.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return string.Empty;

        await EnsureSuccessAsync(response, "fetch file content");
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string NormalizeChangeType(string changeType) =>
        changeType.ToLowerInvariant() switch
        {
            var c when c.Contains("add") => "Added   ",
            var c when c.Contains("delete") => "Deleted ",
            var c when c.Contains("rename") => "Renamed ",
            _ => "Modified"
        };

    private string BuildUrl(string path, string? queryExtra = null)
    {
        var org = _options.Organization;
        var project = _options.Project;
        var query = $"api-version=7.1{(queryExtra != null ? "&" + queryExtra : "")}";
        return $"https://dev.azure.com/{org}/{project}/_apis/{path}?{query}";
    }

    /// <summary>Organization-scoped URL (no project segment).</summary>
    private string BuildOrgUrl(string path, string? queryExtra = null)
    {
        var org = _options.Organization;
        var query = $"api-version=7.1{(queryExtra != null ? "&" + queryExtra : "")}";
        return $"https://dev.azure.com/{org}/_apis/{path}?{query}";
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            var message = ExtractAzureDevOpsMessage(body)
                          ?? $"Azure DevOps API failed to {operation}: {response.StatusCode}";
            throw new HttpRequestException(message);
        }
    }

    private static string? ExtractAzureDevOpsMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msgProp))
            {
                var raw = msgProp.GetString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                // Strip leading "TF######: " error code prefix
                var colonIndex = raw.IndexOf(':');
                if (colonIndex > 0 && colonIndex < 12)
                    raw = raw[(colonIndex + 1)..].TrimStart();
                return raw;
            }
        }
        catch (JsonException) { }
        return null;
    }
}
