using System.Text.Json.Serialization;

namespace PRReview.Api.Models.AzureDevOps;

public class PullRequestDetails
{
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("sourceRefName")]
    public string SourceRefName { get; set; } = string.Empty;

    [JsonPropertyName("targetRefName")]
    public string TargetRefName { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public IdentityRef CreatedBy { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class IdentityRef
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("uniqueName")]
    public string UniqueName { get; set; } = string.Empty;
}

public class IterationsResponse
{
    [JsonPropertyName("value")]
    public List<PullRequestIteration> Value { get; set; } = [];
}

public class PullRequestIteration
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class IterationChangesResponse
{
    [JsonPropertyName("changeEntries")]
    public List<ChangeEntry> ChangeEntries { get; set; } = [];
}

public class ChangeEntry
{
    [JsonPropertyName("changeType")]
    public string ChangeType { get; set; } = string.Empty;

    [JsonPropertyName("item")]
    public ChangeItem Item { get; set; } = new();
}

public class ChangeItem
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; set; }
}

public class FileDiffResponse
{
    [JsonPropertyName("blocks")]
    public List<DiffBlock> Blocks { get; set; } = [];

    [JsonPropertyName("modifiedFile")]
    public DiffFile? ModifiedFile { get; set; }

    [JsonPropertyName("originalFile")]
    public DiffFile? OriginalFile { get; set; }
}

public class DiffFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class DiffBlock
{
    [JsonPropertyName("changeType")]
    public int ChangeType { get; set; } // 0=none, 1=add, 2=delete, 3=edit

    [JsonPropertyName("mLine")]
    public int MLine { get; set; } // modified (PR branch) starting line

    [JsonPropertyName("mLinesCount")]
    public int MLinesCount { get; set; }

    [JsonPropertyName("oLine")]
    public int OLine { get; set; } // original (base) starting line

    [JsonPropertyName("oLinesCount")]
    public int OLinesCount { get; set; }

    [JsonPropertyName("truncatedDiff")]
    public bool TruncatedDiff { get; set; }
}
