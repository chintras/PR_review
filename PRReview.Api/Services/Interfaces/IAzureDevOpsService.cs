using PRReview.Api.Models.AzureDevOps;

namespace PRReview.Api.Services.Interfaces;

public interface IAzureDevOpsService
{
    Task<PullRequestDetails> GetPullRequestDetailsAsync(
        string repository, int prNumber, CancellationToken ct = default);

    Task<string> GetPullRequestDiffAsync(
        string repository, int prNumber, CancellationToken ct = default);
}
