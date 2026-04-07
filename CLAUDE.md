# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

AI-powered code review tool. Users submit a PR number and repository alias; the backend fetches the diff from Azure DevOps, passes it to the Claude CLI, and returns structured review feedback (blockers, major issues, minor issues, nits, praise) to an Angular UI.

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

1. Angular form → `POST /api/review` with `{ repository, prNumber, baseBranch, prBranch }`
2. `ReviewController` calls `AzureDevOpsService` to fetch PR metadata + diff (capped at 50 files)
3. `ClaudeCliReviewService` builds a markdown prompt, spawns `claude -p --output-format text` via stdin/stdout, and parses the response with regex
4. Structured `ReviewResponse` (categorized items + raw markdown) returned to Angular
5. `ReviewResultsComponent` displays results in Material tabs grouped by severity

**Backend structure (`PRReview.Api/`):**

- `Controllers/ReviewController.cs` — single POST endpoint
- `Services/AzureDevOpsService.cs` — Azure DevOps REST API v7.1, fetches iterations/diffs; `EnsureSuccessAsync` parses Azure DevOps error JSON and surfaces a clean human-readable message (strips `TF######:` prefix)
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

- Angular standalone components (no NgModules); signals for reactive state (`loading`, `reviewResult`, `repositories`)
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

Repository aliases map short names (used in the UI form) to actual Azure DevOps repo names.

## Error Handling

Azure DevOps API errors are cleaned up in `AzureDevOpsService.EnsureSuccessAsync` before reaching the UI:
- The raw JSON body is parsed; the `message` field is extracted.
- The `TF######:` error code prefix is stripped (e.g. `TF401180: The requested pull request was not found.` → `The requested pull request was not found.`).
- If JSON parsing fails, a generic `"Azure DevOps API failed to {operation}: {statusCode}"` message is used.
- The cleaned message is set as `ProblemDetails.Detail` in the 502 response from `ReviewController`, which the Angular `ReviewService.handleError` reads via `error.error.detail`.

## CORS

Currently allows all origins (`AllowAngularDev` policy) — intentionally permissive for local development only.
