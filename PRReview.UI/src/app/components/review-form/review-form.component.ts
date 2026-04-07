import { Component, OnInit, signal, computed } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';

import { ReviewService } from '../../services/review.service';
import { ReviewResponse } from '../../models/review.model';
import { ReviewResultsComponent } from '../review-results/review-results.component';

@Component({
  selector: 'app-review-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatCardModule,
    MatTooltipModule,
    ReviewResultsComponent,
  ],
  templateUrl: './review-form.component.html',
  styleUrls: ['./review-form.component.scss'],
})
export class ReviewFormComponent implements OnInit {
  form!: FormGroup;
  loading = signal(false);
  reviewResult = signal<ReviewResponse | null>(null);

  readonly baseBranchOptions = ['main', 'develop'];
  repositories = signal<{ alias: string; name: string }[]>([]);
  selectedRepository = signal<string>('');

  selectedRepoDisplay = computed(() => {
    const alias = this.selectedRepository();
    if (!alias) return '';
    const repo = this.repositories().find(r => r.alias === alias);
    return repo ? `${repo.alias} - ${repo.name}` : alias;
  });

  constructor(
    private fb: FormBuilder,
    private reviewService: ReviewService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      baseBranch: ['main', Validators.required],
      repository: ['', Validators.required],
      prNumber: [null, [Validators.required, Validators.min(1), Validators.pattern('^[0-9]+$')]],
      prBranch: [''],
    });

    this.form.get('repository')!.valueChanges.subscribe(val => {
      this.selectedRepository.set(val ?? '');
    });

    this.reviewService.getRepositories().subscribe({
      next: (repos) => {
        this.repositories.set(
          Object.entries(repos).map(([alias, name]) => ({ alias, name }))
        );
      },
      error: () => {
        // Fallback to defaults if API is unreachable on load
        this.repositories.set([
          { alias: 'UI', name: 'TechoilNew' },
          { alias: 'BE', name: 'techoil-backend' },
          { alias: 'Integrations', name: 'techoil-integrations' },
          { alias: 'ClientSpecific', name: 'TechoilClientSpecific' },
          { alias: 'OCM', name: 'OCM-DBS' },
        ]);
      },
    });
  }

  onSubmit(): void {
    if (this.form.invalid || this.loading()) return;

    this.loading.set(true);
    this.reviewResult.set(null);

    const { baseBranch, repository, prNumber, prBranch } = this.form.value;
    const payload = {
      baseBranch,
      repository,
      prNumber: Number(prNumber),
      ...(prBranch?.trim() ? { prBranch: prBranch.trim() } : {}),
    };

    this.reviewService.submitReview(payload).subscribe({
      next: (result) => {
        this.loading.set(false);
        this.reviewResult.set(result);
        this.showToast('Review fetched successfully!', 'snack-success');
      },
      error: (err: Error) => {
        this.loading.set(false);
        this.showToast(err.message, 'snack-error');
      },
    });
  }

  private showToast(message: string, panelClass: string): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 5000,
      panelClass: [panelClass],
      horizontalPosition: 'right',
      verticalPosition: 'top',
    });
  }

  get prNumberError(): string {
    const ctrl = this.form.get('prNumber');
    if (ctrl?.hasError('required')) return 'PR Number is required';
    if (ctrl?.hasError('min') || ctrl?.hasError('pattern')) return 'Must be a positive integer';
    return '';
  }
}
