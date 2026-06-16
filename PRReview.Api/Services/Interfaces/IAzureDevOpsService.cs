using PRReview.Api.Models.AzureDevOps;

namespace PRReview.Api.Services.Interfaces;

public interface IAzureDevOpsService
{
    /// <summary>
    /// Resolves a pull request by its (organization-wide unique) id, returning
    /// its metadata including the owning repository and source/target branches.
    /// No repository name is required from the caller.
    /// </summary>
    Task<PullRequestDetails> GetPullRequestByIdAsync(
        int prNumber, CancellationToken ct = default);

    /// <summary>
    /// Builds a unified diff containing only the lines the pull request changed,
    /// computed from its latest iteration (merge-base commit ↔ PR source commit).
    /// This is accurate whether or not the PR has been merged. Capped at 50 files.
    /// </summary>
    Task<string> GetPullRequestDiffAsync(
        string repositoryId, int prNumber, CancellationToken ct = default);
}
