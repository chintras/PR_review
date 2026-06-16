using Microsoft.AspNetCore.Mvc;
using PRReview.Api.Models.Requests;
using PRReview.Api.Models.Responses;
using PRReview.Api.Services.Interfaces;

namespace PRReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewController : ControllerBase
{
    private readonly IAzureDevOpsService _adoService;
    private readonly IClaudeReviewService _claudeService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(
        IAzureDevOpsService adoService,
        IClaudeReviewService claudeService,
        ILogger<ReviewController> logger)
    {
        _adoService = adoService;
        _claudeService = claudeService;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a Claude-powered code review for the specified Azure DevOps pull request.
    /// The owning repository is resolved from the (org-wide unique) PR number; the review
    /// only covers the changes between the base branch and the PR branch.
    /// </summary>
    /// <param name="request">
    /// baseBranch: target branch to diff against (e.g. "main").
    /// prNumber: the PR number (organization-wide unique).
    /// prBranch: optional source branch — derived from PR metadata if omitted.
    /// </param>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Review(
        [FromBody] ReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            // 1. Resolve the PR (and therefore its repository) from the PR number alone.
            var prDetails = await _adoService.GetPullRequestByIdAsync(request.PrNumber, ct);

            var repository = prDetails.Repository.Name;
            var prBranch = !string.IsNullOrWhiteSpace(request.PrBranch)
                ? request.PrBranch!.Trim()
                : prDetails.SourceRefName.Replace("refs/heads/", "");
            var baseBranch = request.BaseBranch.Trim();

            _logger.LogInformation(
                "Review requested — PR #{Pr} → Repo: {Repo}, Base: {Base}, PrBranch: {PrBranch}",
                request.PrNumber, repository, baseBranch, prBranch);

            // 2. Diff the PR's changes only (merge-base ↔ PR tip), accurate even if merged.
            var diff = await _adoService.GetPullRequestDiffAsync(
                prDetails.Repository.Id, request.PrNumber, ct);

            // 3. Review the diff.
            var review = await _claudeService.ReviewPullRequestAsync(
                prDetails, diff, repository, baseBranch, prBranch, ct);

            return Ok(review);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Upstream API failure for PR #{Pr}", request.PrNumber);
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Upstream API error",
                Detail = ex.Message
            });
        }
    }
}
