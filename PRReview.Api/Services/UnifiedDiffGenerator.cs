using System.Text;

namespace PRReview.Api.Services;

/// <summary>
/// Dependency-free unified-diff generator (LCS based). Produces a git-style
/// diff (with @@ hunk headers and 3 lines of context) comparing the base-branch
/// version of a file against the PR-branch version, so only the *changed* lines
/// and their immediate context are sent to the reviewer — never the whole file.
/// </summary>
public static class UnifiedDiffGenerator
{
    private const int ContextLines = 3;

    // Guard against pathological memory use on very large files (LCS is O(n*m)).
    private const int MaxLinesForDiff = 6000;

    /// <summary>
    /// Builds a unified diff for a single file.
    /// </summary>
    /// <param name="path">File path (for the diff header).</param>
    /// <param name="changeType">Azure DevOps change type ("add", "edit", "delete", "rename", ...).</param>
    /// <param name="baseText">File content on the base branch (empty for adds).</param>
    /// <param name="prText">File content on the PR branch (empty for deletes).</param>
    public static string Build(string path, string changeType, string baseText, string prText)
    {
        var ct = changeType.ToLowerInvariant();
        var sb = new StringBuilder();

        if (ct.Contains("add"))
        {
            sb.AppendLine($"--- /dev/null");
            sb.AppendLine($"+++ b{path}");
            AppendAllLines(sb, prText, '+');
            return sb.ToString();
        }

        if (ct.Contains("delete"))
        {
            sb.AppendLine($"--- a{path}");
            sb.AppendLine($"+++ /dev/null");
            AppendAllLines(sb, baseText, '-');
            return sb.ToString();
        }

        // edit / rename / content change → real diff
        var baseLines = SplitLines(baseText);
        var prLines = SplitLines(prText);

        if (baseLines.Length > MaxLinesForDiff || prLines.Length > MaxLinesForDiff)
        {
            sb.AppendLine($"--- a{path}");
            sb.AppendLine($"+++ b{path}");
            sb.AppendLine(
                $"(file too large to diff inline — {baseLines.Length} → {prLines.Length} lines; review skipped)");
            return sb.ToString();
        }

        var ops = Diff(baseLines, prLines);
        if (ops.All(o => o.Type == OpType.Equal))
            return string.Empty; // no textual change (e.g. rename only)

        sb.AppendLine($"--- a{path}");
        sb.AppendLine($"+++ b{path}");
        AppendHunks(sb, ops);
        return sb.ToString();
    }

    private static void AppendAllLines(StringBuilder sb, string text, char prefix)
    {
        foreach (var line in SplitLines(text))
            sb.AppendLine($"{prefix}{line}");
    }

    private static string[] SplitLines(string text) =>
        string.IsNullOrEmpty(text) ? [] : text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private enum OpType { Equal, Insert, Delete }

    private readonly record struct Op(OpType Type, string Text, int BaseLine, int PrLine);

    /// <summary>Classic LCS backtrack producing an ordered edit script.</summary>
    private static List<Op> Diff(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        var lcs = new int[n + 1, m + 1];

        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ops = new List<Op>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
                ops.Add(new Op(OpType.Equal, a[x], x++, y++));
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
                ops.Add(new Op(OpType.Delete, a[x], x++, -1));
            else
                ops.Add(new Op(OpType.Insert, b[y], -1, y++));
        }
        while (x < n) ops.Add(new Op(OpType.Delete, a[x], x++, -1));
        while (y < m) ops.Add(new Op(OpType.Insert, b[y], -1, y++));

        return ops;
    }

    /// <summary>Groups the edit script into hunks with surrounding context.</summary>
    private static void AppendHunks(StringBuilder sb, List<Op> ops)
    {
        // Indices of changed ops.
        var changed = new List<int>();
        for (int i = 0; i < ops.Count; i++)
            if (ops[i].Type != OpType.Equal) changed.Add(i);

        int idx = 0;
        while (idx < changed.Count)
        {
            int start = Math.Max(0, changed[idx] - ContextLines);

            // Extend the hunk while the next change is within 2*context of the current end.
            int end = changed[idx];
            int j = idx;
            while (j + 1 < changed.Count && changed[j + 1] - end <= ContextLines * 2)
            {
                j++;
                end = changed[j];
            }
            int hunkEnd = Math.Min(ops.Count - 1, end + ContextLines);

            EmitHunk(sb, ops, start, hunkEnd);
            idx = j + 1;
        }
    }

    private static void EmitHunk(StringBuilder sb, List<Op> ops, int start, int end)
    {
        int baseStart = 0, prStart = 0, baseCount = 0, prCount = 0;
        bool baseStartSet = false, prStartSet = false;

        for (int i = start; i <= end; i++)
        {
            var op = ops[i];
            if (op.Type is OpType.Equal or OpType.Delete)
            {
                if (!baseStartSet) { baseStart = op.BaseLine + 1; baseStartSet = true; }
                baseCount++;
            }
            if (op.Type is OpType.Equal or OpType.Insert)
            {
                if (!prStartSet) { prStart = op.PrLine + 1; prStartSet = true; }
                prCount++;
            }
        }

        sb.AppendLine($"@@ -{baseStart},{baseCount} +{prStart},{prCount} @@");
        for (int i = start; i <= end; i++)
        {
            var op = ops[i];
            var prefix = op.Type switch
            {
                OpType.Insert => '+',
                OpType.Delete => '-',
                _ => ' '
            };
            sb.AppendLine($"{prefix}{op.Text}");
        }
    }
}
