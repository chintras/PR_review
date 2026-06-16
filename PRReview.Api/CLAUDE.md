# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

ASP.NET Core 8.0 Web API that accepts a PR number + base branch, resolves the owning repository from the (org-wide unique) PR number, builds a unified diff of the PR's changed lines from its latest iteration, invokes the Claude CLI as a subprocess, and returns a structured code review (blockers, major issues, minor suggestions, nits, praise).

## Commands

```bash
dotnet build
dotnet run                          # API on https://localhost:7015 and http://localhost:5034
dotnet run --launch-profile https   # Explicit HTTPS profile
dotnet publish -c Release --output ./publish
```

Swagger UI is available at `/swagger` in Development.

## Architecture

**Endpoints (`Controllers/ReviewController.cs`):**
- `POST /api/review` — takes `ReviewRequest` (`baseBranch`, `prNumber`, optional `prBranch`), returns `ReviewResponse`

**Request flow:**
1. `AzureDevOpsService.GetPullRequestByIdAsync(prNumber)` hits the **org-level** endpoint `/{org}/_apis/git/pullrequests/{id}` — PR ids are unique across the organization — and returns the owning repository + source/target branches. No repository is supplied by the caller.
2. `AzureDevOpsService.GetPullRequestDiffAsync(repoId, prNumber)` reads the PR's latest iteration, then for each changed file (max 50, folders excluded) fetches the base version at `commonRefCommit` (merge base) and the PR version at `sourceRefCommit` and produces a unified diff via `UnifiedDiffGenerator`. Using the merge base means the diff reflects only the PR's own changes and stays correct after the PR is merged.
3. `ClaudeCliReviewService` builds a prompt from `SystemPrompt.PrReviewer` + PR metadata + diff (with instructions to review only `+`/`-` lines), then spawns `claude -p --output-format text` with the prompt on stdin
4. Response markdown is parsed via regex into five `List<ReviewItem>` sections and returned as `ReviewResponse`

**Key files:**
- `Program.cs` — DI registration, named HTTP client "AzureDevOps" (Basic auth, 60 s timeout), CORS policy `AllowAngularDev`
- `Services/AzureDevOpsService.cs` — Azure DevOps REST API v7.1; org-level PR resolution + iteration merge-base diff; per-file content fetched by commit id
- `Services/UnifiedDiffGenerator.cs` — dependency-free LCS unified-diff generator (`+`/`-` hunks, 3 lines of context); handles add/edit/delete
- `Services/ClaudeCliReviewService.cs` — process spawning with concurrent stdout/stderr readers; configurable timeout; regex parser for five markdown sections
- `Prompts/SystemPrompt.cs` — static prompt constant; expected output format is `- **[file:LINE]** Title — Comment` under emoji-prefixed `###` headings
- `Configuration/AzureDevOpsOptions.cs` — holds org/project/PAT; `RepositoryAliases`/`ResolveRepository()` are now legacy (unused by the request flow)
- `Models/Responses/ReviewResponse.cs` — `ReviewItem` has `File`, `Line?`, `Title`, `Comment`

## Configuration

`appsettings.json` (use `appsettings.Development.json` locally):

```json
{
  "AzureDevOps": {
    "Organization": "inatech",
    "Project": "Techoil",
    "PatToken": "REPLACE_WITH_PAT_TOKEN",
    "RepositoryAliases": {
      "UI": "TechoilNew",
      "BE": "techoil-backend",
      "MVC": "Techoil",
      "Integrations": "techoil-integrations",
      "ClientSpecific": "TechoilClientSpecific",
      "OCM": "OCM-DBS"
    }
  },
  "Claude": {
    "CliBinaryPath": "claude",
    "TimeoutSeconds": 180
  }
}
```

Environment variable overrides use `__` as separator: `AzureDevOps__PatToken=...`

## Repository Resolution

The repository is resolved automatically from the PR number via the org-level Azure DevOps endpoint — no alias or repository name is needed. `RepositoryAliases` in `appsettings.json` is legacy config that the request flow no longer reads.

## Claude CLI Invocation

The service spawns: `claude -p --output-format text` and writes the full prompt to stdin. If the process exceeds `TimeoutSeconds`, the entire process tree is killed. Exit code ≠ 0 or empty output throws `InvalidOperationException`.

## Notes

- No test projects configured.
- CORS (`AllowAngularDev`) allows all origins — intentional for local dev only.
- The PAT in `appsettings.Development.json` is a real credential; move to user secrets (`dotnet user-secrets`) to avoid committing it.
