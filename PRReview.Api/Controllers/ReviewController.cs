using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PRReview.Api.Configuration;
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
    private readonly AzureDevOpsOptions _adoOptions;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(
        IAzureDevOpsService adoService,
        IClaudeReviewService claudeService,
        IOptions<AzureDevOpsOptions> adoOptions,
        ILogger<ReviewController> logger)
    {
        _adoService = adoService;
        _claudeService = claudeService;
        _adoOptions = adoOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a Claude-powered code review for the specified Azure DevOps pull request.
    /// </summary>
    /// <param name="request">
    /// baseBranch: target branch name (e.g. "main").
    /// prNumber: the PR number.
    /// repository: alias (e.g. "UI", "BE") or exact repository name.
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
        var repository = _adoOptions.ResolveRepository(request.Repository);

        _logger.LogInformation(
            "Review requested — Alias: {Alias} → Repo: {Repo}, PR: #{Pr}, Base: {Base}",
            request.Repository, repository, request.PrNumber, request.BaseBranch);

        try
        {
            var prDetails = await _adoService.GetPullRequestDetailsAsync(
                repository, request.PrNumber, ct);

            var diff = await _adoService.GetPullRequestDiffAsync(
                repository, request.PrNumber, ct);

            var review = await _claudeService.ReviewPullRequestAsync(
                prDetails, diff, repository, ct);

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

    /// <summary>
    /// Returns the configured repository alias mappings.
    /// </summary>
    [HttpGet("repositories")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public IActionResult GetRepositories()
    {
        return Ok(_adoOptions.RepositoryAliases);
    }
}
