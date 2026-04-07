namespace PRReview.Api.Models.Responses;

public class ReviewResponse
{
    public int PrNumber { get; set; }
    public string Repository { get; set; } = string.Empty;
    public string PrTitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public string PrBranch { get; set; } = string.Empty;

    public List<ReviewItem> Blockers { get; set; } = [];
    public List<ReviewItem> MajorIssues { get; set; } = [];
    public List<ReviewItem> MinorSuggestions { get; set; } = [];
    public List<ReviewItem> Nits { get; set; } = [];
    public List<ReviewItem> Praise { get; set; } = [];

    /// <summary>Full unmodified markdown from Claude — use this for rich rendering.</summary>
    public string RawMarkdown { get; set; } = string.Empty;
}

public class ReviewItem
{
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}
