# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

ASP.NET Core 8.0 Web API that accepts a PR number + repository alias, fetches the diff from Azure DevOps, invokes the Claude CLI as a subprocess, and returns a structured code review (blockers, major issues, minor suggestions, nits, praise).

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
- `POST /api/review` — takes `ReviewRequest`, returns `ReviewResponse`
- `GET /api/review/repositories` — returns the configured alias → repo name map

**Request flow:**
1. Resolve repository alias via `AzureDevOpsOptions.ResolveRepository()` (case-insensitive)
2. `AzureDevOpsService` fetches PR metadata and builds a unified diff string (max 50 files, folders excluded, line-numbered content per file)
3. `ClaudeCliReviewService` builds a prompt from `SystemPrompt.PrReviewer` + PR metadata + diff, then spawns `claude -p --output-format text` with the prompt on stdin
4. Response markdown is parsed via regex into five `List<ReviewItem>` sections and returned as `ReviewResponse`

**Key files:**
- `Program.cs` — DI registration, named HTTP client "AzureDevOps" (Basic auth, 60 s timeout), CORS policy `AllowAngularDev`
- `Services/AzureDevOpsService.cs` — Azure DevOps REST API v7.1; fetches latest iteration ID, then per-file diffs
- `Services/ClaudeCliReviewService.cs` — process spawning with concurrent stdout/stderr readers; configurable timeout; regex parser for five markdown sections
- `Prompts/SystemPrompt.cs` — static prompt constant; expected output format is `- **[file:LINE]** Title — Comment` under emoji-prefixed `###` headings
- `Configuration/AzureDevOpsOptions.cs` — holds org/project/PAT/aliases; `ResolveRepository()` does case-insensitive alias lookup
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

## Adding a New Repository Alias

Add an entry to `RepositoryAliases` in `appsettings.json`. No code changes needed — `AzureDevOpsOptions.ResolveRepository()` handles lookup automatically.

## Claude CLI Invocation

The service spawns: `claude -p --output-format text` and writes the full prompt to stdin. If the process exceeds `TimeoutSeconds`, the entire process tree is killed. Exit code ≠ 0 or empty output throws `InvalidOperationException`.

## Notes

- No test projects configured.
- CORS (`AllowAngularDev`) allows all origins — intentional for local dev only.
- The PAT in `appsettings.Development.json` is a real credential; move to user secrets (`dotnet user-secrets`) to avoid committing it.
