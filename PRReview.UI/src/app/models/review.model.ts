export interface ReviewRequest {
  baseBranch: string;
  prNumber: number;
  prBranch?: string;
}

export interface ReviewItem {
  file: string;
  line?: number;
  title: string;
  comment: string;
}

export interface ReviewResponse {
  prNumber: number;
  repository: string;
  prTitle: string;
  author: string;
  baseBranch: string;
  prBranch: string;
  blockers: ReviewItem[];
  majorIssues: ReviewItem[];
  minorSuggestions: ReviewItem[];
  nits: ReviewItem[];
  praise: ReviewItem[];
  rawMarkdown: string;
}

export interface ReviewCategory {
  key: keyof Pick<ReviewResponse, 'blockers' | 'majorIssues' | 'minorSuggestions' | 'nits' | 'praise'>;
  label: string;
  icon: string;
  color: string;
  badgeClass: string;
}

export const REVIEW_CATEGORIES: ReviewCategory[] = [
  { key: 'blockers',          label: 'Blockers',           icon: 'block',          color: '#c53030', badgeClass: 'badge-blocker'     },
  { key: 'majorIssues',       label: 'Major Issues',       icon: 'error',          color: '#c05621', badgeClass: 'badge-major'       },
  { key: 'minorSuggestions',  label: 'Minor Suggestions',  icon: 'lightbulb',      color: '#2b6cb0', badgeClass: 'badge-minor'       },
  { key: 'nits',              label: 'Nits',               icon: 'edit_note',      color: '#6b46c1', badgeClass: 'badge-nit'         },
  { key: 'praise',            label: 'Praise',             icon: 'thumb_up',       color: '#276749', badgeClass: 'badge-praise'      },
];
