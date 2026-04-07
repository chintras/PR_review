namespace PRReview.Api.Configuration;

public class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PatToken { get; set; } = string.Empty;

    /// <summary>
    /// Short alias → actual Azure DevOps repository name mapping.
    /// e.g. "UI" → "TechoilNew", "BE" → "techoil-backend"
    /// </summary>
    public Dictionary<string, string> RepositoryAliases { get; set; } = new();

    /// <summary>Resolves an alias or passes through the name if no alias matches.</summary>
    public string ResolveRepository(string alias)
    {
        if (RepositoryAliases.TryGetValue(alias, out var actual))
            return actual;

        // Case-insensitive fallback
        var match = RepositoryAliases
            .FirstOrDefault(kv => kv.Key.Equals(alias, StringComparison.OrdinalIgnoreCase));

        return match.Value ?? alias;
    }
}
