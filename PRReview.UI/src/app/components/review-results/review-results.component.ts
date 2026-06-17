import { Component, Input, OnChanges, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatButtonModule } from '@angular/material/button';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';

import { ReviewResponse, ReviewItem, REVIEW_CATEGORIES, ReviewCategory } from '../../models/review.model';

interface CategoryData {
  category: ReviewCategory;
  items: ReviewItem[];
}

@Component({
  selector: 'app-review-results',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatTableModule,
    MatExpansionModule,
    MatButtonModule,
    MatBadgeModule,
    MatTooltipModule,
    MatTabsModule,
    MatDividerModule,
  ],
  templateUrl: './review-results.component.html',
  styleUrls: ['./review-results.component.scss'],
})
export class ReviewResultsComponent implements OnChanges {
  @Input() review!: ReviewResponse;

  showRawMarkdown = signal(false);
  categories = signal<CategoryData[]>([]);

  readonly displayedColumns = ['severity', 'file', 'line', 'title', 'comment'];

  ngOnChanges(): void {
    if (!this.review) return;
    this.categories.set(
      REVIEW_CATEGORIES.map((cat) => ({
        category: cat,
        items: this.review[cat.key] ?? [],
      }))
    );
  }

  toggleMarkdown(): void {
    this.showRawMarkdown.update((v) => !v);
  }

  categoriesWithItems = computed(() =>
    this.categories().filter(c => c.items.length > 0)
  );

  totalIssues = computed(() =>
    this.categories().reduce((sum, c) => sum + c.items.length, 0)
  );

  // Only Blockers / Major Issues warrant the review tables. Minor suggestions,
  // nits, and praise on their own still count as a "great PR".
  hasSeriousIssues = computed(() =>
    this.categories().some(
      c => (c.category.key === 'blockers' || c.category.key === 'majorIssues') && c.items.length > 0
    )
  );

  hasItems(items: ReviewItem[]): boolean {
    return items.length > 0;
  }

  trackByIndex(index: number): number {
    return index;
  }
}
