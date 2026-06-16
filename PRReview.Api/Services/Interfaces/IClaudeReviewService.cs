using PRReview.Api.Models.AzureDevOps;
using PRReview.Api.Models.Responses;

namespace PRReview.Api.Services.Interfaces;

public interface IClaudeReviewService
{
    Task<ReviewResponse> ReviewPullRequestAsync(
        PullRequestDetails prDetails,
        string diffContent,
        string repository,
        string baseBranch,
        string prBranch,
        CancellationToken ct = default);
}
