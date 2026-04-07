# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

Angular 19 SPA that lets users submit an Azure DevOps PR for AI-powered code review. Users select a repository alias, enter a PR number and base branch, and receive categorised review feedback rendered in a Material Design tabbed interface.

## Commands

```bash
npm install
npm start        # Dev server → http://localhost:4200
npm run build    # Production build → dist/pr-review-ui/
npm run watch    # Watch mode (development config)
```

No test runner or linter is configured.

## Architecture

**Component tree:**
```
AppComponent (toolbar + router-outlet)
└── ReviewFormComponent   — reactive form, signals for state
    └── ReviewResultsComponent (@Input() review)  — tabbed results table
```

**Key files:**
- `app/app.config.ts` — providers: `provideZoneChangeDetection({ eventCoalescing: true })`, `provideRouter`, `provideHttpClient`, `provideAnimationsAsync`
- `app/app.routes.ts` — single route: `''` → `ReviewFormComponent`; wildcard redirects to root
- `app/components/review-form/review-form.component.ts` — `FormBuilder` group with validators; signals: `loading`, `reviewResult`, `repositories`, `selectedRepository`; computed: `selectedRepoDisplay`; fetches repo list on `ngOnInit`, falls back to hardcoded list if API is unreachable
- `app/components/review-results/review-results.component.ts` — receives `ReviewResponse` via `@Input()`; computed signals `categoriesWithItems()` and `totalIssues()`; toggleable raw markdown section
- `app/services/review.service.ts` — `POST /api/review` and `GET /api/review/repositories`; API base hardcoded to `https://localhost:7015`; errors mapped to user-friendly messages via `catchError`
- `app/models/review.model.ts` — `ReviewRequest`, `ReviewItem`, `ReviewResponse`, `ReviewCategory`; `REVIEW_CATEGORIES` constant drives tab ordering and icons

## Angular Patterns Used

- **Standalone components** (`standalone: true`) — no NgModules; each component declares its own `imports`
- **Signals** (`signal()`, `computed()`) for all local component state — prefer signals over RxJS `BehaviorSubject` for new state
- **New control flow** (`@if`, `@for`) in templates — do not use `*ngIf` / `*ngFor`
- **Reactive forms** via `FormBuilder` — validators on `prNumber`: `required`, `min(1)`, `pattern('^[0-9]+$')`

## Styling

- Material theme: `azure-blue` (set in `angular.json` styles array)
- Global styles in `src/styles.scss`: snackbar theme classes `.snack-success` / `.snack-error`, utility `.full-width`, `.text-muted`
- Component SCSS uses fixed field widths: `.field-xs` 130px, `.field-sm` 180px, `.field-md` 200px+
- Fonts: Inter (Google Fonts, weights 300–700) + Material Icons loaded in `index.html`

## API Integration

The backend must be running on `https://localhost:7015` (HTTPS, self-signed cert in dev). The API base URL is hardcoded in `review.service.ts` — update it there if the port changes. No environment files exist yet.

## Adding a New Review Category

1. Add the entry to `REVIEW_CATEGORIES` in `models/review.model.ts`
2. Add the corresponding field to `ReviewResponse` interface
3. `ReviewResultsComponent` derives tabs from `REVIEW_CATEGORIES` automatically
