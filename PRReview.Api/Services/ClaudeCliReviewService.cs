using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PRReview.Api.Configuration;
using PRReview.Api.Models.AzureDevOps;
using PRReview.Api.Models.Responses;
using PRReview.Api.Prompts;
using PRReview.Api.Services.Interfaces;

namespace PRReview.Api.Services;

public class ClaudeCliReviewService : IClaudeReviewService
{
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeCliReviewService> _logger;

    public ClaudeCliReviewService(
        IOptions<ClaudeOptions> options,
        ILogger<ClaudeCliReviewService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReviewResponse> ReviewPullRequestAsync(
        PullRequestDetails prDetails,
        string diffContent,
        string repository,
        CancellationToken ct = default)
    {
        var sourceBranch = prDetails.SourceRefName.Replace("refs/heads/", "");
        var targetBranch = prDetails.TargetRefName.Replace("refs/heads/", "");

        var prompt = BuildPrompt(prDetails, sourceBranch, targetBranch, diffContent);

        _logger.LogInformation(
            "Invoking claude CLI for PR #{PrId}, prompt length: {Len} chars",
            prDetails.PullRequestId, prompt.Length);

        var rawMarkdown = await InvokeClaudeCliAsync(prompt, ct);

        _logger.LogInformation(
            "Claude CLI returned review for PR #{PrId}, response length: {Len} chars",
            prDetails.PullRequestId, rawMarkdown.Length);

        return ParseReviewResponse(rawMarkdown, prDetails, repository, sourceBranch, targetBranch);
    }

    private async Task<string> InvokeClaudeCliAsync(string prompt, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.CliBinaryPath,
                // -p (--print): non-interactive, print response and exit
                // --output-format text: plain text output (no ANSI/JSON wrapping)
                Arguments = "-p --output-format text",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            }
        };

        process.Start();

        // Write the full prompt via stdin to avoid command-line length limits
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        // Read stdout and stderr concurrently to avoid deadlocks on large outputs
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Claude CLI did not complete within {_options.TimeoutSeconds} seconds.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("Claude CLI exited with code {Code}. Stderr: {Err}",
                process.ExitCode, stderr);
            throw new InvalidOperationException(
                $"Claude CLI failed (exit {process.ExitCode}): {stderr}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException(
                $"Claude CLI returned empty output. Stderr: {stderr}");
        }

        return stdout.Trim();
    }

    private static string BuildPrompt(
        PullRequestDetails pr, string sourceBranch, string targetBranch, string diffContent)
    {
        return $"""
            {SystemPrompt.PrReviewer}

            ---

            Please review this pull request.

            ## PR Details
            Title: {pr.Title}
            Author: {pr.CreatedBy.DisplayName} ({pr.CreatedBy.UniqueName})
            Base branch: {targetBranch}
            PR branch: {sourceBranch}
            Description: {pr.Description ?? "(no description provided)"}

            ## Diff
            {diffContent}
            """;
    }

    private static ReviewResponse ParseReviewResponse(
        string markdown, PullRequestDetails pr, string repository,
        string sourceBranch, string targetBranch)
    {
        var review = new ReviewResponse
        {
            PrNumber = pr.PullRequestId,
            Repository = repository,
            PrTitle = pr.Title,
            Author = $"{pr.CreatedBy.DisplayName} ({pr.CreatedBy.UniqueName})",
            BaseBranch = targetBranch,
            PrBranch = sourceBranch,
            RawMarkdown = markdown
        };

        review.Blockers = ExtractSection(markdown, "BLOCKERS");
        review.MajorIssues = ExtractSection(markdown, "MAJOR ISSUES");
        review.MinorSuggestions = ExtractSection(markdown, "MINOR SUGGESTIONS");
        review.Nits = ExtractSection(markdown, "NITS");
        review.Praise = ExtractSection(markdown, "PRAISE");

        return review;
    }

    private static List<ReviewItem> ExtractSection(string markdown, string sectionName)
    {
        var sectionPattern = new Regex(
            $@"###[^#\n]*{Regex.Escape(sectionName)}[^\n]*\n(.*?)(?=###|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var sectionMatch = sectionPattern.Match(markdown);
        if (!sectionMatch.Success) return [];

        var sectionContent = sectionMatch.Groups[1].Value.Trim();
        if (sectionContent == "*(none)*" || string.IsNullOrWhiteSpace(sectionContent))
            return [];

        var items = new List<ReviewItem>();

        // Match: - **[path/to/file.ts:42]** Title — Comment
        var bulletPattern = new Regex(
            @"^-\s+\*\*\[(?<file>[^\]:]+)(?::(?<line>\d+))?\]\*\*\s+(?<title>[^—–\-]+?)(?:\s*[—–-]+\s*(?<comment>.+))?$",
            RegexOptions.Multiline);

        foreach (Match match in bulletPattern.Matches(sectionContent))
        {
            var item = new ReviewItem
            {
                File = match.Groups["file"].Value.Trim(),
                Title = match.Groups["title"].Value.Trim(),
                Comment = match.Groups["comment"].Success
                    ? match.Groups["comment"].Value.Trim()
                    : string.Empty
            };

            if (int.TryParse(match.Groups["line"].Value, out var line))
                item.Line = line;

            items.Add(item);
        }

        // Fallback: return raw section text if no structured bullets matched
        if (items.Count == 0)
        {
            items.Add(new ReviewItem
            {
                File = string.Empty,
                Title = sectionName,
                Comment = sectionContent
            });
        }

        return items;
    }
}
