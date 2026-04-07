namespace PRReview.Api.Configuration;

public class ClaudeOptions
{
    public const string SectionName = "Claude";

    /// <summary>
    /// Path to the claude CLI binary. Defaults to "claude" (assumes it's in PATH).
    /// Override if the binary is not on the system PATH.
    /// </summary>
    public string CliBinaryPath { get; set; } = "claude";

    /// <summary>Timeout in seconds for a single PR review. Defaults to 3 minutes.</summary>
    public int TimeoutSeconds { get; set; } = 180;
}
