# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

AI-powered code review tool. Users submit a PR number and a base branch; the backend resolves the owning repository from the (organization-wide unique) PR number, computes the PR's diff, passes it to the Claude CLI, and returns structured review feedback (blockers, major issues, minor issues, nits, praise) to an Angular UI. No repository is selected by the user.

## Commands

### Launch Both Apps (recommended)

Double-click `Start-PRReview.bat` at the repo root. It opens two terminal windows — one for the API, one for the UI — and waits 5 seconds between them.

| Service | URL |
|---------|-----|
| Swagger | https://localhost:7015/swagger |
| Angular UI | http://localhost:4600 |

### Backend (PRReview.Api/)

```bash
cd PRReview.Api
dotnet build
dotnet run --launch-profile https   # API on https://localhost:7015, Swagger at /swagger
dotnet publish -c Release
```

### Frontend (PRReview.UI/)

```bash
cd PRReview.UI
npm install
npm start           # Dev server on http://localhost:4600 (opens browser automatically)
npm run build       # Production build → dist/pr-review-ui/
npm run watch       # Watch mode (dev config)
```

No test projects or linters are configured yet.

## Architecture

**Request flow:**

1. Angular form → `POST /api/review` with `{ baseBranch, prNumber, prBranch? }` (no repository)
2. `ReviewController` calls `AzureDevOpsService.GetPullRequestByIdAsync` (org-level endpoint) to resolve the PR — PR ids are unique across the organization, so this yields the owning repository plus source/target branches
3. `AzureDevOpsService.GetPullRequestDiffAsync` builds a **unified diff of only the PR's changed lines** from the latest iteration (merge-base `commonRefCommit` ↔ PR-tip `sourceRefCommit`, capped at 50 files). This is accurate even after the PR is merged — comparing branch *tips* is not, since the change is already in the base.
4. `ClaudeCliReviewService` builds a markdown prompt instructing the reviewer to evaluate only added/removed lines, spawns `claude -p --output-format text` via stdin/stdout, and parses the response with regex
5. Structured `ReviewResponse` (categorized items + raw markdown) returned to Angular
6. `ReviewResultsComponent` displays results in Material tabs grouped by severity

**Backend structure (`PRReview.Api/`):**

- `Controllers/ReviewController.cs` — single POST endpoint; resolves PR→repo, then diff, then review
- `Services/AzureDevOpsService.cs` — Azure DevOps REST API v7.1: `GetPullRequestByIdAsync` (org-level PR lookup), `GetPullRequestDiffAsync` (iteration merge-base diff, per-file content fetched by commit id); `EnsureSuccessAsync` parses Azure DevOps error JSON and surfaces a clean human-readable message (strips `TF######:` prefix)
- `Services/UnifiedDiffGenerator.cs` — dependency-free LCS unified-diff generator (`+`/`-` hunks with 3 lines of context); handles add/edit/delete
- `Services/ClaudeCliReviewService.cs` — spawns Claude CLI process, parses markdown output
- `Prompts/SystemPrompt.cs` — static system prompt (elite senior reviewer persona)
- `Configuration/` — `AzureDevOpsOptions` (org/project/PAT/aliases) and `ClaudeOptions` (CLI path/timeout)
- `Models/` — `ReviewRequest`, `ReviewResponse`, Azure DevOps API shapes

**Frontend structure (`PRReview.UI/src/app/`):**

- `components/review-form/` — reactive form with `FormBuilder`; signals for loading/result state
- `components/review-results/` — tabbed Material table of review items
- `services/review.service.ts` — single `HttpClient` call to backend
- `models/review.model.ts` — shared interfaces and category definitions

**Key patterns:**

- Angular standalone components (no NgModules); signals for reactive state (`loading`, `reviewResult`)
- .NET Options pattern: `IOptions<AzureDevOpsOptions>`, `IOptions<ClaudeOptions>` bound from `appsettings.json`
- Services are scoped DI; HTTP client factory used for Azure DevOps (named client with auth header)
- Claude CLI invoked non-interactively with a 180 s timeout (configurable via `Claude.TimeoutSeconds`)

## Configuration

`appsettings.json` (or `appsettings.Development.json` for local):

```json
{
  "AzureDevOps": {
    "Organization": "inatech",
    "Project": "Techoil",
    "PatToken": "...",
    "RepositoryAliases": {
      "UI": "TechoilNew",
      "BE": "techoil-backend",
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

`RepositoryAliases` is **legacy** — the repository is now resolved automatically from the PR number, so the UI no longer sends a repository and the aliases are not used by the request flow. The section can be left in place or removed.

## Error Handling

Azure DevOps API errors are cleaned up in `AzureDevOpsService.EnsureSuccessAsync` before reaching the UI:
- The raw JSON body is parsed; the `message` field is extracted.
- The `TF######:` error code prefix is stripped (e.g. `TF401180: The requested pull request was not found.` → `The requested pull request was not found.`).
- If JSON parsing fails, a generic `"Azure DevOps API failed to {operation}: {statusCode}"` message is used.
- The cleaned message is set as `ProblemDetails.Detail` in the 502 response from `ReviewController`, which the Angular `ReviewService.handleError` reads via `error.error.detail`.

## CORS

Currently allows all origins (`AllowAngularDev` policy) — intentionally permissive for local development only.
