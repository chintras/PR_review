using System.ComponentModel.DataAnnotations;

namespace PRReview.Api.Models.Requests;

public class ReviewRequest
{
    /// <summary>
    /// Target/base branch to compare the PR against (e.g. "main", "develop").
    /// The owning repository is resolved automatically from <see cref="PrNumber"/>.
    /// </summary>
    [Required]
    public string BaseBranch { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "PrNumber must be a positive integer.")]
    public int PrNumber { get; set; }

    /// <summary>PR source branch. If omitted, it is derived from the PR metadata.</summary>
    public string? PrBranch { get; set; }
}
