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

    public async Task<PullRequestDetails> GetPullRequestDetailsAsync(
        string repository, int prNumber, CancellationToken ct = default)
    {
        var url = BuildUrl($"git/repositories/{repository}/pullrequests/{prNumber}");
        _logger.LogInformation("Fetching PR #{PrNumber} details from {Url}", prNumber, url);

        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, "fetch PR details");

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<PullRequestDetails>(json, JsonOpts)
               ?? throw new InvalidOperationException("Failed to deserialize PR details.");
    }

    public async Task<string> GetPullRequestDiffAsync(
        string repository, int prNumber, CancellationToken ct = default)
    {
        // Step 1: Get the latest iteration ID
        var iterationId = await GetLatestIterationIdAsync(repository, prNumber, ct);

        // Step 2: Get the list of changed files in that iteration
        var changedFiles = await GetIterationChangesAsync(repository, prNumber, iterationId, ct);

        if (changedFiles.Count == 0)
        {
            _logger.LogWarning("No changed files found for PR #{PrNumber}", prNumber);
            return "No changed files detected in this pull request.";
        }

        // Step 3: Build a unified diff string
        var diffBuilder = new StringBuilder();
        diffBuilder.AppendLine("=== CHANGED FILES SUMMARY ===");

        foreach (var file in changedFiles)
        {
            var marker = file.ChangeType.ToLowerInvariant() switch
            {
                "add" => "Added  ",
                "delete" => "Deleted",
                _ => "Modified"
            };
            diffBuilder.AppendLine($"{marker}: {file.Item.Path}");
        }

        diffBuilder.AppendLine();

        // Step 4: Fetch inline diff for each file (up to 50 files to avoid token explosion)
        var filesToReview = changedFiles
            .Where(f => !f.Item.IsFolder)
            .Take(50)
            .ToList();

        foreach (var file in filesToReview)
        {
            diffBuilder.AppendLine($"=== FILE: {file.Item.Path} ({file.ChangeType}) ===");

            try
            {
                var fileDiff = await GetFileDiffAsync(
                    repository, prNumber, iterationId, file.Item.Path, ct);
                diffBuilder.Append(fileDiff);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch diff for {Path}", file.Item.Path);
                diffBuilder.AppendLine($"(diff unavailable: {ex.Message})");
            }

            diffBuilder.AppendLine();
        }

        return diffBuilder.ToString();
    }

    private async Task<int> GetLatestIterationIdAsync(
        string repository, int prNumber, CancellationToken ct)
    {
        var url = BuildUrl($"git/repositories/{repository}/pullrequests/{prNumber}/iterations");
        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, "fetch PR iterations");

        var json = await response.Content.ReadAsStringAsync(ct);
        var iterations = JsonSerializer.Deserialize<IterationsResponse>(json, JsonOpts);

        if (iterations?.Value == null || iterations.Value.Count == 0)
            throw new InvalidOperationException($"No iterations found for PR #{prNumber}.");

        return iterations.Value[^1].Id; // latest iteration
    }

    private async Task<List<ChangeEntry>> GetIterationChangesAsync(
        string repository, int prNumber, int iterationId, CancellationToken ct)
    {
        var url = BuildUrl(
            $"git/repositories/{repository}/pullrequests/{prNumber}/iterations/{iterationId}/changes",
            "$top=200");

        var response = await _httpClient.GetAsync(url, ct);
        await EnsureSuccessAsync(response, "fetch iteration changes");

        var json = await response.Content.ReadAsStringAsync(ct);
        var changesResponse = JsonSerializer.Deserialize<IterationChangesResponse>(json, JsonOpts);

        return changesResponse?.ChangeEntries
               .Where(c => !c.Item.IsFolder)
               .ToList()
               ?? [];
    }

    private async Task<string> GetFileDiffAsync(
        string repository, int prNumber, int iterationId, string filePath, CancellationToken ct)
    {
        // Use the diffs API to get line-level diff for this file
        var encodedPath = Uri.EscapeDataString(filePath);
        var url = BuildUrl(
            $"git/repositories/{repository}/diffs/commits",
            $"targetVersion=PR%3A{prNumber}%40{iterationId}&baseVersion=PR%3A{prNumber}%40{(iterationId - 1 < 1 ? 0 : iterationId - 1)}&targetVersionType=commit&baseVersionType=commit&path={encodedPath}");

        // Fallback: use the PR iteration diff endpoint with path filter
        var diffUrl = BuildUrl(
            $"git/repositories/{repository}/pullrequests/{prNumber}/iterations/{iterationId}/changes",
            $"$top=1&path={encodedPath}");

        // Use the items API to get actual file content for context
        var modifiedUrl = BuildUrl(
            $"git/repositories/{repository}/items",
            $"path={encodedPath}&versionDescriptor.versionType=branch&$format=text&includeContent=true");

        // Try to get the content from the PR branch
        var contentResponse = await _httpClient.GetAsync(modifiedUrl, ct);

        if (!contentResponse.IsSuccessStatusCode)
        {
            return $"(file content unavailable — status {contentResponse.StatusCode})\n";
        }

        var content = await contentResponse.Content.ReadAsStringAsync(ct);
        var lines = content.Split('\n');

        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            sb.AppendLine($"{i + 1,4}: {lines[i]}");
        }

        return sb.ToString();
    }

    private string BuildUrl(string path, string? queryExtra = null)
    {
        var org = _options.Organization;
        var project = _options.Project;
        var query = $"api-version=7.1{(queryExtra != null ? "&" + queryExtra : "")}";
        return $"https://dev.azure.com/{org}/{project}/_apis/{path}?{query}";
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
