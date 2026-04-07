using System.ComponentModel.DataAnnotations;

namespace PRReview.Api.Models.Requests;

public class ReviewRequest
{
    [Required]
    public string BaseBranch { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "PrNumber must be a positive integer.")]
    public int PrNumber { get; set; }

    /// <summary>PR source branch. If omitted, it is derived from the PR metadata.</summary>
    public string? PrBranch { get; set; }

    /// <summary>
    /// Repository alias (e.g. "UI", "BE") or exact repository name.
    /// Aliases are resolved via AzureDevOps:RepositoryAliases in appsettings.
    /// </summary>
    [Required]
    public string Repository { get; set; } = string.Empty;
}
